using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bwork.FjordCoast.Editor;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Bwork.Authoring.Editor.Rocks
{
    public static class RockCommand
    {
        [Serializable] public sealed class Receipt
        {
            public int schemaVersion = 1;
            public string state, batchId, sourceHash, scene, rootName;
            public RockRecipe recipe;
            public int placed, considered, target, rejectedGround, rejectedHeight, rejectedSlope, rejectedExclusion, rejectedSpacing;
            public int lodGroups, colliders;
        }
        sealed class Planned { public FjordBulkScatter.Result result; public Pose[] poses; }
        static string RootName(string id) => "BFjord Rocks [owned:bwork_rocks:v1:" + id + "]";
        static string ReceiptPath(string id) => ToolSandbox.Generated + "/rocks-" + id + ".json";

        [CliCommand("bwork_rocks", "Prepare, apply, inspect or remove deterministic original rock batches with shared LOD prefabs.", MainThreadRequired = true)]
        public static object Run(
            [CliArg("action", "prepare, apply, status, remove, build-assets, catalog or view")] string action = "status",
            [CliArg("recipePath", "Project/package-relative recipe; empty uses rocks-highland.json")] string recipePath = "",
            [CliArg("batchId", "Owned lowercase rock batch ID")] string batchId = "rocks")
        {
            RockContract.Id(batchId);
            var terrain = ToolSandbox.RequireTerrain();
            if (action == "status") return Status(batchId);
            if (action == "remove") return Publish(batchId, null, null, null);
            var source = RockContract.Admit(ProjectContext.Current.rockSourceRoot);
            if (action == "catalog") return RockLibrary.Catalog(source);
            if (action == "build-assets") return RockLibrary.Build(source);
            if (action == "view")
            {
                var batch = Exact(RootName(batchId)); RockContract.Require(batch != null, "Apply a rock batch before framing it.");
                var renderers = batch.GetComponentsInChildren<Renderer>(true); RockContract.Require(renderers.Length > 0, "Rock batch is empty.");
                var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                RockContract.Require(SceneView.lastActiveSceneView != null, "Open a Scene view to frame rocks.");
                SceneView.lastActiveSceneView.Frame(bounds, false);
                return new { state = "view", batchId, bounds = bounds.ToString() };
            }
            RockContract.Require(action == "prepare" || action == "apply", "Unknown rock action.");
            var recipe = LoadRecipe(recipePath);
            var plan = Plan(recipe, source.manifest, terrain, batchId);
            if (action == "prepare") return Describe(batchId, source, recipe, plan.result, "prepared");
            RockContract.Require(RockLibrary.Ready(source), "Run build-assets for this source generation before applying rocks.");
            return Publish(batchId, source, recipe, plan);
        }
        public static RockRecipe LoadRecipe(string path)
        {
            string full;
            if (string.IsNullOrEmpty(path)) full = ToolSandbox.SamplePath("rocks-highland.json");
            else
            {
                ProjectContext.ValidateRelative(path);
                if (path.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    var parts = path.Split('/'); RockContract.Require(parts.Length >= 3, "Recipe package path is incomplete.");
                    var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(parts[1]);
                    RockContract.Require(package != null, "Recipe package is not installed.");
                    full = RockContract.FileAt(package.resolvedPath, string.Join("/", parts.Skip(2)));
                }
                else full = RockContract.FileAt(ProjectContext.ProjectRoot, path);
            }
            ProjectContext.RejectLinks(full);
            RockContract.Require(File.Exists(full) && new FileInfo(full).Length <= 65536, "Recipe must exist and contain at most 64 KiB.");
            return RockContract.Parse<RockRecipe>(File.ReadAllText(full));
        }
        static Planned Plan(RockRecipe recipe, RockManifest manifest, Terrain terrain, string batchId)
        {
            var planner = RockContract.PlannerRecipe(recipe, manifest);
            var spatial = SpatialExclusions.Create(ToolSandbox.Root, recipe.exclusions, recipe.waterMode == "none");
            var water = recipe.waterMode == "none" ? null : ConnectedWaterCommand.ActiveField();
            RockContract.Require(recipe.waterMode == "none" || water != null, "Water-aware rocks require applied connected water.");
            Vector3 origin = terrain.transform.position; var data = terrain.terrainData;
            RockContract.Require(terrain.transform.rotation == Quaternion.identity && terrain.transform.lossyScale == Vector3.one, "Rock scatter expects an unrotated, unit-scale Terrain.");
            float? Ground(Vector2 p)
            {
                float u = (p.x - origin.x) / data.size.x, v = (p.y - origin.z) / data.size.z;
                if (u < 0 || u > 1 || v < 0 || v > 1) return null;
                int hx = Mathf.Clamp(Mathf.FloorToInt(u * data.holesResolution), 0, data.holesResolution - 1);
                int hz = Mathf.Clamp(Mathf.FloorToInt(v * data.holesResolution), 0, data.holesResolution - 1);
                if (data.IsHole(hx, hz)) return null;
                return origin.y + data.GetInterpolatedHeight(u, v);
            }
            Vector3 Normal(Vector3 p) => data.GetInterpolatedNormal((p.x - origin.x) / data.size.x, (p.z - origin.z) / data.size.z);
            bool Excluded(Vector3 p, float radius)
            {
                if (spatial.Intersects(p, radius + recipe.exclusionPadding)) return true;
                if (water != null)
                {
                    var point = new Vector2(p.x, p.z);
                    float shoreClearance = radius + recipe.exclusionPadding;
                    if (recipe.waterMode == "bank") shoreClearance = Mathf.Max(shoreClearance, recipe.maximumWaterDistance);
                    if (water.IsNearLakeOrOcean(point, shoreClearance)) return true;
                    var sample = water.SampleForBank(point, recipe.maximumWaterDistance);
                    if (!float.IsFinite(sample.Distance)) return true;
                    if (recipe.waterMode == "bank" && (sample.Distance < radius || sample.Distance > recipe.maximumWaterDistance || p.y < sample.Height || p.y > sample.Height + recipe.maximumBankHeight)) return true;
                    float depth = sample.Height - p.y;
                    if (recipe.waterMode == "shallow" && (sample.Distance > 0 || depth < recipe.minimumWaterDepth || depth > recipe.maximumWaterDepth)) return true;
                }
                float minimum = p.y, maximum = p.y;
                foreach (var offset in SupportOffsets(radius))
                {
                    var height = Ground(new Vector2(p.x, p.z) + offset);
                    if (!height.HasValue) return true;
                    minimum = Mathf.Min(minimum, height.Value); maximum = Mathf.Max(maximum, height.Value);
                }
                return maximum - minimum > recipe.maximumGroundRelief;
            }
            var result = FjordBulkScatter.Plan(planner, Ground, Normal, Excluded, Neighbors(batchId));
            var variants = manifest.variants.ToDictionary(v => v.id);
            var poses = result.placements.Select(p =>
            {
                float yaw = recipe.orientation == "strata" ? recipe.strataYawDegrees + (p.rotationDegrees / 180 - 1) * recipe.yawVariationDegrees : p.rotationDegrees;
                if (recipe.orientation == "flow")
                {
                    var flow = water.SampleForBank(new Vector2(p.position.x, p.position.z), recipe.maximumWaterDistance).Flow;
                    yaw = Mathf.Atan2(flow.x, flow.y) * Mathf.Rad2Deg + 90 + (p.rotationDegrees / 180 - 1) * recipe.yawVariationDegrees;
                }
                var rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, Normal(p.position)), recipe.alignment) * Quaternion.Euler(0, yaw, 0);
                var size = variants[p.prefabKey].Size * p.scale;
                // Seat the transformed base plane against the lowest of nine Terrain samples, then bury.
                float y = p.position.y;
                foreach (var offset in SupportOffsets(Mathf.Max(size.x, size.z) * .35f))
                {
                    var local = rotation * new Vector3(offset.x, 0, offset.y);
                    var h = Ground(new Vector2(p.position.x + local.x, p.position.z + local.z));
                    if (h.HasValue) y = Mathf.Min(y, h.Value - local.y);
                }
                return new Pose(new Vector3(p.position.x, y - size.y * recipe.burialFraction, p.position.z), rotation);
            }).ToArray();
            return new Planned { result = result, poses = poses };
        }
        static IEnumerable<Vector2> SupportOffsets(float radius)
        {
            yield return Vector2.zero;
            for (int i = 0; i < 8; i++) yield return new Vector2(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4)) * radius;
        }
        // Other batches are immutable scene obstacles; replacing one's own batch must not exclude its old positions.
        static FjordBulkScatter.Placement[] Neighbors(string batchId)
        {
            var result = new List<FjordBulkScatter.Placement>();
            foreach (Transform batch in ToolSandbox.Root)
            {
                if (batch.name == RootName(batchId) || !batch.name.StartsWith("BFjord Rocks [owned:bwork_rocks:v1:", StringComparison.Ordinal) || !batch.name.EndsWith("]", StringComparison.Ordinal)) continue;
                foreach (Transform rock in batch)
                {
                    var renderers = rock.GetComponentsInChildren<Renderer>(true);
                    RockContract.Require(renderers.Length > 0, "Neighbor rock lost its renderers.");
                    var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    float radius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
                    result.Add(new FjordBulkScatter.Placement(new FjordBulkScatter.Species { id = rock.name, prefabKey = rock.name, radius = radius }, bounds.center, 0, 1));
                }
            }
            return result.ToArray();
        }
        public static string[] RiverRockBatchIds()
        {
            const string prefix = "BFjord Rocks [owned:bwork_rocks:v1:";
            // Include receipts and roots so a partially restored batch fails closed before water changes.
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Transform root in ToolSandbox.Root)
                if (root.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    RockContract.Require(root.name.EndsWith("]", StringComparison.Ordinal), "Unfinished rock batch requires recovery before changing water.");
                    string id = root.name.Substring(prefix.Length, root.name.Length - prefix.Length - 1);
                    RockContract.Id(id); ids.Add(id);
                }
            if (Directory.Exists(ToolSandbox.Generated))
                foreach (string path in Directory.EnumerateFiles(ToolSandbox.Generated, "rocks-*.json", SearchOption.TopDirectoryOnly))
                {
                    string id = Path.GetFileNameWithoutExtension(path).Substring("rocks-".Length);
                    RockContract.Id(id); ids.Add(id);
                }
            var dependencies = new List<string>();
            foreach (string id in ids.OrderBy(value => value, StringComparer.Ordinal))
            {
                var receipt = Read(id); var root = Exact(RootName(id));
                RequireOwnership(receipt, root);
                if (root != null && receipt.recipe.waterMode != "none") dependencies.Add(id);
            }
            return dependencies.ToArray();
        }
        public static bool HasRiverRocks() => RiverRockBatchIds().Length > 0;
        public static void RequireNoCustomRiverDependencies()
        {
            var custom = RiverRockBatchIds().Where(id => id != "river-bank" && id != "river-shallow").ToArray();
            RockContract.Require(custom.Length == 0, "Remove these water-aware batches with bwork_rocks action=remove before changing the river scene: " + string.Join(", ", custom));
        }
        public static object PrepareRiverAssets()
        {
            var source = RockContract.Admit(ProjectContext.Current.rockSourceRoot);
            foreach (string sample in new[] { "rocks-river-bank.json", "rocks-river-shallow.json" })
                RockContract.PlannerRecipe(LoadRecipe(SampleRecipe(sample)), source.manifest);
            RockLibrary.Build(source);
            return RockLibrary.Catalog(source);
        }
        static string SampleRecipe(string name) => "Packages/" + UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(RockCommand).Assembly).name + "/Samples/" + name;
        public static object[] RemoveRiverRocks() => new[] { Run("remove", "", "river-bank"), Run("remove", "", "river-shallow") };
        public static object[] ApplyRiverRocks() => new[] { Run("apply", SampleRecipe("rocks-river-bank.json"), "river-bank"), Run("apply", SampleRecipe("rocks-river-shallow.json"), "river-shallow") };
        static Receipt Describe(string id, RockSource source, RockRecipe recipe, FjordBulkScatter.Result p, string state) => new Receipt
        {
            state = state, batchId = id, sourceHash = source.hash, recipe = recipe, scene = SceneManager.GetActiveScene().path,
            rootName = RootName(id), placed = p.placements.Length, target = p.targetCount, considered = p.considered,
            rejectedGround = p.rejectedGround, rejectedHeight = p.rejectedHeight, rejectedSlope = p.rejectedSlope,
            rejectedExclusion = p.rejectedExclusion, rejectedSpacing = p.rejectedSpacing
        };
        static Transform Exact(string name)
        {
            var matches = ToolSandbox.Root.Cast<Transform>().Where(t => t.name == name).ToArray();
            RockContract.Require(matches.Length <= 1, "Duplicate owned rock root: " + name); return matches.SingleOrDefault();
        }
        static Receipt Read(string id)
        {
            string path = ReceiptPath(id); ProjectContext.RejectLinks(path);
            if (!File.Exists(path)) return null;
            RockContract.Require(new FileInfo(path).Length <= 1024 * 1024, "Oversized rock receipt.");
            var receipt = RockContract.Parse<Receipt>(File.ReadAllText(path));
            RockContract.Require(receipt != null && receipt.schemaVersion == 1 && receipt.batchId == id && receipt.rootName == RootName(id) && receipt.scene == SceneManager.GetActiveScene().path, "Rock receipt ownership mismatch.");
            RockContract.Require(receipt.state == "removed" || receipt.state == "applied" && receipt.recipe != null &&
                (receipt.recipe.waterMode == "none" || receipt.recipe.waterMode == "bank" || receipt.recipe.waterMode == "shallow"), "Invalid rock receipt state or water dependency.");
            return receipt;
        }
        static object Status(string id)
        {
            var receipt = Read(id); var root = Exact(RootName(id));
            RequireOwnership(receipt, root);
            return new { state = root == null ? "absent" : "applied", batchId = id, existing = root == null ? 0 : root.childCount, receipt };
        }
        static void RequireOwnership(Receipt receipt, Transform root)
        {
            RockContract.Require((receipt != null && receipt.state == "applied") == (root != null), "Rock receipt/root disagree; preserve the scene for recovery.");
            if (root == null) return;
            RockContract.Require(receipt.sourceHash != null && receipt.sourceHash.Length == 64 && receipt.sourceHash.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f') && root.childCount == receipt.placed, "Owned rock root no longer matches its receipt.");
            string prefix = ToolSandbox.Generated + "/RockLibrary/" + receipt.sourceHash + "/";
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                RockContract.Require(filter.sharedMesh != null && AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(prefix, StringComparison.Ordinal), "Owned rock root contains foreign geometry; preserve it before replacement.");
        }
        static object Publish(string id, RockSource source, RockRecipe recipe, Planned planned)
        {
            var parent = ToolSandbox.Root; var scene = parent.gameObject.scene; string path = ReceiptPath(id);
            var previous = Read(id); var old = Exact(RootName(id)); RequireOwnership(previous, old);
            string pendingName = RootName(id) + " pending"; RockContract.Require(Exact(pendingName) == null, "Unfinished rock batch requires recovery.");
            ProjectContext.RejectLinks(path + ".meta");
            byte[] priorBytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
            byte[] priorMeta = File.Exists(path + ".meta") ? File.ReadAllBytes(path + ".meta") : null;
            RockContract.Require((priorBytes == null) == (priorMeta == null), "Receipt JSON/meta ownership is incomplete.");
            var receipt = planned == null ? new Receipt { batchId = id, scene = scene.path, rootName = RootName(id), state = "removed" } : Describe(id, source, recipe, planned.result, "applied");
            GameObject pending = null; Scene holding = default; bool moved = false, saveAttempted = false; int sibling = old == null ? 0 : old.GetSiblingIndex();
            try
            {
                if (planned != null)
                {
                    pending = new GameObject(pendingName); pending.SetActive(false); pending.transform.SetParent(parent, false);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(RockLibrary.MaterialPath(source, recipe.materialProfile));
                    for (int i = 0; i < planned.result.placements.Length; i++)
                    {
                        var p = planned.result.placements[i]; var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RockLibrary.PrefabPath(source, p.prefabKey));
                        RockContract.Require(prefab != null && material != null, "Prepared rock assets are missing.");
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pending.transform);
                        instance.name = p.prefabKey; instance.transform.SetPositionAndRotation(planned.poses[i].position, planned.poses[i].rotation);
                        instance.transform.localScale = Vector3.one * p.scale;
                        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                        if (!recipe.colliders) foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                    }
                    receipt.lodGroups = pending.GetComponentsInChildren<LODGroup>(true).Length;
                    receipt.colliders = pending.GetComponentsInChildren<Collider>(true).Length;
                }
                if (old != null)
                { holding = EditorSceneManager.NewPreviewScene(); old.SetParent(null, true); SceneManager.MoveGameObjectToScene(old.gameObject, holding); moved = true; }
                if (pending != null) { pending.name = RootName(id); pending.SetActive(true); }
                ToolSandbox.Persist(new TextAsset(JsonUtility.ToJson(receipt, true)), Path.GetFileName(path));
                saveAttempted = true; ToolSandbox.Save();
            }
            catch (Exception error)
            {
                var failures = new List<Exception> { error };
                void Attempt(Action action) { try { action(); } catch (Exception e) { failures.Add(e); } }
                Attempt(() => { if (pending != null) Object.DestroyImmediate(pending); });
                if (old != null && moved) Attempt(() => { SceneManager.MoveGameObjectToScene(old.gameObject, scene); old.SetParent(parent, true); old.SetSiblingIndex(sibling); });
                Attempt(() =>
                {
                    if (priorBytes == null)
                    {
                        if (File.Exists(path) || File.Exists(path + ".meta"))
                            RockContract.Require(AssetDatabase.DeleteAsset(path) && !File.Exists(path) && !File.Exists(path + ".meta"),
                                "Failed to remove the attempted rock receipt; preserve it for recovery: " + path);
                    }
                    else { File.WriteAllBytes(path, priorBytes); File.WriteAllBytes(path + ".meta", priorMeta); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); }
                });
                if (saveAttempted && failures.Count == 1) Attempt(ToolSandbox.Save);
                if (holding.IsValid() && (old == null || old.gameObject.scene == scene)) Attempt(() => EditorSceneManager.ClosePreviewScene(holding));
                if (failures.Count > 1) throw new AggregateException("Rock publication failed with rollback errors; preserve the open scene.", failures);
                throw;
            }
            if (old != null) Object.DestroyImmediate(old.gameObject);
            if (holding.IsValid()) EditorSceneManager.ClosePreviewScene(holding);
            return receipt;
        }
    }
}

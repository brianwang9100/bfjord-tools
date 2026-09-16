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

namespace Bwork.Authoring.Editor
{
    public static class FoliageCommand
    {
        const string LegacyOwnedBatch = "Bwork Foliage [owned:bwork_foliage:v1]";
        const string LegacyPendingBatch = "Bwork Foliage [owned:bwork_foliage:pending]";
        const string LegacyRecipeAsset = "bwork-foliage-recipe.json";
        const string LegacyResultAsset = "bwork-foliage-result.json";

        [Serializable]
        public sealed class Species
        {
            public string id, prefabKey;
            public float weight, radius, minimumScale, maximumScale;
            public float groundOffsetMeters;
            public bool alignToSurface;
        }

        [Serializable]
        public sealed class Area
        {
            public float x, y, width, height;
            public Rect Rect => new Rect(x, y, width, height);
        }

        [Serializable]
        public sealed class Recipe
        {
            public string id;
            public Area area;
            public int seed, maximumCount;
            public float densityPerHectare, minimumSpacing;
            public float minimumHeight, maximumHeight, minimumSlopeDegrees, maximumSlopeDegrees;
            public Species[] species;
            public SpatialExclusions.Primitive[] exclusions;
            public int clusterCount;
            public float clusterRadius;
            public string spacingGroup;
            public float maximumWaterDistance, maximumBankHeight = 3;

            public FjordBulkScatter.Recipe PlannerRecipe() => new FjordBulkScatter.Recipe
            {
                id = id, area = area?.Rect ?? default, seed = seed, maximumCount = maximumCount,
                densityPerHectare = densityPerHectare, minimumSpacing = minimumSpacing,
                minimumHeight = minimumHeight, maximumHeight = maximumHeight,
                minimumSlopeDegrees = minimumSlopeDegrees, maximumSlopeDegrees = maximumSlopeDegrees,
                clusterCount = clusterCount, clusterRadius = clusterRadius,
                species = species?.Select(value => new FjordBulkScatter.Species
                {
                    id = value.id, prefabKey = value.prefabKey, weight = value.weight, radius = value.radius,
                    minimumScale = value.minimumScale, maximumScale = value.maximumScale,
                    groundOffsetMeters = value.groundOffsetMeters,
                    alignToSurface = SurfaceAlignment(value),
                }).ToArray(),
            };

            static bool SurfaceAlignment(Species species)
            {
                if (species.alignToSurface && FoliageGrounding.IsCanopy(species.prefabKey))
                    throw new ArgumentException("Canopy species must remain upright: " + species.prefabKey);
                return species.alignToSurface;
            }
        }

        [Serializable]
        public sealed class Receipt
        {
            public string action, batchId, batchRoot, recipeId, state, error, recipeAssetPath, resultAssetPath;
            public int target, considered, placed, existing, lodGroups, renderers, collidersRemoved, surfaceAligned;
            public int rejectedGround, rejectedHeight, rejectedSlope, rejectedExclusion, rejectedSpacing;
            public int exclusionGroups, exclusionTriangles, exclusionPrimitives;
            public double elapsedSeconds;
            public object assetCatalog;
        }

        [CliCommand("bwork_foliage", "Plan, apply, remove, or inspect a selected tool-owned deterministic foliage batch.", MainThreadRequired = true)]
        public static Receipt Run(
            [CliArg("action", "prepare, apply, remove, status, build-assets, catalog, view, grass-view or wind-frame")] string action = "apply",
            [CliArg("recipePath", "Optional project- or package-relative JSON recipe path")] string recipePath = "",
            [CliArg("batchId", "Optional lowercase batch ID; empty selects the legacy foliage batch")] string batchId = "",
            [CliArg("windSeconds", "wind-frame: -1 restores live time; 0–60 fixes time for reproducible captures")] float windSeconds = -1)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            ToolSandbox.RequireTerrain();
            batchId = ValidateBatchId(batchId);
            string batchRoot = OwnedBatchName(batchId);
            var receipt = new Receipt { action = action, batchId = batchId, batchRoot = batchRoot };
            if (action == "wind-frame") { receipt.assetCatalog = FoliagePresentation.WindFrame(windSeconds); receipt.state = "wind-preview"; return receipt; }
            if (action == "grass-view") { receipt.assetCatalog = FoliagePresentation.GrassView(); receipt.state = "view"; return receipt; }
            if (action == "view") { receipt.assetCatalog = FoliagePresentation.View(); receipt.state = "view"; return receipt; }
            if (action == "build-assets" || action == "catalog")
            {
                receipt.assetCatalog = action == "build-assets" ? (object)FoliagePresentation.Build() : FoliagePresentation.Status();
                receipt.state = action == "build-assets" ? "assets-ready" : "catalog";
                return receipt;
            }
            if (action == "prepare" || action == "apply")
            {
                var recipe = LoadRecipe(recipePath);
                receipt.recipeId = recipe.id;
                var spatial = SpatialExclusions.Create(ToolSandbox.Root, recipe.exclusions);
                if (action == "prepare")
                {
                    var prefabs = ResolvePrefabs(recipe);
                    Describe(Plan(recipe, spatial.Intersects, batchId, prefabs), receipt);
                    DescribeExisting(receipt, batchRoot);
                    DescribeIndex(spatial, receipt);
                    receipt.state = "prepared";
                }
                else
                {
                    receipt = Apply(recipe, spatial.Intersects, batchId, spatial);
                }
            }
            else if (action == "remove")
            {
                var batch = ExactBatch(batchRoot);
                if (batch != null) Object.DestroyImmediate(batch.gameObject);
                ToolSandbox.Save();
                DescribeExisting(receipt, batchRoot);
                receipt.state = batch == null ? "absent" : "removed";
            }
            else if (action == "status")
            {
                DescribeExisting(receipt, batchRoot);
                DescribeIndex(SpatialExclusions.Create(ToolSandbox.Root), receipt);
                receipt.state = receipt.existing == 0 ? "absent" : "applied";
            }
            else throw new ArgumentException("Unknown action. Use prepare, apply, remove, or status.");
            receipt.elapsedSeconds = watch.Elapsed.TotalSeconds;
            return receipt;
        }

        public static Receipt Apply(Recipe recipe, Func<Vector3, float, bool> excludes, string batchId = "", SpatialExclusions spatial = null)
        {
            if (recipe == null || excludes == null) throw new ArgumentNullException("Recipe and exclusion query are required.");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            batchId = ValidateBatchId(batchId);
            string ownedBatch = OwnedBatchName(batchId), pendingBatch = PendingBatchName(batchId);
            string recipeAsset = RecipeAssetName(batchId), resultAsset = ResultAssetName(batchId);
            string recipeJson = JsonUtility.ToJson(recipe, true);
            var prefabs = ResolvePrefabs(recipe);
            var plan = Plan(recipe, excludes, batchId, prefabs);
            var receipt = new Receipt { action = "apply", batchId = batchId, batchRoot = ownedBatch,
                recipeId = recipe.id, state = "rejected", recipeAssetPath = ToolSandbox.Generated + "/" + recipeAsset,
                resultAssetPath = ToolSandbox.Generated + "/" + resultAsset };
            Describe(plan, receipt);
            if (spatial != null) DescribeIndex(spatial, receipt);
            var snapshots = new[] { new MetadataSnapshot(receipt.recipeAssetPath), new MetadataSnapshot(receipt.resultAssetPath) };
            var sandbox = ToolSandbox.Root;
            var mainScene = sandbox.gameObject.scene;
            var stalePending = ExactBatch(pendingBatch);
            if (stalePending != null) throw new InvalidOperationException("An unfinished foliage replacement exists; preserve it for recovery before retrying.");
            var pending = new GameObject(pendingBatch);
            pending.SetActive(false);
            pending.transform.SetParent(sandbox, false);
            Transform old = null;
            Scene holdingScene = default;
            bool oldActive = false, metadataAttempted = false, saveAttempted = false;
            int oldSibling = 0;
            try
            {
                foreach (var placement in plan.placements)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[placement.prefabKey], pending.transform);
                    if (instance == null) throw new InvalidOperationException("Could not instantiate " + placement.prefabKey);
                    instance.name = placement.speciesId;
                    instance.transform.SetPositionAndRotation(placement.position, placement.rotation);
                    instance.transform.localScale = Vector3.one * placement.scale;
                    foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                    { Object.DestroyImmediate(collider); receipt.collidersRemoved++; }
                }
                old = ExactBatch(ownedBatch);
                if (old != null)
                {
                    oldActive = old.gameObject.activeSelf;
                    oldSibling = old.GetSiblingIndex();
                    holdingScene = EditorSceneManager.NewPreviewScene();
                    old.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(old.gameObject, holdingScene);
                }
                pending.name = ownedBatch;
                pending.SetActive(true);
                DescribeExisting(receipt, ownedBatch);
                receipt.state = "applied";
                receipt.elapsedSeconds = watch.Elapsed.TotalSeconds;
                // The old root remains recoverable until both metadata files and the scene are durable.
                metadataAttempted = true;
                ToolSandbox.Persist(new TextAsset(recipeJson), recipeAsset);
                ToolSandbox.Persist(new TextAsset(JsonUtility.ToJson(receipt, true)), resultAsset);
                saveAttempted = true;
                ToolSandbox.Save();
            }
            catch (Exception failure)
            {
                var failures = new List<Exception> { failure };
                Attempt(() => { if (pending != null) Object.DestroyImmediate(pending); }, failures);
                if (old != null)
                {
                    Attempt(() =>
                    {
                        if (old.gameObject.scene != mainScene) SceneManager.MoveGameObjectToScene(old.gameObject, mainScene);
                        old.SetParent(sandbox, true);
                        old.SetSiblingIndex(oldSibling);
                        old.gameObject.SetActive(oldActive);
                    }, failures);
                }
                // A failed write can partially replace its file; restore exact bytes and GUID metadata.
                if (metadataAttempted) foreach (var snapshot in snapshots) Attempt(snapshot.Restore, failures);
                // Do not publish a partially restored scene. A clean rollback is saved over a failed publication attempt.
                if (saveAttempted && failures.Count == 1) Attempt(ToolSandbox.Save, failures);
                // Never close a preview scene that still holds the only recoverable old root.
                if (holdingScene.IsValid() && (old == null || old.gameObject.scene == mainScene))
                    Attempt(() => EditorSceneManager.ClosePreviewScene(holdingScene), failures);
                if (failures.Count > 1) throw new AggregateException("Foliage replacement failed; rollback errors are included and the open scene must be preserved for recovery.", failures);
                throw;
            }
            // Publication has committed. Cleanup errors must not roll back by deleting the new root.
            try
            {
                if (old != null) Object.DestroyImmediate(old.gameObject);
                if (holdingScene.IsValid()) EditorSceneManager.ClosePreviewScene(holdingScene);
            }
            catch (Exception cleanup)
            {
                Debug.LogError("Foliage replacement committed, but prior preview cleanup needs attention: " + cleanup);
            }
            return receipt;
        }

        sealed class MetadataSnapshot
        {
            readonly string path;
            readonly byte[] content, meta;
            public MetadataSnapshot(string path)
            {
                this.path = path;
                ProjectContext.RejectLinks(path); ProjectContext.RejectLinks(path + ".meta");
                if (Directory.Exists(path) || Directory.Exists(path + ".meta")) throw new IOException("Foliage metadata path is a directory: " + path);
                content = File.Exists(path) ? File.ReadAllBytes(path) : null;
                meta = File.Exists(path + ".meta") ? File.ReadAllBytes(path + ".meta") : null;
                if ((content == null) != (meta == null))
                    throw new IOException("Foliage metadata and its Unity meta file must either both exist or both be absent: " + path);
            }
            public void Restore()
            {
                if (File.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    if (!AssetDatabase.DeleteAsset(path))
                    {
                        File.Delete(path);
                        File.Delete(path + ".meta");
                    }
                }
                if (content != null) File.WriteAllBytes(path, content);
                if (meta != null) File.WriteAllBytes(path + ".meta", meta);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (content != null) AssetDatabase.ImportAsset(path,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                if (!Matches(path, content) || !Matches(path + ".meta", meta))
                    throw new IOException("Could not restore exact foliage metadata bytes and Unity GUID: " + path);
            }

            static bool Matches(string candidate, byte[] expected) => expected == null
                ? !File.Exists(candidate)
                : File.Exists(candidate) && File.ReadAllBytes(candidate).SequenceEqual(expected);
        }

        static void Attempt(Action action, List<Exception> failures)
        {
            try { action(); } catch (Exception error) { failures.Add(error); }
        }

        /// <summary>Returns terrain height only at a finite supported coordinate, including terrain-hole rejection.</summary>
        public static float? SampleGround(TerrainData data, Vector3 origin, Vector2 xz)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            float u = (xz.x - origin.x) / data.size.x, v = (xz.y - origin.z) / data.size.z;
            if (!float.IsFinite(u) || !float.IsFinite(v) || !float.IsFinite(origin.y) ||
                u < 0 || u > 1 || v < 0 || v > 1) return null;
            int resolution = data.holesResolution;
            int x = Mathf.Min(resolution - 1, Mathf.FloorToInt(u * resolution));
            int z = Mathf.Min(resolution - 1, Mathf.FloorToInt(v * resolution));
            if (resolution <= 0 || data.IsHole(x, z)) return null;
            return origin.y + data.GetInterpolatedHeight(u, v);
        }

        static FjordBulkScatter.Result Plan(Recipe recipe, Func<Vector3, float, bool> excludes, string batchId, Dictionary<string, GameObject> prefabs)
        {
            var terrain = ToolSandbox.RequireTerrain();
            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            float? Ground(Vector2 xz) => SampleGround(data, origin, xz);
            Vector3 Normal(Vector3 point)
            {
                float u = (point.x - origin.x) / data.size.x, v = (point.z - origin.z) / data.size.z;
                return terrain.transform.TransformDirection(data.GetInterpolatedNormal(u, v));
            }
            if (!float.IsFinite(recipe.maximumWaterDistance) || recipe.maximumWaterDistance < 0 || recipe.maximumWaterDistance > 200 ||
                !float.IsFinite(recipe.maximumBankHeight) || recipe.maximumBankHeight < 0 || recipe.maximumBankHeight > 50)
                throw new ArgumentException("Water-distance and bank-height filters are outside the recipe bounds.");
            var water = recipe.maximumWaterDistance > 0 ? ConnectedWaterCommand.ActiveField() : null;
            if (recipe.maximumWaterDistance > 0 && water == null) throw new InvalidOperationException("This bank recipe requires applied connected water.");
            bool Excluded(Vector3 point, float radius)
            {
                if (excludes(point, radius)) return true;
                if (water == null) return false;
                var sample = water.SampleForBank(new Vector2(point.x, point.z), recipe.maximumWaterDistance);
                return sample.Distance < radius || sample.Distance > recipe.maximumWaterDistance ||
                    point.y < sample.Height || point.y > sample.Height + recipe.maximumBankHeight;
            }
            var roots = prefabs.Where(pair => FoliageGrounding.IsCanopy(pair.Key))
                .ToDictionary(pair => pair.Key, pair => FoliageGrounding.FromPrefab(pair.Value), StringComparer.Ordinal);
            var support = roots.Count == 0 ? null : FoliageGrounding.TerrainSupport.Read(terrain);
            Vector3? Fit(FjordBulkScatter.Species species, Vector3 position, float scale) =>
                roots.TryGetValue(species.prefabKey, out var root) ? FoliageGrounding.Fit(root, position, scale, support.Minimum) : position;
            return FjordBulkScatter.Plan(recipe.PlannerRecipe(), Ground, Normal, Excluded, Neighbors(recipe, batchId), support == null ? null : Fit);
        }

        static FjordBulkScatter.Placement[] Neighbors(Recipe recipe, string batchId)
        {
            if (string.IsNullOrEmpty(recipe.spacingGroup)) return Array.Empty<FjordBulkScatter.Placement>();
            if (recipe.spacingGroup.Length > 32 || recipe.spacingGroup.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
                throw new ArgumentException("A spacing group must be a simple name of at most 32 characters.");
            var neighbors = new List<FjordBulkScatter.Placement>();
            const string prefix = "Bwork Foliage [owned:bwork_foliage:v1:";
            foreach (Transform batch in ToolSandbox.Root)
            {
                if (!batch.name.StartsWith(prefix, StringComparison.Ordinal) || !batch.name.EndsWith("]", StringComparison.Ordinal)) continue;
                string otherId = batch.name.Substring(prefix.Length, batch.name.Length - prefix.Length - 1);
                if (otherId == batchId) continue;
                var source = AssetDatabase.LoadAssetAtPath<TextAsset>(ToolSandbox.Generated + "/" + RecipeAssetName(otherId));
                if (source == null) continue;
                var otherRecipe = JsonUtility.FromJson<Recipe>(source.text);
                if (otherRecipe?.spacingGroup != recipe.spacingGroup) continue;
                var species = otherRecipe.PlannerRecipe().species.ToDictionary(s => s.id, StringComparer.Ordinal);
                foreach (Transform plant in batch)
                {
                    if (!species.TryGetValue(plant.name, out var descriptor)) throw new InvalidDataException("Spacing-group instance does not match its saved recipe: " + plant.name);
                    neighbors.Add(new FjordBulkScatter.Placement(descriptor, plant.position, plant.eulerAngles.y,
                        Mathf.Max(Mathf.Abs(plant.lossyScale.x), Mathf.Abs(plant.lossyScale.z))));
                }
            }
            return neighbors.ToArray();
        }

        static Dictionary<string, GameObject> ResolvePrefabs(Recipe recipe)
        {
            if (recipe.species == null) throw new ArgumentException("Recipe species are required.");
            // Validate alignment before resolving or preparing assets, including dry-run requests.
            recipe.PlannerRecipe();
            var result = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (var species in recipe.species)
            {
                string path = FoliagePresentation.Resolve(species.prefabKey);
                if ((species.prefabKey.StartsWith("bfjord:", StringComparison.Ordinal) ||
                    path.StartsWith(ToolSandbox.Generated + "/FoliageLibrary/", StringComparison.Ordinal)) && species.minimumScale < .5f)
                    throw new ArgumentException("Prepared wind variants require minimumScale >= .5 to preserve their culling displacement bound.");
                if (result.ContainsKey(species.prefabKey)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponentsInChildren<LODGroup>(true).Length != 1 ||
                    prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new InvalidOperationException("Missing prepared shared LOD prefab: " + species.prefabKey);
                result.Add(species.prefabKey, prefab);
            }
            return result;
        }

        static Recipe LoadRecipe(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return DefaultRecipe();
            if (Path.IsPathRooted(path)) throw new ArgumentException("Recipe path must be project- or package-relative.");
            path = path.Replace('\\', '/');
            if (path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var parts = path.Split('/');
                if (parts.Length < 3) throw new ArgumentException("Package recipe path must include a package name and file.");
                var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(parts[1])
                    ?? throw new FileNotFoundException("Recipe package is not installed.", parts[1]);
                string packageRoot = Path.GetFullPath(package.resolvedPath) + Path.DirectorySeparatorChar;
                string packaged = Path.GetFullPath(Path.Combine(packageRoot, string.Join("/", parts.Skip(2))));
                if (!packaged.StartsWith(packageRoot, StringComparison.Ordinal) || !File.Exists(packaged))
                    throw new FileNotFoundException("Package recipe is outside the package or missing.", path);
                return ReadRecipe(packaged);
            }
            string root = Path.GetFullPath(Directory.GetCurrentDirectory()) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(root, path));
            if (!full.StartsWith(root, StringComparison.Ordinal) || !File.Exists(full))
                throw new FileNotFoundException("Recipe path is outside the project or missing.", path);
            return ReadRecipe(full);
        }

        static Recipe ReadRecipe(string full)
        {
            var recipe = JsonUtility.FromJson<Recipe>(File.ReadAllText(full));
            return recipe ?? throw new InvalidDataException("Recipe JSON is empty or invalid.");
        }

        static Recipe DefaultRecipe() => new Recipe
        {
            id = "tool-sandbox-foliage-001", area = new Area { x = 40, y = 40, width = 420, height = 420 }, seed = 20260915,
            densityPerHectare = 25, minimumSpacing = 9, minimumHeight = -1000, maximumHeight = 3000,
            minimumSlopeDegrees = 0, maximumSlopeDegrees = 32, maximumCount = 500,
            species = new[]
            {
                MakeSpecies("mature-fir", "Assets/Bwork/ThirdParty/PolyHavenMatureFir/Prefabs/MatureFir_A.prefab", .25f, 2.8f, .8f, 1.08f),
                MakeSpecies("pine-sapling", "Assets/Bwork/ThirdParty/PolyHaven/Prefabs/pine_sapling_small_b.prefab", .25f, 1.3f, .85f, 1.2f),
                MakeSpecies("shrub", "Assets/Bwork/ThirdParty/PolyHaven/Prefabs/shrub_03_a.prefab", .2f, 1.1f, .8f, 1.2f),
                MakeSpecies("fern", "Assets/Bwork/ThirdParty/PolyHaven/Prefabs/fern_02_b.prefab", .2f, .7f, .8f, 1.2f),
                MakeSpecies("moss-rock", "Assets/Bwork/ThirdParty/PolyHaven/Prefabs/rock_moss_set_01_rock01.prefab", .1f, 1.4f, .65f, 1.05f),
            },
        };

        static Species MakeSpecies(string id, string path, float weight, float radius, float low, float high) =>
            new Species { id = id, prefabKey = path, weight = weight, radius = radius, minimumScale = low, maximumScale = high };

        static string ValidateBatchId(string value)
        {
            if (value == null) value = "";
            if (value.Length > 32 || value.Any(character =>
                !(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') &&
                character != '-' && character != '_') ||
                (value.Length > 0 && !char.IsLetterOrDigit(value[0])))
                throw new ArgumentException("Batch ID must be empty or 1-32 lowercase letters, digits, hyphens, or underscores and start with a letter or digit.");
            return value;
        }

        static string OwnedBatchName(string batchId) => string.IsNullOrEmpty(batchId) ? LegacyOwnedBatch :
            "Bwork Foliage [owned:bwork_foliage:v1:" + batchId + "]";
        static string PendingBatchName(string batchId) => string.IsNullOrEmpty(batchId) ? LegacyPendingBatch :
            "Bwork Foliage [owned:bwork_foliage:pending:" + batchId + "]";
        static string RecipeAssetName(string batchId) => string.IsNullOrEmpty(batchId) ? LegacyRecipeAsset :
            "bwork-foliage-" + batchId + "-recipe.json";
        static string ResultAssetName(string batchId) => string.IsNullOrEmpty(batchId) ? LegacyResultAsset :
            "bwork-foliage-" + batchId + "-result.json";

        static Transform ExactBatch(string name)
        {
            var found = ToolSandbox.Root.Cast<Transform>().Where(value => value.name == name).ToArray();
            if (found.Length > 1) throw new InvalidOperationException("Duplicate tool-owned foliage roots require manual review.");
            return found.SingleOrDefault();
        }

        static void Describe(FjordBulkScatter.Result plan, Receipt receipt)
        {
            receipt.target = plan.targetCount; receipt.considered = plan.considered; receipt.placed = plan.placements.Length;
            receipt.rejectedGround = plan.rejectedGround; receipt.rejectedHeight = plan.rejectedHeight;
            receipt.rejectedSlope = plan.rejectedSlope; receipt.rejectedExclusion = plan.rejectedExclusion;
            receipt.rejectedSpacing = plan.rejectedSpacing;
            receipt.surfaceAligned = plan.placements.Count(p => p.alignedToSurface);
        }

        static void DescribeExisting(Receipt receipt, string batchRoot)
        {
            var batch = ExactBatch(batchRoot);
            receipt.existing = batch == null ? 0 : batch.childCount;
            receipt.lodGroups = batch == null ? 0 : batch.GetComponentsInChildren<LODGroup>(true).Length;
            receipt.renderers = batch == null ? 0 : batch.GetComponentsInChildren<Renderer>(true).Length;
        }

        static void DescribeIndex(SpatialExclusions index, Receipt receipt)
        {
            receipt.exclusionGroups = index.GroupCount;
            receipt.exclusionTriangles = index.TriangleCount;
            receipt.exclusionPrimitives = index.PrimitiveCount;
        }

    }
}

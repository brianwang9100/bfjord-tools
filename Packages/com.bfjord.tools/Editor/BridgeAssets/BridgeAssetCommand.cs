using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Bwork.Authoring.Editor.BridgeAssets
{
    public static class BridgeAssetCommand
    {
        internal sealed class OwnedBridge
        {
            public GameObject Root;
            public Scene Scene;
            public string GenerationDirectory;
        }
        internal static OwnedBridge RequireOwned(string batch)
        {
            BridgeAssetContract.ValidateId(batch, "batchId");
            var scene = RequireScene();
            var receipt = Read(batch);
            var root = ExactRoot(scene, batch);
            RequireOwnership(receipt, root);
            BridgeAssetContract.Require(receipt != null && !receipt.removed && receipt.targetScene == scene.path, "An applied bridge in the current scene is required.");
            ValidateExisting(receipt, root);
            return new OwnedBridge { Root = root, Scene = scene, GenerationDirectory = receipt.generationDirectory };
        }
        static string Generated => ProjectContext.Current.bridgeAssetGeneratedRoot;
        const int ReceiptVersion = 1;
        [Serializable] sealed class Receipt
        {
            public int schemaVersion = ReceiptVersion;
            public string batchId, sourceHash, placementHash, recipeHash, manifestId, generationDirectory, targetScene, rootName;
            public BridgePlacement placement;
            public BridgePoint boundsMin, boundsMax;
            public int[] triangles;
            public int renderers, colliders;
            public long sourceBytes;
            public bool removed;
            public string[] cleanupDirectories = Array.Empty<string>();
        }

        [CliCommand("bwork_bridge_asset", "Prepare, import, inspect or remove one owned, exact-sized straight level coastal bridge. Blender generation is external; no terrain edits or scaling.", MainThreadRequired = true, Tags = new[] { "authoring/bridge" })]
        public static object Run(
            [CliArg("action", "prepare, apply, status or remove")] string action = "status",
            [CliArg("manifestPath", "Absolute or project-relative prepared manifest.json")] string manifestPath = "",
            [CliArg("placementRecipePath", "Absolute or project-relative placement JSON; top deck midpoint in metres")] string placementRecipePath = "",
            [CliArg("batchId", "Explicit lowercase owned bridge batch ID")] string batchId = "")
        {
            BridgeAssetContract.ValidateId(batchId, "batchId");
            Scene scene = RequireScene();
            var previous = Read(batchId);
            if (previous != null) BridgeAssetContract.Require(previous.targetScene == scene.path, "Bridge receipt belongs to another scene: " + previous.targetScene);
            var old = ExactRoot(scene, batchId);
            if (action == "status") return Status(previous, old, batchId);
            if (action == "remove") return Remove(scene, previous, old, batchId);
            BridgeAssetContract.Require(action == "prepare" || action == "apply", "Unknown bridge action.");
            RequireOwnership(previous, old);
            BridgeAssetContract.Require(previous == null || !previous.removed, "Complete pending removal with action=remove before replacement.");
            var prepared = BridgeAssetContract.Prepare(manifestPath, placementRecipePath, batchId);
            BridgeAssetContract.Require(prepared.Placement.targetScene == scene.path, "Open the placement's exact target scene before preparing or applying.");
            bool unchanged = previous != null && previous.sourceHash == prepared.SourceHash && previous.placementHash == prepared.PlacementHash;
            if (unchanged) ValidateExisting(previous, old);
            if (action == "prepare")
            {
                var p = prepared.Placement; var m = prepared.Manifest;
                Vector3 socket = Quaternion.Euler(0, p.yawDegrees, 0) * new Vector3(0, 0, m.totalLength / 2);
                return new
                {
                    state = "prepared", batchId, unchanged, sourceHash = prepared.SourceHash, recipeHash = m.recipeHash,
                    placementHash = prepared.PlacementHash, sourceBytes = prepared.SourceBytes, sourceFiles = prepared.RelativeFiles.Length,
                    targetScene = scene.path, boundsMin = m.boundsMin, boundsMax = m.boundsMax, deckWidth = m.deckWidth, clearCarriageway = m.clearCarriageway,
                    socketStart = BridgePoint.From(p.position.Vector - socket), socketEnd = BridgePoint.From(p.position.Vector + socket),
                    triangles = m.lods.Select(l => l.triangles).ToArray(), materials = m.materials.Length,
                    actualFbxValidation = "Imported geometry, axes, UV/material bindings and collider are validated on apply before publication."
                };
            }
            if (unchanged)
            {
                var warnings = Cleanup(previous);
                return new { state = "unchanged", batchId, sourceHash = previous.sourceHash, root = old.name, warnings };
            }
            if (old != null) ValidateExisting(previous, old);
            if (previous != null)
            {
                Cleanup(previous);
                BridgeAssetContract.Require(previous.cleanupDirectories.Length < 16, "Too many obsolete bridge generations remain; finish cleanup before another replacement.");
            }
            return Apply(scene, prepared, previous, old);
        }

        static object Apply(Scene scene, BridgePreparedSource source, Receipt previous, GameObject old)
        {
            string batch = source.Placement.batchId;
            string generation = BatchDirectory(batch) + "/" + source.SourceHash.Substring(0, 16) + "-" + Guid.NewGuid().ToString("N");
            string receiptPath = ReceiptPath(batch);
            byte[] priorBytes = File.Exists(receiptPath) ? File.ReadAllBytes(receiptPath) : null;
            // This journal keeps an interrupted import distinguishable from a committed bridge.
            string journalPath = BatchDirectory(batch) + "/pending.json";
            BridgeAssetContract.Require(!File.Exists(journalPath), "An interrupted bridge generation needs recovery; preserve pending.json and inspect the owned generation before retry.");
            Directory.CreateDirectory(generation);
            WriteJson(journalPath, new { schemaVersion = 1, batchId = batch, targetScene = scene.path, sourceHash = source.SourceHash, generationDirectory = generation });
            BridgeImported imported = null;
            var held = new WaterGeneration.HeldRoot(old == null ? null : old.transform);
            bool published = false, saveAttempted = false;
            Receipt next = null;
            try
            {
                imported = BridgeAssetImporter.Import(source, generation, RootName(batch) + " Pending");
                BridgeAssetContract.Require(SceneManager.GetActiveScene() == scene, "The active scene changed during bridge import.");
                BridgeAssetContract.Require(BridgeAssetContract.Fingerprint(source.Directory, File.ReadAllText(source.ManifestPath), source.RelativeFiles) == source.SourceHash, "Bridge source bytes changed during import; prepare again.");
                next = new Receipt
                {
                    batchId = batch, sourceHash = source.SourceHash, placementHash = source.PlacementHash,
                    recipeHash = source.Manifest.recipeHash, manifestId = source.Manifest.id,
                    generationDirectory = generation, targetScene = scene.path, rootName = RootName(batch), placement = source.Placement,
                    boundsMin = BridgePoint.From(imported.Bounds.min), boundsMax = BridgePoint.From(imported.Bounds.max),
                    triangles = imported.Triangles, colliders = imported.Colliders, renderers = imported.Renderers, sourceBytes = source.SourceBytes,
                    cleanupDirectories = (previous == null ? Array.Empty<string>() : previous.cleanupDirectories.Append(previous.generationDirectory)).Where(GenerationExists).Distinct(StringComparer.Ordinal).ToArray()
                };
                held.Park();
                imported.Root.name = RootName(batch); imported.Root.SetActive(true);
                published = true;
                WriteJson(receiptPath, next);
                saveAttempted = true;
                Save(scene);
            }
            catch (Exception error)
            {
                var errors = new List<Exception> { error };
                WaterGeneration.Attempt(() => { if (imported?.Root != null) Object.DestroyImmediate(imported.Root); }, errors);
                WaterGeneration.Attempt(held.Restore, errors);
                if (published) WaterGeneration.Attempt(() => RestoreBytes(receiptPath, priorBytes), errors);
                if (saveAttempted) WaterGeneration.Attempt(() => Save(scene), errors);
                if (errors.Count == 1)
                {
                    WaterGeneration.Attempt(() => DeleteGeneration(batch, generation), errors);
                    if (errors.Count == 1) WaterGeneration.Attempt(() => DeleteFileAsset(journalPath), errors);
                }
                throw new AggregateException(errors.Count == 1 ? "Bridge import failed; the previous bridge was preserved." : "Bridge recovery is incomplete; pending.json and generated files are retained for recovery.", errors);
            }
            var cleanupWarnings = new List<string>();
            bool released = false;
            try { held.Release(); released = true; } catch (Exception error) { cleanupWarnings.Add("Committed bridge; old hidden root cleanup failed: " + error.Message); }
            if (released) cleanupWarnings.AddRange(Cleanup(next));
            try { DeleteFileAsset(journalPath); } catch (Exception error) { cleanupWarnings.Add("Committed bridge; journal cleanup failed: " + error.Message); }
            return new
            {
                state = "committed", batchId = batch, sourceHash = source.SourceHash, root = imported.Root.name,
                triangles = imported.Triangles, imported.Renderers, imported.Colliders,
                boundsMin = next.boundsMin, boundsMax = next.boundsMax,
                receipt = receiptPath, generationDirectory = generation, warnings = cleanupWarnings.ToArray()
            };
        }

        static object Remove(Scene scene, Receipt previous, GameObject old, string batch)
        {
            RequireOwnership(previous, old);
            if (previous == null) return new { state = "absent", batchId = batch };
            if (!previous.removed)
            {
                ValidateExisting(previous, old);
                var held = new WaterGeneration.HeldRoot(old.transform);
                byte[] bytes = File.ReadAllBytes(ReceiptPath(batch));
                bool saved = false;
                try
                {
                    held.Park(); previous.removed = true;
                    WriteJson(ReceiptPath(batch), previous);
                    saved = true; Save(scene);
                }
                catch (Exception error)
                {
                    var errors = new List<Exception> { error };
                    WaterGeneration.Attempt(held.Restore, errors);
                    WaterGeneration.Attempt(() => RestoreBytes(ReceiptPath(batch), bytes), errors);
                    if (saved) WaterGeneration.Attempt(() => Save(scene), errors);
                    throw new AggregateException("Bridge removal failed; restoration was attempted.", errors);
                }
                held.Release();
            }
            var warnings = Cleanup(previous).ToList();
            if (warnings.Count == 0)
            {
                try { DeleteFileAsset(ReceiptPath(batch)); }
                catch (Exception error) { warnings.Add(error.Message); }
            }
            return new { state = warnings.Count == 0 ? "removed" : "removed_cleanup_pending", batchId = batch, warnings = warnings.ToArray() };
        }

        static object Status(Receipt receipt, GameObject root, string batch)
        {
            bool pending = File.Exists(BatchDirectory(batch) + "/pending.json");
            string state = receipt == null ? root == null ? "absent" : "ownership_conflict" : receipt.removed ? root == null ? "removed_cleanup_pending" : "ownership_conflict" : root == null ? "ownership_conflict" : "applied";
            return new { state, batchId = batch, root = root == null ? null : root.name, sourceHash = receipt?.sourceHash, recipeHash = receipt?.recipeHash,
                triangles = receipt?.triangles, renderers = root == null ? 0 : root.GetComponentsInChildren<Renderer>(true).Length,
                colliders = root == null ? 0 : root.GetComponentsInChildren<Collider>(true).Length, pendingRecovery = pending,
                generationDirectory = receipt?.generationDirectory, targetScene = receipt?.targetScene,
                cleanupPending = receipt == null ? 0 : receipt.cleanupDirectories.Count(Directory.Exists) + (receipt.removed && Directory.Exists(receipt.generationDirectory) ? 1 : 0) };
        }

        static Scene RequireScene()
        {
            return ProjectContext.RequireAllowedScene();
        }

        static void RequireOwnership(Receipt receipt, GameObject root)
        {
            bool expectedRoot = receipt != null && !receipt.removed;
            BridgeAssetContract.Require((root != null) == expectedRoot, "Bridge root and receipt disagree. Preserve the scene and recover ownership before mutation.");
        }
        static void ValidateExisting(Receipt receipt, GameObject root)
        {
            BridgeAssetContract.Require(root != null && root.activeSelf && root.transform.parent == null && Vector3.Distance(root.transform.position, receipt.placement.position.Vector) < .001f && Quaternion.Angle(root.transform.rotation, Quaternion.Euler(0, receipt.placement.yawDegrees, 0)) < .01f && Vector3.Distance(root.transform.localScale, Vector3.one) < .001f, "The owned bridge transform/active state was edited; restore it before replacing or removing.");
            BridgeAssetContract.Require(Directory.Exists(receipt.generationDirectory) && root.GetComponentsInChildren<Renderer>(true).Length == receipt.renderers && root.GetComponentsInChildren<Collider>(true).Length == receipt.colliders && root.GetComponentsInChildren<LODGroup>(true).Length == 1 && root.GetComponent<LODGroup>()?.lodCount == 3, "The owned bridge hierarchy or imported assets were edited or are incomplete.");
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                BridgeAssetContract.Require(filter.sharedMesh != null && AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(receipt.generationDirectory + "/", StringComparison.Ordinal), "The owned bridge mesh binding changed.");
        }
        static GameObject ExactRoot(Scene scene, string batch)
        {
            var roots = scene.GetRootGameObjects().Where(g => g.name == RootName(batch)).ToArray();
            BridgeAssetContract.Require(roots.Length <= 1, "Duplicate owned bridge roots for batch " + batch);
            return roots.FirstOrDefault();
        }
        static Receipt Read(string batch)
        {
            string path = ReceiptPath(batch); if (!File.Exists(path)) return null;
            BridgeAssetContract.Require(new FileInfo(path).Length <= 1024 * 1024, "Bridge receipt is too large.");
            Receipt r;
            try { r = JsonConvert.DeserializeObject<Receipt>(File.ReadAllText(path)); }
            catch (JsonException error) { throw new InvalidDataException("Invalid bridge receipt.", error); }
            BridgeAssetContract.Require(r != null && r.schemaVersion == ReceiptVersion && r.batchId == batch && BridgeAssetContract.IsHash(r.sourceHash) && BridgeAssetContract.IsHash(r.placementHash) && r.rootName == RootName(batch) && r.placement != null && r.targetScene == r.placement.targetScene && r.triangles != null && r.triangles.Length == 3, "Invalid bridge ownership receipt.");
            BridgeAssetContract.ReadPlacementJson(JsonConvert.SerializeObject(r.placement), batch);
            ValidateGeneration(batch, r.generationDirectory);
            r.cleanupDirectories = r.cleanupDirectories ?? Array.Empty<string>();
            BridgeAssetContract.Require(r.cleanupDirectories.Length <= 16, "Too many pending bridge generations; finish cleanup first.");
            BridgeAssetContract.Require(!r.cleanupDirectories.Contains(r.generationDirectory), "Receipt cleanup cannot contain the current bridge generation.");
            foreach (string directory in r.cleanupDirectories) ValidateGeneration(batch, directory);
            return r;
        }
        static string[] Cleanup(Receipt receipt)
        {
            var warnings = new List<string>();
            foreach (string directory in receipt.cleanupDirectories.Concat(receipt.removed ? new[] { receipt.generationDirectory } : Array.Empty<string>()).Distinct(StringComparer.Ordinal))
                try { DeleteGeneration(receipt.batchId, directory); }
                catch (Exception error) { warnings.Add("Committed state retained; obsolete generation cleanup failed: " + error.Message); }
            var remaining = receipt.cleanupDirectories.Where(GenerationExists).Distinct(StringComparer.Ordinal).ToArray();
            if (!receipt.cleanupDirectories.SequenceEqual(remaining))
            {
                receipt.cleanupDirectories = remaining;
                try { WriteJson(ReceiptPath(receipt.batchId), receipt); }
                catch (Exception error) { warnings.Add("Committed state retained; cleanup receipt update failed: " + error.Message); }
            }
            return warnings.ToArray();
        }
        static bool GenerationExists(string directory) => Directory.Exists(directory) || File.Exists(directory + ".meta");
        static void ValidateGeneration(string batch, string directory)
        {
            string prefix = BatchDirectory(batch) + "/";
            BridgeAssetContract.Require(directory != null && directory.StartsWith(prefix, StringComparison.Ordinal), "Generation escaped bridge batch directory.");
            string name = directory.Substring(prefix.Length);
            BridgeAssetContract.Require(name.Length == 49 && name[16] == '-' && name.Where((c, i) => i != 16).All(c => "0123456789abcdef".Contains(c)), "Invalid owned bridge generation name.");
        }
        static void DeleteGeneration(string batch, string directory)
        {
            ValidateGeneration(batch, directory);
            if ((Directory.Exists(directory) || File.Exists(directory + ".meta")) && !AssetDatabase.DeleteAsset(directory))
                throw new IOException("Could not remove owned generation: " + directory);
        }
        static void WriteJson(string path, object value) => RestoreBytes(path, System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, Formatting.Indented) + "\n"));
        static void RestoreBytes(string path, byte[] bytes)
        {
            if (bytes == null) { DeleteFileAsset(path); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        static void DeleteFileAsset(string path)
        {
            if ((File.Exists(path) || File.Exists(path + ".meta")) && !AssetDatabase.DeleteAsset(path)) throw new IOException("Could not remove bridge receipt/journal: " + path);
        }
        static void Save(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scene.path)) throw new IOException("Could not save bridge target scene.");
        }
        static string RootName(string batch) => "Bwork Bridge Asset [batch:" + batch + "]";
        static string BatchDirectory(string batch) => Generated + "/" + batch;
        static string ReceiptPath(string batch) => BatchDirectory(batch) + "/receipt.json";
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bwork.Authoring.Editor.BridgeAssets
{
    public static class BridgeSurfaceCommand
    {
        [Serializable] sealed class Receipt
        {
            public int schemaVersion = 1;
            public string profileId, profileHash, directory;
        }

        [CliCommand("bwork_bridge_surface", "Change bridge surfaces without rebuilding meshes, colliders or placement.", MainThreadRequired = true)]
        public static object Run(
            [CliArg("action", "prepare, apply, status or reset")] string action = "status",
            [CliArg("batchId", "Applied bridge batch")] string batchId = "",
            [CliArg("profilePath", "Independent JSON material profile")] string profilePath = "")
        {
            var bridge = BridgeAssetCommand.RequireOwned(batchId);
            string receiptPath = bridge.GenerationDirectory + "/surface.json";
            var prior = ReadReceipt(receiptPath, bridge.GenerationDirectory);
            if (action == "status") return new { state = prior == null ? "default" : "applied", profile = prior?.profileId, hash = prior?.profileHash };
            BridgeAssetContract.Require(new[] { "prepare", "apply", "reset" }.Contains(action), "Unknown surface action.");
            var manifest = JsonConvert.DeserializeObject<BridgeManifest>(File.ReadAllText(bridge.GenerationDirectory + "/manifest.json"));
            var defaults = manifest.materials.ToDictionary(m => m.key, m => AssetDatabase.LoadAssetAtPath<Material>(bridge.GenerationDirectory + "/" + m.key + ".mat"), StringComparer.Ordinal);
            BridgeAssetContract.Require(defaults.Values.All(m => m != null), "An original bridge material is missing.");
            var renderers = bridge.Root.GetComponentsInChildren<MeshRenderer>(true);
            var bindings = renderers.Select(r => r.sharedMaterials).ToArray();
            var expected = new Dictionary<string, Material>(defaults, StringComparer.Ordinal);
            if (prior != null)
                foreach (string key in defaults.Keys)
                {
                    var overrideMaterial = AssetDatabase.LoadAssetAtPath<Material>(prior.directory + "/" + key + ".mat");
                    if (overrideMaterial != null) expected[key] = overrideMaterial;
                }
            var keys = bindings.Select(row => row.Select(m => Key(m, expected)).ToArray()).ToArray();
            PreparedBridgeSurface prepared = action == "reset" ? null : BridgeSurfaceContract.Prepare(profilePath, defaults.Keys.ToArray());
            bool unchanged = prepared == null ? prior == null : prior != null && prior.profileHash == prepared.Hash;
            if (action == "prepare") return new { state = "prepared", unchanged, profile = prepared.Profile.id, hash = prepared.Hash, materials = prepared.Profile.materials.Select(m => m.key).ToArray(), geometryChanges = false };
            if (unchanged) return new { state = "unchanged", geometryChanges = false };
            string directory = prepared == null ? null : bridge.GenerationDirectory + "/Surfaces/" + prepared.Hash.Substring(0, 16) + "-" + Guid.NewGuid().ToString("N");
            byte[] priorBytes = File.Exists(receiptPath) ? File.ReadAllBytes(receiptPath) : null;
            bool published = false;
            try
            {
                var materials = new Dictionary<string, Material>(defaults, StringComparer.Ordinal);
                if (prepared != null)
                {
                    Directory.CreateDirectory(directory);
                    foreach (string relative in prepared.Files)
                    {
                        string destination = directory + "/source/" + relative;
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        File.Copy(BridgeAssetContract.ResolveRelative(prepared.Directory, relative), destination, false);
                        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                    }
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    BridgeAssetContract.Require(shader != null && shader.isSupported, "URP Lit is required.");
                    foreach (var definition in prepared.Profile.materials)
                    {
                        foreach (var texture in new[] { (definition.baseColorPath, 0), (definition.normalPath, 1), (definition.maskPath, 2) }.Where(t => !string.IsNullOrEmpty(t.Item1)))
                            BridgeAssetImporter.ConfigureTexture(directory + "/source/" + texture.Item1, texture.Item2);
                        var material = BridgeAssetImporter.Material(definition, shader, directory + "/source/");
                        AssetDatabase.CreateAsset(material, directory + "/" + definition.key + ".mat");
                        AssetDatabase.SaveAssetIfDirty(material);
                        materials[definition.key] = material;
                    }
                    BridgeAssetContract.Require(BridgeAssetContract.Fingerprint(prepared.Directory, File.ReadAllText(profilePath), prepared.Files) == prepared.Hash, "Surface source changed during import.");
                }
                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = keys[i].Select(k => materials[k]).ToArray();
                published = true;
                if (prepared == null) DeleteReceipt(receiptPath);
                else
                {
                    File.WriteAllText(receiptPath, JsonConvert.SerializeObject(new Receipt { profileId = prepared.Profile.id, profileHash = prepared.Hash, directory = directory }, Formatting.Indented));
                    AssetDatabase.ImportAsset(receiptPath, ImportAssetOptions.ForceSynchronousImport);
                }
                EditorSceneManager.MarkSceneDirty(bridge.Scene);
                if (!EditorSceneManager.SaveScene(bridge.Scene)) throw new IOException("Could not save bridge material change.");
            }
            catch (Exception error)
            {
                var errors = new List<Exception> { error };
                if (published)
                {
                    WaterGeneration.Attempt(() => { for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = bindings[i]; }, errors);
                    WaterGeneration.Attempt(() => { if (priorBytes == null) DeleteReceipt(receiptPath); else { File.WriteAllBytes(receiptPath, priorBytes); AssetDatabase.ImportAsset(receiptPath); } }, errors);
                    WaterGeneration.Attempt(() => { EditorSceneManager.MarkSceneDirty(bridge.Scene); if (!EditorSceneManager.SaveScene(bridge.Scene)) throw new IOException("Could not save restored material bindings."); }, errors);
                }
                if (errors.Count == 1 && directory != null) WaterGeneration.Attempt(() => DeleteDirectory(directory), errors);
                throw new AggregateException("Surface change failed; original structure remains. Material restoration was attempted.", errors);
            }
            string cleanupWarning = null;
            if (prior != null)
                try { DeleteDirectory(prior.directory); }
                catch (Exception error) { cleanupWarning = error.Message; }
            return new { state = prepared == null ? "reset" : "applied", profile = prepared?.Profile.id, hash = prepared?.Hash, geometryChanges = false, cleanupWarning };
        }

        static string Key(Material material, Dictionary<string, Material> expected)
        {
            BridgeAssetContract.Require(material != null, "A bridge material binding is missing.");
            var pair = expected.FirstOrDefault(p => p.Value == material);
            BridgeAssetContract.Require(pair.Key != null, "The owned material bindings were edited externally; restore them before applying a profile.");
            return pair.Key;
        }
        static Receipt ReadReceipt(string path, string generation)
        {
            if (!File.Exists(path)) return null;
            BridgeAssetContract.Require(new FileInfo(path).Length < 16384, "Invalid material receipt size.");
            var receipt = JsonConvert.DeserializeObject<Receipt>(File.ReadAllText(path));
            BridgeAssetContract.Require(receipt != null && receipt.schemaVersion == 1 && BridgeAssetContract.IsHash(receipt.profileHash), "Invalid material receipt.");
            BridgeAssetContract.ValidateId(receipt.profileId, "profile id");
            string prefix = generation + "/Surfaces/";
            BridgeAssetContract.Require(receipt.directory != null && receipt.directory.StartsWith(prefix, StringComparison.Ordinal), "Material receipt escaped the bridge generation.");
            string leaf = receipt.directory.Substring(prefix.Length);
            BridgeAssetContract.Require(leaf.Length == 49 && leaf[16] == '-' && leaf.Where((c, i) => i != 16).All(c => "0123456789abcdef".Contains(c)) && Directory.Exists(receipt.directory), "Missing or invalid surface generation.");
            return receipt;
        }
        static void DeleteDirectory(string path)
        {
            if ((Directory.Exists(path) || File.Exists(path + ".meta")) && !AssetDatabase.DeleteAsset(path)) throw new IOException("Could not clean obsolete surface: " + path);
        }
        static void DeleteReceipt(string path)
        {
            if ((File.Exists(path) || File.Exists(path + ".meta")) && !AssetDatabase.DeleteAsset(path)) throw new IOException("Could not remove surface receipt.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace Bwork.Authoring.Editor.BridgeAssets
{
    [Serializable] public sealed class BridgePoint
    {
        public float x, y, z;
        [JsonIgnore] public Vector3 Vector => new Vector3(x, y, z);
        public static BridgePoint From(Vector3 p) => new BridgePoint { x = p.x, y = p.y, z = p.z };
    }
    [Serializable] public sealed class BridgePlacement
    {
        public int schemaVersion;
        public string batchId, targetScene;
        public BridgePoint position;
        public float yawDegrees;
    }
    [Serializable] public sealed class BridgeLODSource
    {
        public int level, triangles;
        public string fbxRelativePath;
        public float screenRelativeHeight;
    }
    [Serializable] public sealed class BridgeMaterialSlot { public string slotName, materialKey; }
    [Serializable] public sealed class BridgeMaterialSource
    {
        public string key, baseColorPath, normalPath, maskPath;
        public float tilingMeters = 1, metallic, smoothness = .35f;
        public float[] baseColor;
    }
    [Serializable] public sealed class BridgeSourceCredit { public string name, license, url; }
    [Serializable] public sealed class BridgeManifest
    {
        public int schemaVersion;
        public string id, recipeHash, generatorVersion, units, coordinateSystem, colliderRelativePath, designType;
        public float deckDatum, totalLength, deckWidth, clearCarriageway, archSpan, archRise;
        public BridgePoint boundsMin, boundsMax;
        public BridgeLODSource[] lods;
        public BridgeMaterialSlot[] materialSlots;
        public BridgeMaterialSource[] materials;
        public BridgeSourceCredit[] sources;
    }
    public sealed class BridgePreparedSource
    {
        public BridgeManifest Manifest;
        public BridgePlacement Placement;
        public string Directory, ManifestPath, ManifestJson, SourceHash, PlacementHash;
        public string[] RelativeFiles;
        public long SourceBytes;
    }

    public static class BridgeAssetContract
    {
        public static BridgePreparedSource Prepare(string manifestPath, string placementPath, string batchId)
        {
            ValidateId(batchId, "batchId");
            string path = Path.GetFullPath(manifestPath);
            Require(File.Exists(path) && new FileInfo(path).Length <= 1024 * 1024, "manifestPath must name a manifest no larger than 1 MiB.");
            Require(File.Exists(placementPath) && new FileInfo(placementPath).Length <= 65536, "placementRecipePath must name a placement no larger than 64 KiB.");
            RejectLinks(path);
            string json = File.ReadAllText(path);
            var manifest = Parse<BridgeManifest>(json);
            ValidateManifest(manifest);
            var placement = ReadPlacementJson(File.ReadAllText(placementPath), batchId);
            string directory = Path.GetDirectoryName(path);
            var files = manifest.lods.Select(l => l.fbxRelativePath).Append(manifest.colliderRelativePath)
                .Concat(manifest.materials.SelectMany(m => new[] { m.baseColorPath, m.normalPath, m.maskPath }))
                .Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Require(files.Length <= 32, "Too many source files.");
            long bytes = 0;
            foreach (string relative in files)
            {
                string source = ResolveRelative(directory, relative);
                Require(File.Exists(source), "Missing source file: " + relative);
                RejectLinks(source);
                long length = new FileInfo(source).Length;
                Require(length > 0 && length <= 128L * 1024 * 1024, "Source must contain 1 byte..128 MiB: " + relative);
                bytes += length;
            }
            Require(bytes <= 256L * 1024 * 1024, "Bridge sources exceed 256 MiB.");
            return new BridgePreparedSource
            {
                Manifest = manifest, Placement = placement, Directory = directory, ManifestPath = path, ManifestJson = json,
                RelativeFiles = files, SourceBytes = bytes, SourceHash = Fingerprint(directory, json, files),
                PlacementHash = Hash(JsonConvert.SerializeObject(placement))
            };
        }

        public static BridgePlacement ReadPlacementJson(string json, string batchId)
        {
            var p = Parse<BridgePlacement>(json);
            Require(p != null && p.schemaVersion == 1 && p.batchId == batchId, "Placement schemaVersion/batchId mismatch.");
            ValidateId(p.batchId, "batchId");
            Require(p.position != null && Finite(p.position.Vector) && Mathf.Abs(p.position.x) <= 100000 && Mathf.Abs(p.position.y) <= 10000 && Mathf.Abs(p.position.z) <= 100000 && float.IsFinite(p.yawDegrees), "Finite, bounded placement required.");
            ProjectContext.ValidateAssetPath(p.targetScene, true);
            ResolveRelative(Path.GetFullPath("."), p.targetScene);
            return p;
        }

        static void ValidateManifest(BridgeManifest m)
        {
            Require(m != null && (m.schemaVersion == 1 || m.schemaVersion == 2), "Unsupported bridge manifest schema.");
            string design = m.designType ?? "coastalArch";
            Require(new[] { "coastalArch", "stoneViaduct", "steelThroughTruss", "timberTrestle" }.Contains(design), "Unknown bridge design.");
            Require(m.schemaVersion == 2 || design == "coastalArch", "Non-arch designs require manifest schemaVersion 2.");
            ValidateId(m.id, "id");
            Require(IsHash(m.recipeHash) && !string.IsNullOrWhiteSpace(m.generatorVersion), "Manifest requires a lowercase SHA256 recipeHash and generatorVersion.");
            Require(m.units == "meters" && m.coordinateSystem == "unity-y-up-z-forward" && m.deckDatum == 0, "Only metre assets with Unity axes and top deck y=0 are supported.");
            Require(float.IsFinite(m.totalLength) && m.totalLength >= 20 && m.totalLength <= 300 && float.IsFinite(m.deckWidth) && m.deckWidth >= 3 && m.deckWidth <= 15, "Bridge dimensions are invalid.");
            if (design == "coastalArch") Require(float.IsFinite(m.archSpan) && m.archSpan > 0 && m.archSpan < m.totalLength && float.IsFinite(m.archRise) && m.archRise > 0 && m.archRise < m.archSpan, "Arch dimensions are invalid.");
            Require(float.IsFinite(m.clearCarriageway) && m.clearCarriageway > 0 && m.clearCarriageway <= m.deckWidth, "clearCarriageway is required and must be positive and no wider than the structural deckWidth.");
            Require(m.boundsMin != null && m.boundsMax != null && Finite(m.boundsMin.Vector) && Finite(m.boundsMax.Vector), "Finite evaluated bridge bounds required.");
            Vector3 size = m.boundsMax.Vector - m.boundsMin.Vector;
            Require(size.x >= m.deckWidth - .1f && size.x <= m.deckWidth + 10 && size.y > 1 && size.y < 150 && Mathf.Abs(size.z - m.totalLength) <= 2 && m.boundsMin.y < 0 && m.boundsMax.y > 0, "Bridge bounds do not match its declared dimensions.");
            if (design == "coastalArch") Require(size.y > m.archRise && m.boundsMin.y < -m.archRise, "Arch bounds do not contain the declared rise.");
            Require(m.lods != null && m.lods.Length == 3, "Exactly three LOD files required.");
            float priorHeight = 1; int priorTriangles = int.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                var lod = m.lods[i];
                Require(lod != null && lod.level == i && lod.triangles > 0 && lod.triangles < priorTriangles && lod.triangles <= 1000000 && float.IsFinite(lod.screenRelativeHeight) && lod.screenRelativeHeight > 0 && lod.screenRelativeHeight < priorHeight, "LOD levels, triangle counts and transition heights must descend.");
                Require(Extension(lod.fbxRelativePath, ".fbx"), "LOD source must be FBX.");
                priorTriangles = lod.triangles; priorHeight = lod.screenRelativeHeight;
            }
            Require(m.lods.Select(l => l.fbxRelativePath).Distinct(StringComparer.Ordinal).Count() == 3 && Extension(m.colliderRelativePath, ".fbx") && m.lods.All(l => l.fbxRelativePath != m.colliderRelativePath), "LOD and collision files must be distinct FBXs.");
            Require(m.materials != null && m.materials.Length >= (m.schemaVersion == 1 ? 3 : 1) && m.materials.Length <= 8 && m.materialSlots != null && m.materialSlots.Length == m.materials.Length, "Bridge material count or bindings are invalid.");
            var expected = new Dictionary<string, string> { { "BridgeConcrete", "concrete" }, { "BridgeAsphalt", "asphalt" }, { "BridgeMetal", "metal" } };
            Require(m.materialSlots.All(s => s != null && !string.IsNullOrEmpty(s.slotName) && s.slotName.StartsWith("Bridge", StringComparison.Ordinal) && s.slotName.Length <= 64 && s.slotName.All(char.IsLetterOrDigit) && !string.IsNullOrEmpty(s.materialKey)) && m.materialSlots.Select(s => s.slotName).Distinct().Count() == m.materialSlots.Length && m.materialSlots.Select(s => s.materialKey).Distinct().Count() == m.materialSlots.Length, "Invalid or duplicate bridge material slot bindings.");
            if (m.schemaVersion == 1) Require(expected.All(pair => m.materialSlots.Any(s => s.slotName == pair.Key && s.materialKey == pair.Value)), "Required concrete/asphalt/metal slots are missing.");
            Require(m.materials.All(v => v != null && !string.IsNullOrEmpty(v.key) && v.key.Length <= 64 && v.key.All(char.IsLetterOrDigit)) && m.materials.Select(v => v.key).OrderBy(v => v).SequenceEqual(m.materialSlots.Select(s => s.materialKey).OrderBy(v => v)), "Material definitions and slot keys differ.");
            foreach (var material in m.materials) ValidateMaterial(material);
            Require(m.sources != null && m.sources.Length > 0 && m.sources.Length <= 32 && m.sources.All(s => s != null && !string.IsNullOrWhiteSpace(s.name) && !string.IsNullOrWhiteSpace(s.license)), "Source/license records required.");
        }

        public static void ValidateMaterial(BridgeMaterialSource material)
        {
            Require(material != null && !string.IsNullOrEmpty(material.key) && material.key.Length <= 64 && material.key.All(char.IsLetterOrDigit), "Invalid material key.");
            Require(float.IsFinite(material.tilingMeters) && material.tilingMeters > 0 && material.tilingMeters <= 100 && Unit(material.metallic) && Unit(material.smoothness), "Invalid material scale or surface parameters: " + material.key);
            bool textured = !string.IsNullOrEmpty(material.baseColorPath);
            Require(textured || (material.baseColor != null && material.baseColor.Length == 4 && material.baseColor.All(Unit)), "Material requires a baseColor texture or explicit RGBA: " + material.key);
            Require(material.baseColor == null || material.baseColor.Length == 4 && material.baseColor.All(Unit), "Invalid RGBA tint: " + material.key);
            foreach (string texture in new[] { material.baseColorPath, material.normalPath, material.maskPath }.Where(s => !string.IsNullOrEmpty(s)))
                Require(Extension(texture, ".png"), "Bridge textures must be PNG: " + texture);
        }

        public static string ResolveRelative(string directory, string relative)
        {
            Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\') && !relative.Contains(':') && relative.Split('/').All(s => s.Length > 0 && s != "." && s != ".."), "Source path must stay inside the manifest directory: " + relative);
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            Require(path.StartsWith(root, StringComparison.Ordinal), "Source path escaped manifest directory.");
            return path;
        }
        public static void ValidateId(string value, string field)
        {
            Require(!string.IsNullOrEmpty(value) && value.Length <= 64 && value[0] >= 'a' && value[0] <= 'z' && value.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-'), field + " must be a lowercase letter followed by lowercase letters, digits or hyphens (maximum 64).");
        }
        public static bool IsHash(string value) => value != null && value.Length == 64 && value.All(c => "0123456789abcdef".Contains(c));
        public static string Hash(string text)
        {
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }
        public static string Fingerprint(string directory, string manifestJson, IEnumerable<string> relativeFiles)
        {
            var description = new StringBuilder(manifestJson);
            using (var sha = SHA256.Create())
                foreach (string relative in relativeFiles.OrderBy(p => p, StringComparer.Ordinal))
                    using (var stream = File.OpenRead(ResolveRelative(directory, relative)))
                        description.Append('\n').Append(relative).Append(':').Append(Hex(sha.ComputeHash(stream)));
            return Hash(description.ToString());
        }
        static T Parse<T>(string json)
        {
            try { return JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 32 }); }
            catch (JsonException error) { throw new InvalidDataException("Invalid bridge JSON: " + error.Message, error); }
        }
        internal static void RejectLinks(string path)
        {
            for (string current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Symlinked bridge source paths are not supported: " + current);
        }
        static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        static bool Extension(string path, string extension) => !string.IsNullOrEmpty(path) && string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);
        static bool Unit(float value) => float.IsFinite(value) && value >= 0 && value <= 1;
        public static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        public static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Bwork.FjordCoast.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Bwork.Authoring.Editor.Rocks
{
    [Serializable] public sealed class RockLOD { public string path; public int triangles; public float screenRelativeHeight; }
    [Serializable] public sealed class RockVariant
    {
        public string id, displayName, colliderPath;
        public string materialId = "rock";
        public RockLOD[] lods;
        public float[] boundsSize;
        [JsonIgnore] public Vector3 Size => new Vector3(boundsSize[0], boundsSize[1], boundsSize[2]);
    }
    [Serializable] public sealed class RockMaterialSource
    { public string id, baseColorPath, normalPath, metallicSmoothnessPath; }
    [Serializable] public sealed class RockManifest
    { public int schemaVersion; public string license; public RockVariant[] variants; public RockMaterialSource[] materials; }
    public sealed class RockSource
    { public RockManifest manifest; public string root, json, hash; public string[] files; public long bytes; }
    [Serializable] public sealed class RockSpecies
    { public string id; public float weight = 1, minimumScale = .8f, maximumScale = 1.2f; }
    [Serializable] public sealed class RockArea
    { public float x, z, width, depth; }
    [Serializable] public sealed class RockRecipe
    {
        public int schemaVersion = 1, seed = 41, maximumCount = 180, clusterCount = 0;
        public string id = "rock-scatter", materialProfile = "granite", orientation = "random", sizeDistribution = "uniform";
        public RockArea area;
        public RockSpecies[] species;
        public float densityPerHectare = 15, minimumSpacing = 1, minimumHeight = -1000, maximumHeight = 10000;
        public float minimumSlopeDegrees, maximumSlopeDegrees = 60, clusterRadius = 24;
        public float alignment = .7f, strataYawDegrees = 25, yawVariationDegrees = 15, burialFraction = .12f;
        public float maximumGroundRelief = 5, exclusionPadding = 1;
        public string waterMode = "none";
        public float maximumWaterDistance = 10, minimumWaterDepth = .05f, maximumWaterDepth = 1.2f, maximumBankHeight = 3;
        public bool colliders;
        public SpatialExclusions.Primitive[] exclusions;
    }

    /// <summary>Bounded admission before any scene or asset mutation. Source hashes include every admitted byte.</summary>
    public static class RockContract
    {
        public static readonly string[] Profiles = { "granite", "sandstone", "basalt", "mossy", "wet" };
        public static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        public static void Id(string id) => Require(!string.IsNullOrEmpty(id) && id.Length <= 48 && id.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_'), "Rock IDs require 1..48 lowercase letters, digits, hyphens or underscores.");
        public static string FileAt(string root, string relative)
        {
            ProjectContext.ValidateRelative(relative);
            string path = Path.GetFullPath(Path.Combine(root, relative));
            ProjectContext.RejectLinks(path);
            Require(path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Rock source escaped its configured root.");
            return path;
        }
        public static T Parse<T>(string json, bool strict = true)
        {
            Require(json != null && json.Length <= 1024 * 1024, "Rock JSON exceeds 1 MiB.");
            using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 20 };
            var token = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            return token.ToObject<T>(JsonSerializer.Create(new JsonSerializerSettings { MissingMemberHandling = strict ? MissingMemberHandling.Error : MissingMemberHandling.Ignore }));
        }
        public static RockSource Admit(string root)
        {
            Require(!string.IsNullOrWhiteSpace(root) && Path.IsPathRooted(root), "Configure an explicit rockSourceRoot before authoring rocks.");
            var manifestPath = FileAt(root, "manifest.json");
            Require(File.Exists(manifestPath) && new FileInfo(manifestPath).Length <= 1024 * 1024, "A rock manifest of at most 1 MiB is required.");
            var json = File.ReadAllText(manifestPath); var manifest = Parse<RockManifest>(json, false);
            ValidateManifest(manifest);
            var files = manifest.variants.SelectMany(v => v.lods.Select(l => l.path).Append(v.colliderPath))
                .Concat(manifest.materials.SelectMany(m => new[] { m.baseColorPath, m.normalPath, m.metallicSmoothnessPath }))
                .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            long bytes = 0;
            using var hash = SHA256.Create();
            void Add(byte[] data) => hash.TransformBlock(data, 0, data.Length, data, 0);
            Add(Encoding.UTF8.GetBytes(json));
            foreach (var relative in files)
            {
                var path = FileAt(root, relative);
                Require(File.Exists(path) && new FileInfo(path).Length > 0 && new FileInfo(path).Length <= 24 * 1024 * 1024, "Missing or oversized rock source: " + relative);
                bytes += new FileInfo(path).Length;
                Require(bytes <= 64 * 1024 * 1024, "Rock source bundle exceeds 64 MiB.");
                Add(Encoding.UTF8.GetBytes(relative)); Add(File.ReadAllBytes(path));
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return new RockSource { root = root, json = json, manifest = manifest, files = files, bytes = bytes, hash = BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant() };
        }
        public static void ValidateManifest(RockManifest m)
        {
            Require(m != null && m.schemaVersion == 1 && m.license == "CC0-1.0", "Rock manifest requires schemaVersion=1 and CC0-1.0.");
            Require(m.variants != null && m.variants.Length > 0 && m.variants.Length <= 32 && m.materials != null && m.materials.Length >= 1 && m.materials.Length <= 8, "Require 1..32 variants and 1..8 shared stone material sources.");
            foreach (var v in m.variants)
            {
                Require(v != null, "Null rock variant."); Id(v.id); Id(v.materialId);
                Require(m.materials.Any(material => material != null && material.id == v.materialId), "Unknown rock material source: " + v.materialId);
                Require(v.boundsSize != null && v.boundsSize.Length == 3 && v.boundsSize.All(x => float.IsFinite(x) && x > 0 && x <= 40), "Rock bounds must be finite positive meters, at most 40m per axis.");
                Require(v.lods != null && v.lods.Length == 3, "Every rock requires three LODs.");
                int prior = int.MaxValue; float height = 1;
                foreach (var lod in v.lods)
                {
                    Require(lod != null && lod.triangles > 0 && lod.triangles < prior && lod.triangles <= 20000, "Rock LOD triangle counts must decrease and remain <=20000.");
                    Require(float.IsFinite(lod.screenRelativeHeight) && lod.screenRelativeHeight > 0 && lod.screenRelativeHeight < height, "LOD screen heights must decrease within (0,1).");
                    ProjectContext.ValidateRelative(lod.path); Require(lod.path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase), "Rock LOD requires FBX.");
                    prior = lod.triangles; height = lod.screenRelativeHeight;
                }
                ProjectContext.ValidateRelative(v.colliderPath); Require(v.colliderPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase), "Rock collider requires FBX.");
            }
            Require(m.variants.Select(v => v.id).Distinct().Count() == m.variants.Length, "Duplicate rock variant ID.");
            Require(m.materials.All(material => material != null) && m.materials.Select(material => material.id).Distinct().Count() == m.materials.Length, "Duplicate or null rock material source.");
            foreach (var material in m.materials)
            {
                Require(material != null, "Null rock material."); Id(material.id);
                foreach (var path in new[] { material.baseColorPath, material.normalPath, material.metallicSmoothnessPath })
                { ProjectContext.ValidateRelative(path); Require(new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(path).ToLowerInvariant()), "Rock textures require PNG or JPEG."); }
            }
        }
        public static FjordBulkScatter.Recipe PlannerRecipe(RockRecipe r, RockManifest m)
        {
            Require(r != null && r.schemaVersion == 1 && r.area != null, "Rock recipe requires schemaVersion=1 and area."); Id(r.id);
            Require(Profiles.Contains(r.materialProfile), "Unknown rock material profile.");
            Require(r.orientation == "random" || r.orientation == "strata" || r.orientation == "flow", "orientation must be random, strata or flow.");
            Require(r.sizeDistribution == "uniform" || r.sizeDistribution == "small-biased" || r.sizeDistribution == "large-biased", "Unknown rock size distribution.");
            Require(float.IsFinite(r.alignment) && r.alignment >= 0 && r.alignment <= 1 && float.IsFinite(r.burialFraction) && r.burialFraction >= 0 && r.burialFraction <= .5f, "Alignment is 0..1 and burialFraction 0..0.5.");
            Require(float.IsFinite(r.strataYawDegrees) && Mathf.Abs(r.strataYawDegrees) <= 360 && float.IsFinite(r.yawVariationDegrees) && r.yawVariationDegrees >= 0 && r.yawVariationDegrees <= 180, "Invalid strata angles.");
            Require(float.IsFinite(r.maximumGroundRelief) && r.maximumGroundRelief >= 0 && r.maximumGroundRelief <= 40 && float.IsFinite(r.exclusionPadding) && r.exclusionPadding >= 0 && r.exclusionPadding <= 20, "Invalid contact relief or exclusion padding.");
            Require(r.species != null && r.species.Length > 0 && r.species.Length <= 10, "Require 1..10 weighted rock species.");
            Require(r.waterMode == "none" || r.waterMode == "bank" || r.waterMode == "shallow", "waterMode must be none, bank or shallow.");
            Require(r.orientation != "flow" || r.waterMode != "none", "Flow orientation requires a water mode.");
            Require(float.IsFinite(r.maximumWaterDistance) && r.maximumWaterDistance > 0 && r.maximumWaterDistance <= 100 && float.IsFinite(r.minimumWaterDepth) && float.IsFinite(r.maximumWaterDepth) && r.minimumWaterDepth >= 0 && r.minimumWaterDepth <= r.maximumWaterDepth && r.maximumWaterDepth <= 5 && float.IsFinite(r.maximumBankHeight) && r.maximumBankHeight >= 0 && r.maximumBankHeight <= 20, "Invalid bank distance, depth, or height limits.");
            var species = r.species.Select(s =>
            {
                Require(s != null, "Null rock species.");
                var v = m.variants.SingleOrDefault(v => v.id == s.id); Require(v != null, "Unknown rock species: " + s.id);
                // The full rotation envelope protects exclusion/spacing even when strata are tilted.
                float radius = v.Size.magnitude * .5f + v.Size.y * .5f;
                Require(radius * s.maximumScale + r.exclusionPadding <= 100, "Scaled rock footprint exceeds 100m.");
                return new FjordBulkScatter.Species { id = s.id, prefabKey = s.id, weight = s.weight, radius = radius,
                    minimumScale = s.minimumScale, maximumScale = s.maximumScale };
            }).ToArray();
            if (r.sizeDistribution != "uniform") species = species.SelectMany(s => Enumerable.Range(0, 3).Select(i => new FjordBulkScatter.Species
            {
                id = s.id + "-band" + i, prefabKey = s.prefabKey, radius = s.radius,
                minimumScale = Mathf.Lerp(s.minimumScale, s.maximumScale, i / 3f), maximumScale = Mathf.Lerp(s.minimumScale, s.maximumScale, (i + 1) / 3f),
                weight = s.weight * (r.sizeDistribution == "small-biased" ? 5 - i * 2 : 1 + i * 2)
            })).ToArray();
            return new FjordBulkScatter.Recipe { id = r.id, seed = r.seed, area = new Rect(r.area.x, r.area.z, r.area.width, r.area.depth),
                species = species, maximumCount = r.maximumCount, densityPerHectare = r.densityPerHectare, minimumSpacing = r.minimumSpacing,
                minimumHeight = r.minimumHeight, maximumHeight = r.maximumHeight, minimumSlopeDegrees = r.minimumSlopeDegrees,
                maximumSlopeDegrees = r.maximumSlopeDegrees, clusterCount = r.clusterCount, clusterRadius = r.clusterRadius };
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Bwork.Authoring.WaterSandbox;
using UnityEditor;
using UnityEngine;

namespace Bwork.Authoring.Editor
{
    /// <summary>Artist-controlled classification of finished terrain; moisture is a visual proxy, not hydrology.</summary>
    [Serializable]
    public sealed class TerrainPaintProfile
    {
        public int schemaVersion = 1, seed = 731;
        public string palette = "temperate";
        public float rockSlopeStart = 28, rockSlopeEnd = 52;
        public float exposedHeightStart = 40, exposedHeightEnd = 90;
        public float patchScaleMeters = 32, soilStrength = .55f, gravelStrength = .65f;
        public float bankWidthMeters = 7, bankHeightMeters = 2.2f;
        public bool heightBlend = true;
        public float heightTransition = .16f, bankBreakup = .45f, patchWarpMeters = 8;
    }

    public static class TerrainPresentation
    {
        const string ProfileFile = "terrain-paint-profile.json";
        const string MaskRoot = "Assets/BFjord/TerrainDetail/";
        static readonly string[] MaterialNames = { "Ground", "Dirt", "Gravel", "Rock" };
        static readonly string[] LayerNames = { "Meadow", "Soil", "Talus", "Outcrop" };
        // Keep each scan at its documented physical footprint. In particular,
        // Rocky Terrain is a 90 m aerial outcrop scan; shrinking it to a small
        // material repeat makes its natural slabs read as repeating masonry.
        static readonly float[] TileMeters = { 2, 3, 2.5f, 90 };

        public sealed class Snapshot
        {
            readonly TerrainLayer[] layers;
            readonly string[] layerProperties;
            readonly float[,,] map;
            readonly Material material;
            readonly string materialProperties;
            public Snapshot(Terrain terrain)
            {
                layers = terrain.terrainData.terrainLayers;
                material = terrain.materialTemplate;
                materialProperties = material == null ? null : EditorJsonUtility.ToJson(material);
                // Paint updates these persistent assets in place through CopySerialized.
                // References alone cannot roll that back. Editor serialization copies all
                // native layer properties and texture references without leaking clone objects.
                layerProperties = layers.Select(layer => layer == null ? null : EditorJsonUtility.ToJson(layer)).ToArray();
                map = layers.Length == 0 ? null : terrain.terrainData.GetAlphamaps(0, 0, terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight);
            }
            public void Restore(Terrain terrain)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] == null || layerProperties[i] == null) continue;
                    EditorJsonUtility.FromJsonOverwrite(layerProperties[i], layers[i]);
                    EditorUtility.SetDirty(layers[i]);
                }
                terrain.terrainData.terrainLayers = layers;
                if (material != null && materialProperties != null)
                {
                    EditorJsonUtility.FromJsonOverwrite(materialProperties, material);
                    EditorUtility.SetDirty(material);
                }
                terrain.materialTemplate = material;
                if (map != null) terrain.terrainData.SetAlphamaps(0, 0, map);
                terrain.Flush();
                EditorUtility.SetDirty(terrain.terrainData);
            }
        }

        public static void Validate(TerrainPaintProfile profile)
        {
            if (profile == null || profile.schemaVersion != 1 ||
                !Range(profile.rockSlopeStart, 0, 80) || !Range(profile.rockSlopeEnd, profile.rockSlopeStart + 1, 89) ||
                !Range(profile.exposedHeightStart, -1000, 5000) || !Range(profile.exposedHeightEnd, profile.exposedHeightStart + 1, 6000) ||
                !Range(profile.patchScaleMeters, 4, 256) || !Range(profile.soilStrength, 0, 1) || !Range(profile.gravelStrength, 0, 1) ||
                !Range(profile.bankWidthMeters, .5f, 30) || !Range(profile.bankHeightMeters, .1f, 10) ||
                !Range(profile.heightTransition, .01f, 1) || !Range(profile.bankBreakup, 0, .8f) || !Range(profile.patchWarpMeters, 0, 32))
                throw new ArgumentException("Terrain paint requires a version-1 profile with finite, ordered slope/height bounds and bounded patch/bank scales.");
            TerrainMaterialBank.Validate(profile.palette);
        }

        public static object ApplyProfile(Terrain terrain, string recipePath)
        {
            var profile = string.IsNullOrWhiteSpace(recipePath) ? new TerrainPaintProfile() : JsonUtility.FromJson<TerrainPaintProfile>(File.ReadAllText(recipePath));
            Validate(profile);
            var snapshot = new Snapshot(terrain);
            string path = ToolSandbox.Generated + "/" + ProfileFile;
            string previous = File.Exists(path) ? File.ReadAllText(path) : null;
            try
            {
                Paint(terrain, ConnectedWaterCommand.ActiveField(), profile);
                ToolSandbox.Persist(new TextAsset(JsonUtility.ToJson(profile, true)), ProfileFile);
                ToolSandbox.Save();
            }
            catch { snapshot.Restore(terrain); ToolSandbox.RestoreText(ProfileFile, previous); throw; }
            return new { painted = true, layers = LayerNames, profile };
        }

        public static void Paint(Terrain terrain, ConnectedWaterField water = null, TerrainPaintProfile profile = null)
        {
            if (terrain == null || terrain.terrainData == null) throw new ArgumentNullException(nameof(terrain));
            if (profile == null)
            {
                string path = ToolSandbox.Generated + "/" + ProfileFile;
                profile = File.Exists(path) ? JsonUtility.FromJson<TerrainPaintProfile>(File.ReadAllText(path)) : new TerrainPaintProfile();
            }
            Validate(profile);
            var data = terrain.terrainData;
            var materials = MaterialNames.Select(name => AssetDatabase.LoadAssetAtPath<Material>(ProjectContext.Material(name)) ??
                throw new InvalidOperationException("Missing CC0 terrain surface: " + name)).ToArray();
            if (materials.Any(m => m.GetTexture("_BaseMap") == null || m.GetTexture("_BumpMap") == null))
                throw new InvalidOperationException("Every terrain surface needs scanned base color and normal textures.");
            var oldLayers = data.terrainLayers;
            bool owned = oldLayers.Length == 0 || oldLayers.Length == 2 && oldLayers[0].name == "Loam" && oldLayers[1].name == "Stone" ||
                oldLayers.Length == 4 && oldLayers.Select(l => l == null ? "" : l.name).SequenceEqual(LayerNames);
            if (!owned) throw new InvalidOperationException("Terrain palette is externally authored; restore the toolkit palette before automatic painting.");
            var material = terrain.materialTemplate;
            string materialPath = material == null ? "" : AssetDatabase.GetAssetPath(material);
            if (material == null || material.shader.name != "Universal Render Pipeline/Terrain/Lit" ||
                materialPath != ToolSandbox.Generated + "/Terrain.mat")
                throw new InvalidOperationException("Terrain material is externally authored; restore the toolkit Terrain.mat before automatic painting.");
            var masks = LayerNames.Select(name => AssetDatabase.LoadAssetAtPath<Texture2D>(MaskRoot + name + "_TerrainMask.png") ??
                throw new InvalidOperationException("Missing CC0 Terrain mask: " + name + ". Install the current sample asset catalog.")).ToArray();
            var surfaces = TerrainMaterialBank.Resolve(profile.palette, materials, masks, TileMeters);
            int width = data.alphamapWidth, height = data.alphamapHeight;
            var map = new float[height, width, 4];
            float stepX = 4 / data.size.x, stepZ = 4 / data.size.z;
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1), v = z / (float)(height - 1);
                float y = data.GetInterpolatedHeight(u, v) + terrain.transform.position.y;
                float wx = terrain.transform.position.x + u * data.size.x, wz = terrain.transform.position.z + v * data.size.z;
                float near = (data.GetInterpolatedHeight(Mathf.Clamp01(u-stepX),v) + data.GetInterpolatedHeight(Mathf.Clamp01(u+stepX),v) +
                    data.GetInterpolatedHeight(u,Mathf.Clamp01(v-stepZ)) + data.GetInterpolatedHeight(u,Mathf.Clamp01(v+stepZ))) * .25f + terrain.transform.position.y;
                float warpX = (Mathf.PerlinNoise(wx/53 + 93, wz/53 + profile.seed*.001f)-.5f)*profile.patchWarpMeters;
                float warpZ = (Mathf.PerlinNoise(wx/47 + profile.seed*.002f, wz/47 + 17)-.5f)*profile.patchWarpMeters;
                float noise = Mathf.PerlinNoise((wx+warpX)/profile.patchScaleMeters + profile.seed*.0131f, (wz+warpZ)/profile.patchScaleMeters + profile.seed*.0073f);
                float detail = Mathf.PerlinNoise(wx/3.7f + 37, wz/3.7f + 61);
                float bank = 0;
                if (water != null)
                {
                    var sample = water.SampleForBank(new Vector2(wx, wz), profile.bankWidthMeters);
                    // Both gates matter: a road or hillside above the water must not become a wet bank.
                    float bankWidth = profile.bankWidthMeters * (1-profile.bankBreakup + profile.bankBreakup*detail);
                    bank = (1 - Smooth(0, bankWidth, Mathf.Max(0, sample.Distance))) *
                        (1 - Smooth(.2f, profile.bankHeightMeters, Mathf.Max(0, y - sample.Height)));
                }
                Vector4 weights = Weights(data.GetSteepness(u, v), y, near-y, noise, detail, bank, profile);
                for (int layer = 0; layer < 4; layer++) map[z, x, layer] = weights[layer];
            }
            // Terrain masks carry the scan's AO/height/roughness at exactly the same UVs as
            // its albedo/normal. Do not substitute road metallic/smoothness maps here.
            var layers = new TerrainLayer[4];
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i] = ToolSandbox.Persist(new TerrainLayer { name = LayerNames[i],
                    diffuseTexture = surfaces[i].color, normalMapTexture = surfaces[i].normal,
                    maskMapTexture = surfaces[i].mask, maskMapRemapMin = Vector4.zero,
                    maskMapRemapMax = new Vector4(0, 1, 1, .45f),
                    tileSize = Vector2.one * surfaces[i].tileMeters,
                    normalScale = materials[i].GetFloat("_BumpScale"), metallic = 0, smoothness = i == 3 ? .08f : .04f,
                    // The default DiffuseAlphaChannel source treats opaque JPG alpha as
                    // mirror smoothness, ignoring the layer's smoothness value entirely.
                    smoothnessSource = TerrainLayerSmoothnessSource.ConstantOnly,
                    tileOffset = new Vector2(-terrain.transform.position.x, -terrain.transform.position.z) }, LayerNames[i] + ".terrainlayer");
            }
            data.terrainLayers = layers;
            data.SetAlphamaps(0, 0, map);
            material.SetFloat("_EnableHeightBlend", profile.heightBlend ? 1 : 0);
            material.SetFloat("_HeightTransition", profile.heightTransition);
            if (profile.heightBlend) material.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
            else material.DisableKeyword("_TERRAIN_BLEND_HEIGHT");
            EditorUtility.SetDirty(material);
            terrain.Flush(); EditorUtility.SetDirty(data);
        }

        public static Vector4 Weights(float slope, float height, float concavity, float noise, float detail, float bank, TerrainPaintProfile profile)
        {
            float exposure = Smooth(profile.exposedHeightStart, profile.exposedHeightEnd, height);
            float rock = Smooth(profile.rockSlopeStart, profile.rockSlopeEnd, slope + (noise-.5f)*12) * (.78f + .22f*exposure);
            float talus = Smooth(14, 35, slope) * (1-rock) * (.25f + .75f*Smooth(-.15f,.6f,concavity)) * profile.gravelStrength;
            float soil = Smooth(.42f,.72f,noise + (detail-.5f)*.18f) * profile.soilStrength * (1-rock);
            // Deposits alternate between small stone and soil patches along the bank;
            // the shore is not a uniform band of one material.
            float bankStone = Mathf.Lerp(.35f, .85f, Smooth(.25f,.75f,detail));
            talus = Mathf.Max(talus, bank*bankStone*(1-rock));
            soil = Mathf.Max(soil, bank*(1-bankStone)*(1-rock));
            float grass = Mathf.Max(.015f, 1-rock-talus-soil);
            var weights = new Vector4(grass, soil, talus, rock);
            return weights / (weights.x+weights.y+weights.z+weights.w);
        }
        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a,b,x));
        static bool Range(float value, float min, float max) => float.IsFinite(value) && value >= min && value <= max;
    }
}

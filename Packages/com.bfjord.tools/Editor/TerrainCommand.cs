using System;
using System.IO;
using System.Linq;
using Bwork.FjordCoast.TerrainAuthoring;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace Bwork.Authoring.Editor
{
    public static class TerrainCommand
    {
        static string RecordPath => ToolSandbox.Generated + "/terrain-edit.json";

        [CliCommand("bwork_terrain", "Prepare, apply or restore a masked regional terrain recipe in the sandbox.", MainThreadRequired = true)]
        public static object Run(
            [CliArg("action", "prepare, apply, remove, status, paint")] string action = "prepare",
            [CliArg("recipePath", "Height recipe JSON (or TerrainPaintProfile for paint); omitted uses built-in profile")] string recipePath = "",
            [CliArg("stampShape", "Built-in stamp: ridge, basin or mesa; custom recipes carry their own stamp arrays")] string stampShape = "ridge")
        {
            var terrain = ToolSandbox.RequireTerrain();
            if (action == "paint") return TerrainPresentation.ApplyProfile(terrain, recipePath);
            var recordAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(RecordPath);
            var previous = recordAsset == null ? null : JsonUtility.FromJson<TerrainPatchChange>(recordAsset.text);
            if (action == "status") return new { applied = previous != null, cells = previous?.Count ?? 0 };
            if (action == "remove")
            {
                if (previous == null) return new { state = "absent", cells = 0 };
                var paintSnapshot = new TerrainPresentation.Snapshot(terrain);
                previous.Restore(terrain);
                try { ToolSandbox.PaintTerrain(terrain); }
                catch { previous.Apply(terrain); paintSnapshot.Restore(terrain); throw; }
                if (!AssetDatabase.DeleteAsset(RecordPath)) throw new IOException("Could not remove the restored edit record.");
                ToolSandbox.Save();
                return new { state = "restored", cells = previous.Count };
            }
            if (action != "prepare" && action != "apply") throw new ArgumentException("Unknown terrain action.");
            var recipe = string.IsNullOrEmpty(recipePath) ? Example(stampShape) : JsonConvert.DeserializeObject<TerrainEditRecipe>(
                File.ReadAllText(recipePath), new StringEnumConverter());
            var data = terrain.terrainData;
            int n = data.heightmapResolution;
            var original = previous == null ? data.GetHeights(0, 0, n, n) : previous.RestoredCopy(terrain);
            var metrics = new TerrainPatchMetrics { TerrainOriginX = terrain.transform.position.x,
                TerrainOriginY = terrain.transform.position.y, TerrainOriginZ = terrain.transform.position.z,
                TerrainSizeX = data.size.x, TerrainSizeY = data.size.y, TerrainSizeZ = data.size.z,
                HeightmapResolution = n };
            var mask = new float[n, n];
            var exclusions = SpatialExclusions.Create(ToolSandbox.Root);
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            {
                var point = terrain.transform.position + new Vector3(x * data.size.x / (n - 1), 0, z * data.size.z / (n - 1));
                mask[z, x] = ToolSandbox.Excluded(point, 4) || exclusions.Intersects(point, 4) ? 0 : 1;
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var heights = FjordTerrainPatchEditor.Apply(original, metrics, mask, recipe);
            var change = TerrainPatchChange.Create(terrain, original, heights);
            float maximum = change.Count == 0 ? 0 : Enumerable.Range(0, change.Count).Max(i => Mathf.Abs(change.after[i] - change.before[i]) * data.size.y);
            if (action == "apply")
            {
                var current = data.GetHeights(0, 0, n, n);
                var transition = TerrainPatchChange.Create(terrain, current, heights);
                string priorRecord = File.Exists(RecordPath) ? File.ReadAllText(RecordPath) : null;
                string recipeFile = ToolSandbox.Generated + "/terrain-recipe.json";
                string priorRecipe = File.Exists(recipeFile) ? File.ReadAllText(recipeFile) : null;
                var paintSnapshot = new TerrainPresentation.Snapshot(terrain);
                transition.Apply(terrain);
                try
                {
                    ToolSandbox.PaintTerrain(terrain);
                    ToolSandbox.Persist(new TextAsset(JsonUtility.ToJson(change)), "terrain-edit.json");
                    ToolSandbox.Persist(new TextAsset(JsonConvert.SerializeObject(recipe, Formatting.Indented, new StringEnumConverter())), "terrain-recipe.json");
                    ToolSandbox.Save();
                }
                catch
                {
                    transition.Restore(terrain);
                    paintSnapshot.Restore(terrain);
                    ToolSandbox.RestoreText("terrain-edit.json", priorRecord);
                    ToolSandbox.RestoreText("terrain-recipe.json", priorRecipe);
                    throw;
                }
            }
            return new { state = action == "apply" ? "applied" : "prepared", cells = change.Count,
                maximumHeightChangeMeters = maximum, milliseconds = watch.Elapsed.TotalMilliseconds,
                operations = recipe.Operations.Select(operation => operation.Kind.ToString()).ToArray() };
        }

        public static TerrainEditRecipe Example(string shape = "ridge")
        {
            if (shape != "ridge" && shape != "basin" && shape != "mesa") throw new ArgumentException("Stamp shape must be ridge, basin or mesa.");
            const int resolution = 65;
            var stamp = new float[resolution * resolution];
            for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
            {
                float px = x / 64f * 2 - 1, pz = z / 64f * 2 - 1;
                float spine = px + .18f * Mathf.Sin(pz * 3.8f) - .08f;
                float ridge = Mathf.Exp(-spine * spine * (spine < 0 ? 6 : 19)) * Mathf.Max(0, 1-pz*pz);
                float spurA = Mathf.Exp(-Mathf.Pow(px + .43f + pz*.45f, 2)*25 - Mathf.Pow(pz+.35f,2)*8);
                float spurB = Mathf.Exp(-Mathf.Pow(px - .39f + pz*.6f, 2)*35 - Mathf.Pow(pz-.24f,2)*13);
                float saddle = .25f*Mathf.Exp(-px*px*10-Mathf.Pow(pz+.05f,2)*55);
                float value = Mathf.Clamp01(ridge*.86f + spurA*.27f + spurB*.31f - saddle);
                if (shape == "basin") value = -Mathf.Exp(-(px*px*3+pz*pz*2)) * Mathf.Max(0,1-px*px) * Mathf.Max(0,1-pz*pz);
                if (shape == "mesa") value = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.88f,Mathf.Sqrt(px*px+pz*pz)));
                // The stamp contract stores normalized values and a separate zero plane.
                stamp[z * resolution + x] = shape == "basin" ? .5f + value*.5f : value;
            }
            return new TerrainEditRecipe { PatchEdgeBlendMeters = 12, Operations = new[]
            {
                new TerrainEditOperation { Kind = TerrainEditKind.Stamp, CenterWorldX = 356, CenterWorldZ = 170,
                    SizeX = 165, SizeZ = 220, RotationDegrees = -22, FalloffMeters = 18,
                    Stamp = new TerrainHeightStamp { Width = resolution, Height = resolution, Values = stamp,
                        ZeroValue = shape == "basin" ? .5f : 0, AmplitudeMeters = shape == "basin" ? 32 : 58 } },
                new TerrainEditOperation { Kind = TerrainEditKind.ThermalErosion, CenterWorldX = 356, CenterWorldZ = 170,
                    SizeX = 195, SizeZ = 240, RotationDegrees = -22, FalloffMeters = 22, Iterations = 14,
                    ThermalTalusAngleDegrees = 34, ThermalMaxTransferMeters = 1.4f, ThermalRelaxation = .15f },
                new TerrainEditOperation { Kind = TerrainEditKind.Smooth, CenterWorldX = 356, CenterWorldZ = 170,
                    SizeX = 190, SizeZ = 240, RotationDegrees = -22, FalloffMeters = 22,
                    SmoothRadiusMeters = 3, Strength = .2f, Iterations = 2 },
                new TerrainEditOperation { Kind = TerrainEditKind.Flatten, CenterWorldX = 90, CenterWorldZ = 95,
                    SizeX = 100, SizeZ = 75, FalloffMeters = 20, TargetWorldY = 12, Strength = .8f }
            }};
        }
    }
}

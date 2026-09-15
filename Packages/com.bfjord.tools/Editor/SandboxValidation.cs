using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace Bwork.Authoring.Editor
{
    public static class SandboxValidation
    {
        [CliCommand("bwork_verify", "Rebuild the bundled sandbox samples and verify their combined ownership lifecycle.", MainThreadRequired = true)]
        public static object Run()
        {
            var terrain = ToolSandbox.RequireTerrain();
            var report = new Dictionary<string, object>();
            string output = Path.Combine(ProjectContext.CaptureDirectory("sandbox"), "lifecycle-verification.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            void Record(string key, object value)
            {
                report[key] = value;
                File.WriteAllText(output, JsonConvert.SerializeObject(report, Formatting.Indented));
            }
            float[,] Heights() => terrain.terrainData.GetHeights(0, 0,
                terrain.terrainData.heightmapResolution, terrain.terrainData.heightmapResolution);
            bool[,] Holes() => terrain.terrainData.GetHoles(0, 0,
                terrain.terrainData.holesResolution, terrain.terrainData.holesResolution);
            try
            {
                foreach (string id in new[] { "", "forest", "scrub", "rock" }) FoliageCommand.Run("remove", "", id);
                StructureCommand.Run("remove");
                ConnectedWaterCommand.Run("remove");
                WaterCommand.Run("remove");
                RoadCommand.Run("remove");
                Record("reset", "Removed bundled samples in reverse dependency order; terrain recipe retained.");

                TerrainCommand.Run("remove");
                RoadCommand.Run("apply");
                var protectedBefore = Heights();
                var footprints = SpatialExclusions.Create(ToolSandbox.Root);
                TerrainCommand.Run("apply");
                var protectedAfter = Heights();
                int protectedCount = 0, changedCount = 0, resolution = protectedAfter.GetLength(0);
                for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
                {
                    var point = terrain.transform.position + new Vector3(x * terrain.terrainData.size.x / (resolution - 1),
                        0, z * terrain.terrainData.size.z / (resolution - 1));
                    if (footprints.Intersects(point, 4))
                    { Require(protectedBefore[z, x] == protectedAfter[z, x], "Terrain recipe changed a protected road footprint"); protectedCount++; }
                    if (protectedBefore[z, x] != protectedAfter[z, x]) changedCount++;
                }
                Require(protectedCount > 0 && changedCount > 0, "Terrain protection fixture did not exercise both regions");
                TerrainCommand.Run("remove");
                Equal(protectedBefore, Heights(), "Protected terrain edit did not restore its baseline");
                RoadCommand.Run("remove");
                TerrainCommand.Run("apply");
                Record("terrainFootprints", new { passed = true, protectedCount, changedCount, restoredExactly = true });

                var baseline = Heights();
                int originalLOD = terrain.heightmapMinimumLODSimplification;
                RoadCommand.Run("prepare");
                Equal(baseline, Heights(), "Road prepare changed Terrain");
                var roads = RoadCommand.Run("apply");
                var roadHeights = Heights();
                var oldRoadAssets = Assets("road-edit.json", "meshAssets");
                RoadCommand.Run("apply");
                Equal(roadHeights, Heights(), "Road replacement drifted");
                Gone(oldRoadAssets);
                Require(ToolSandbox.Root.Cast<Transform>().Count(t => t.name == "Roads") == 1, "Road replacement duplicated root");
                RoadCommand.Run("remove");
                Equal(baseline, Heights(), "Road removal did not restore Terrain");
                Require(terrain.heightmapMinimumLODSimplification == originalLOD, "Road removal changed prior Terrain LOD");
                RoadCommand.Run("apply");
                Record("roads", new { passed = true, prepareReadOnly = true, repeatExact = true,
                    removeRestored = true, obsoleteMeshesDeleted = true, report = roads });

                roadHeights = Heights();
                ConnectedWaterCommand.Run("prepare");
                Equal(roadHeights, Heights(), "Water prepare changed Terrain");
                var water = ConnectedWaterCommand.Run("apply");
                var waterHeights = Heights();
                var oldWaterAssets = Assets("connected-water-edit.json", "assets");
                ConnectedWaterCommand.Run("apply");
                Equal(waterHeights, Heights(), "Water replacement drifted");
                Gone(oldWaterAssets);
                Reject(() => WaterCommand.Run("apply"), "Original water accepted overlapping connected ownership");
                ConnectedWaterCommand.Run("remove");
                Equal(roadHeights, Heights(), "Connected water removal did not restore Terrain");
                WaterCommand.Run("apply");
                Reject(() => ConnectedWaterCommand.Run("apply"), "Connected water accepted overlapping original ownership");
                WaterCommand.Run("remove");
                Equal(roadHeights, Heights(), "Original water removal did not restore Terrain");
                ConnectedWaterCommand.Run("apply");
                Record("water", new { passed = true, prepareReadOnly = true, repeatExact = true,
                    removeRestored = true, obsoleteAssetsDeleted = true, reciprocalOwnershipGuard = true, report = water });

                waterHeights = Heights();
                var originalHoles = Holes();
                StructureCommand.Run("prepare");
                Equal(waterHeights, Heights(), "Structure prepare changed Terrain");
                Equal(originalHoles, Holes(), "Structure prepare changed holes");
                var structures = StructureCommand.Run("apply");
                var structureHeights = Heights();
                var structureHoles = Holes();
                StructureCommand.Run("apply");
                Equal(structureHeights, Heights(), "Structure replacement drifted");
                Equal(structureHoles, Holes(), "Structure replacement changed hole coverage");
                StructureCommand.Run("remove");
                Equal(waterHeights, Heights(), "Structure removal did not restore Terrain");
                Equal(originalHoles, Holes(), "Structure removal did not restore holes");
                StructureCommand.Run("apply");
                Record("structures", new { passed = true, prepareReadOnly = true, repeatExact = true,
                    removeRestored = true, holesRestored = true, report = structures });

                var foliage = new Dictionary<string, object>();
                foreach (string id in new[] { "forest", "scrub", "rock" })
                    foliage[id] = FoliageCommand.Run("apply", "Packages/com.bfjord.tools/Samples/" + id + ".json", id);
                var scrub = Batch("scrub"); var rock = Batch("rock");
                string positions = Fingerprint(Batch("forest"));
                var forest = Batch("forest");
                FoliageCommand.Run("prepare", "Packages/com.bfjord.tools/Samples/forest.json", "forest");
                Require(Batch("forest") == forest, "Foliage prepare replaced the root");
                FoliageCommand.Run("apply", "Packages/com.bfjord.tools/Samples/forest.json", "forest");
                Require(Fingerprint(Batch("forest")) == positions, "Foliage replacement drifted");
                Require(Batch("scrub") == scrub && Batch("rock") == rock, "Foliage replaced another batch");
                FoliageCommand.Run("remove", "", "forest");
                Require(Batch("forest") == null && Batch("scrub") == scrub && Batch("rock") == rock,
                    "Foliage removal changed another batch");
                FoliageCommand.Run("apply", "Packages/com.bfjord.tools/Samples/forest.json", "forest");
                Equal(structureHeights, Heights(), "Foliage changed Terrain");
                Equal(structureHoles, Holes(), "Foliage changed holes");
                Record("foliage", new { passed = true, prepareReadOnly = true, repeatExact = true,
                    separateBatchesPreserved = true, report = foliage });
                ToolSandbox.Save();
                Record("result", new { passed = true, seconds = watch.Elapsed.TotalSeconds,
                    scope = "Isolated 512m authoring sandbox; not island or device acceptance." });
                return new { passed = true, evidence = output, seconds = watch.Elapsed.TotalSeconds };
            }
            catch (Exception error)
            {
                Record("result", new { passed = false, seconds = watch.Elapsed.TotalSeconds, error = error.ToString() });
                throw;
            }
        }

        static string[] Assets(string receipt, string field) => JObject.Parse(
            File.ReadAllText(ToolSandbox.Generated + "/" + receipt))[field].Values<string>().ToArray();
        static void Gone(IEnumerable<string> paths)
        { foreach (string path in paths) Require(!File.Exists(path) && !File.Exists(path + ".meta"), "Obsolete asset retained: " + path); }
        static Transform Batch(string id) => ToolSandbox.Root.Find("Bwork Foliage [owned:bwork_foliage:v1:" + id + "]");
        static string Fingerprint(Transform root) => JsonConvert.SerializeObject(root.Cast<Transform>().Select(t =>
            new { t.name, position = Components(t.localPosition), rotation = Components(t.localEulerAngles), scale = Components(t.localScale) }));
        static float[] Components(Vector3 v) => new[] { v.x, v.y, v.z };
        static void Equal<T>(T[,] expected, T[,] actual, string message)
        { Require(expected.Cast<T>().SequenceEqual(actual.Cast<T>()), message); }
        static void Require(bool valid, string message)
        { if (!valid) throw new InvalidOperationException(message); }
        static void Reject(Action operation, string message)
        {
            try { operation(); }
            catch (InvalidOperationException) { return; }
            throw new InvalidOperationException(message);
        }
    }
}

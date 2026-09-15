using System;
using System.Collections.Generic;
using Bwork.Authoring.Editor.Rocks;
using Unity.Pipeline.Commands;

namespace Bwork.Authoring.Editor
{
    /// <summary>Ordered river composition. Each stage retains its own restoration receipt.</summary>
    public static class RiverSceneCommand
    {
        [CliCommand("bwork_river_scene", "Generate connected water with bank boulders and shallow stream stones, or remove their owned outputs.", MainThreadRequired = true)]
        public static object Run(
            [CliArg("action", "apply, status or remove")] string action = "status",
            [CliArg("recipePath", "Connected-water recipe; empty uses the bundled sample")] string recipePath = "")
        {
            ToolSandbox.RequireTerrain();
            if (action == "status") return new
            {
                water = ConnectedWaterCommand.Run("status"),
                bankRocks = RockCommand.Run("status", "", "river-bank"),
                shallowRocks = RockCommand.Run("status", "", "river-shallow")
            };
            if (action != "apply" && action != "remove") throw new ArgumentException("Unknown river-scene action.");
            RockCommand.RequireNoCustomRiverDependencies();
            var completed = new List<string>();
            string stage = "source admission";
            try
            {
                if (action == "apply")
                {
                    // Admit water before publishing asset preparation or removing the prior decoration.
                    ConnectedWaterCommand.Run("prepare", recipePath);
                    RockCommand.PrepareRiverAssets();
                    completed.Add(stage);
                }
                stage = "remove prior river rocks";
                RockCommand.RemoveRiverRocks();
                completed.Add(stage);
                stage = action == "apply" ? "generate water" : "remove water";
                var water = ConnectedWaterCommand.Run(action, recipePath);
                completed.Add(stage);
                object[] rocks = Array.Empty<object>();
                if (action == "apply")
                {
                    stage = "place bank and shallow rocks";
                    rocks = RockCommand.ApplyRiverRocks();
                    completed.Add(stage);
                }
                return new { state = action == "apply" ? "applied" : "removed", completed, water, rocks,
                    transactionScope = "Each stage commits independently; status reports the retained scene." };
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("River composition stopped at '" + stage + "'. Completed stages: " +
                    string.Join(", ", completed) + ". Completed outputs remain owned; inspect bwork_river_scene status before retrying.", error);
            }
        }
    }
}

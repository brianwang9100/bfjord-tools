using System;
using System.Linq;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace Bwork.Authoring.Editor
{
    /// <summary>Appearance-only presets reuse the active water's transaction and restoration receipt.</summary>
    public static class WaterFidelityCommand
    {
        [CliCommand("bwork_water_fidelity","Apply a low, medium or high river flow with breaking ocean foam to installed connected water. Optional surface refresh updates swell bounds and visual swash coverage without carving.",MainThreadRequired=true)]
        public static object Run(
            [CliArg("preset","low, medium or high")]string preset="medium",
            [CliArg("refreshSurface","Refresh visual apron, swell bounds and shading weights without changing terrain or canonical water fields")]bool refreshSurface=false)
        {
            if(preset!="low"&&preset!="medium"&&preset!="high")throw new ArgumentException("Use low, medium or high flow.");
            return ConnectedWaterCommand.Run(refreshSurface?"refresh-surface":"refresh-appearance",ToolSandbox.SamplePath("water-fidelity-"+preset+".json"));
        }

        [CliCommand("bwork_water_fidelity_view","Frame installed river or north ocean beach, optionally capture water at a fixed animation time.",MainThreadRequired=true)]
        public static object View(
            [CliArg("view","river or beach")]string view="beach",
            [CliArg("capture","Optional screenshot basename")]string capture="",
            [CliArg("timeSeconds","Optional fixed animation time, 0–600 seconds")]string timeSeconds="")
        {
            var field=ConnectedWaterCommand.ActiveField();
            if(field==null)throw new InvalidOperationException("Apply connected water before framing its appearance.");
            if(view=="river")SandboxViews.Run("river");
            else if(view=="beach")
            {
                var ocean=field.Recipe.nodes.Single(n=>n.kind=="ocean");
                var camera=ToolSandbox.Root.GetComponentInChildren<Camera>();
                if(camera==null)throw new InvalidOperationException("The sandbox camera is absent.");
                camera.orthographic=false;camera.fieldOfView=53;
                camera.transform.position=ocean.position+new Vector3(ocean.radius.x*.30f,3.4f,-ocean.radius.y+18);
                camera.transform.LookAt(ocean.position+new Vector3(ocean.radius.x*.22f,0,-ocean.radius.y-5));
            }
            else throw new ArgumentException("Use river or beach.");
            return new{view,image=string.IsNullOrEmpty(capture)?null:
                WaterfallCommand.Run("capture","",capture,timeSeconds)};
        }
    }
}

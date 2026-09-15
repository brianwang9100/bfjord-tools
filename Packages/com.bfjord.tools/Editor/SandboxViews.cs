using System;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace Bwork.Authoring.Editor
{
    public static class SandboxViews
    {
        [CliCommand("bwork_view", "Show or capture a repeatable authoring-sample camera bookmark.", MainThreadRequired = true)]
        public static object Run(
            [CliArg("view", "overview, terrain, terrain-ground, junction, road, bridge, tunnel, forest, water, map")] string view = "overview",
            [CliArg("capture", "Optional screenshot basename; empty only moves the camera")] string capture = "")
        {
            var terrain = ToolSandbox.RequireTerrain();
            Vector3 position, target;
            switch (view)
            {
                case "overview": position = new Vector3(635, 440, 735); target = new Vector3(250, 0, 240); break;
                case "terrain": position = new Vector3(235, 83, 345); target = new Vector3(350, 48, 170); break;
                case "terrain-ground": position = OnTerrain(terrain, 222, 230, 2.2f); target = new Vector3(350, 63, 170); break;
                case "junction": position = new Vector3(218, 80, 340); target = new Vector3(256, 26, 256); break;
                case "road":
                    Physics.SyncTransforms();
                    position = OnRoad(225, 255, 2.1f);
                    target = OnRoad(290, 257, 1.6f);
                    break;
                case "bridge": position = new Vector3(153, 33, 385); target = new Vector3(150, 15, 328); break;
                case "tunnel": position = new Vector3(267, 43, 171); target = new Vector3(310, 38, 167); break;
                case "forest": position = new Vector3(508, 130, 32); target = new Vector3(395, 35, 165); break;
                case "water": position = new Vector3(139, 23, 282); target = new Vector3(167, 2, 380); break;
                case "lake": position = new Vector3(110, 9, 250); target = new Vector3(145, 4, 224); break;
                case "river": position = new Vector3(171, 9, 193); target = new Vector3(153, 5.5f, 208); break;
                case "map": position = new Vector3(256, 650, 270); target = new Vector3(256, 0, 270); break;
                default: throw new ArgumentException("Unknown sandbox bookmark.");
            }
            var camera = ToolSandbox.Root.GetComponentInChildren<Camera>();
            camera.transform.position = position;
            camera.orthographic = view == "map";
            camera.orthographicSize = 302;
            camera.fieldOfView = view == "overview" ? 49 : view == "forest" ? 55 : 53;
            if (camera.orthographic) camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            else camera.transform.LookAt(target);
            return new { view, cameraPosition = new[] { position.x, position.y, position.z },
                cameraTarget = new[] { target.x, target.y, target.z },
                eyeAboveTerrainMeters = position.y - OnTerrain(terrain, position.x, position.z, 0).y,
                image = capture.Length == 0 ? null : ToolSandbox.Capture(capture) };
        }

        static Vector3 OnRoad(float x, float z, float eyeHeight)
        {
            var roads = ToolSandbox.Root.Find("Roads");
            if (roads == null) throw new InvalidOperationException("Apply sandbox roads before using the road bookmark.");
            float surface = float.NegativeInfinity;
            // Conformance can cut Terrain below the fitted mesh. Anchor this bookmark to
            // the actual road collider so its eye and look target cannot follow that cut.
            foreach (var collider in roads.GetComponentsInChildren<MeshCollider>())
            {
                if (!collider.enabled || collider.sharedMesh == null) continue;
                var bounds = collider.bounds;
                var ray = new Ray(new Vector3(x, bounds.max.y + 1, z), Vector3.down);
                if (collider.Raycast(ray, out var hit, bounds.size.y + 2)) surface = Mathf.Max(surface, hit.point.y);
            }
            if (float.IsNegativeInfinity(surface))
                throw new InvalidOperationException($"Road bookmark has no owned road surface at ({x}, {z}); choose a point on the current recipe.");
            return new Vector3(x, surface + eyeHeight, z);
        }

        static Vector3 OnTerrain(Terrain terrain, float x, float z, float eyeHeight)
        {
            var point = new Vector3(x, 0, z);
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y + eyeHeight;
            return point;
        }
    }
}

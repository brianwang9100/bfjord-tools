using System;
using System.IO;
using System.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Bwork.Authoring.Editor
{
    public static class ToolSandbox
    {
        public static string Generated => ProjectContext.Current.sandboxGeneratedRoot;
        public static string ScenePath => ProjectContext.Current.sandboxScene;
        public static Transform Root => GameObject.Find("Bwork Authoring Sandbox")?.transform
            ?? throw new InvalidOperationException("Create the authoring sandbox first.");

        public static string SamplePath(string filename) => Path.Combine(
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ToolSandbox).Assembly).resolvedPath, "Samples", filename);

        [CliCommand("bwork_sandbox", "Create, inspect, save or capture the isolated 512m authoring sample.", MainThreadRequired = true)]
        public static object Run(
            [CliArg("action", "create, status, save, capture, refresh-presentation")] string action = "status",
            [CliArg("name", "Capture filename without extension")] string name = "overview")
        {
            if (action == "create") Create();
            if (action == "save") Save();
            if (action == "refresh-presentation") RefreshPresentation();
            if (action == "capture") return new { image = Capture(name) };
            if (action != "create" && action != "status" && action != "save" && action != "refresh-presentation") throw new ArgumentException("Unknown sandbox action.");
            var terrain = RequireTerrain();
            return new { scene = ScenePath, heightmap = terrain.terrainData.heightmapResolution,
                groups = Root.childCount, sceneDirty = SceneManager.GetActiveScene().isDirty };
        }

        public static Terrain RequireTerrain()
        {
            ProjectContext.RequireAllowedScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer ||
                SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open the authoring sandbox alone in Edit Mode.");
            return Root.GetComponentsInChildren<Terrain>(true).Single();
        }

        public static bool Excluded(Vector3 point, float radius)
        {
            // Reserved strip makes protected road space visible in every scatter sample.
            return Mathf.Abs(point.x - 256) < 12 + radius || point.z > 344 - radius;
        }

        public static T Persist<T>(T value, string filename) where T : Object
        {
            if (Path.GetFileName(filename) != filename) throw new ArgumentException("An owned asset filename is required.");
            Directory.CreateDirectory(Generated);
            var path = Generated + "/" + filename;
            if (value is TextAsset text && (filename.EndsWith(".json", StringComparison.Ordinal) || filename.EndsWith(".txt", StringComparison.Ordinal)))
            {
                File.WriteAllText(path, text.text);
                Object.DestroyImmediate(value);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            var previous = AssetDatabase.LoadAssetAtPath<T>(path);
            if (previous == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, previous);
            Object.DestroyImmediate(value);
            EditorUtility.SetDirty(previous);
            return previous;
        }

        public static void RestoreText(string filename, string content)
        {
            if (Path.GetFileName(filename) != filename) throw new ArgumentException("An owned asset filename is required.");
            if (content == null) AssetDatabase.DeleteAsset(Generated + "/" + filename);
            else Persist(new TextAsset(content), filename);
        }

        public static void Save()
        {
            RequireTerrain();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { Generated }))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
            }
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath))
                throw new IOException("Could not save the sandbox scene.");
        }

        static void Create()
        {
            var context = ProjectContext.Current;
            ProjectContext.RequireIdle();
            if (SceneManager.sceneCount == 1 && SceneManager.GetActiveScene().path == ScenePath)
            { RequireTerrain(); InstallPipeline(); return; }
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save existing scene edits before replacing the sandbox.");
            if (File.Exists(ScenePath))
            { EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); RequireTerrain(); InstallPipeline(); return; }
            foreach (var material in new[] { "Ground", "Dirt", "Gravel", "Rock" })
                if (AssetDatabase.LoadAssetAtPath<Material>(ProjectContext.Material(material)) == null)
                    throw new FileNotFoundException("The reviewed CC0 material is missing: " + material, ProjectContext.Material(material));
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Bwork Authoring Sandbox");
            var data = Persist(new TerrainData { name = "Sandbox Terrain", heightmapResolution = 257,
                size = new Vector3(512, 180, 512), alphamapResolution = 256, baseMapResolution = 512 }, "Terrain.asset");
            var heights = new float[257, 257];
            for (int z = 0; z < 257; z++) for (int x = 0; x < 257; x++)
            {
                float wx = x * 2, wz = z * 2;
                float hills = 28 * Mathf.Exp(-((wx - 355) * (wx - 355) / 16000 + (wz - 165) * (wz - 165) / 22000));
                float coast = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(320, 395, wz));
                float y = -3 + coast * (14 + hills + 3 * Mathf.PerlinNoise(wx / 85f + 3, wz / 85f + 7));
                heights[z, x] = (y + 20) / 180;
            }
            data.SetHeights(0, 0, heights);
            var terrain = Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();
            terrain.name = "Tool Terrain";
            terrain.transform.SetParent(root.transform, false);
            terrain.transform.position = new Vector3(0, -20, 0);
            terrain.heightmapPixelError = 2;
            terrain.materialTemplate = Persist(new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")), "Terrain.mat");
            PaintTerrain(terrain);
            var renderer = Persist(ScriptableObject.CreateInstance<UniversalRendererData>(), "Renderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR = false;
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            pipeline.msaaSampleCount = 4;
            pipeline.mainLightShadowmapResolution = 2048;
            pipeline.shadowDistance = 450;
            pipeline.shadowCascadeCount = 2;
            pipeline = Persist(pipeline, "Pipeline.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            var camera = new GameObject("Sandbox Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(root.transform, false);
            camera.transform.position = new Vector3(635, 440, 735);
            camera.transform.LookAt(new Vector3(250, 0, 240));
            camera.fieldOfView = 49;
            camera.nearClipPlane = .2f;
            camera.farClipPlane = 1600;
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            camera.GetUniversalAdditionalCameraData().requiresColorTexture = true;
            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            ConfigureEnvironment(root.transform, sun, camera);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not create sandbox scene.");
            Save();
        }

        static void RefreshPresentation()
        {
            var terrain = RequireTerrain();
            var snapshot = new TerrainPresentation.Snapshot(terrain);
            try
            {
                PaintTerrain(terrain);
                ConfigureEnvironment(Root, Root.GetComponentsInChildren<Light>().Single(l => l.type == LightType.Directional),
                    Root.GetComponentInChildren<Camera>());
                Save();
            }
            catch { snapshot.Restore(terrain); throw; }
        }

        static void ConfigureEnvironment(Transform root, Light sun, Camera camera)
        {
            // Explicit sample setup already owns the project's URP selection. Scanned
            // albedo and physically based lighting must also agree on linear rendering.
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "/Pipeline.asset");
            if (pipeline == null) throw new InvalidOperationException("The sandbox render pipeline is missing.");
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.requiresDepthTexture = true;
            cameraData.requiresColorTexture = true;
            EditorUtility.SetDirty(cameraData);
            var context = ProjectContext.Current;
            var sky = string.IsNullOrEmpty(context.sandboxSkyMaterial) ? null : AssetDatabase.LoadAssetAtPath<Material>(context.sandboxSkyMaterial);
            if (sky == null)
            {
                var shader = Shader.Find("Skybox/Procedural") ?? throw new InvalidOperationException("The procedural sky shader is missing.");
                var fallback = new Material(shader) { name = "Sandbox Daylight Sky" };
                fallback.SetFloat("_SunDisk", 2);
                fallback.EnableKeyword("_SUNDISK_HIGH_QUALITY");
                fallback.SetFloat("_SunSize", .035f);
                fallback.SetFloat("_AtmosphereThickness", 1);
                fallback.SetColor("_SkyTint", new Color(.5f, .5f, .5f));
                fallback.SetColor("_GroundColor", new Color(.36f, .39f, .38f));
                fallback.SetFloat("_Exposure", 1);
                sky = Persist(fallback, "DaylightSky.mat");
            }
            camera.clearFlags = CameraClearFlags.Skybox;
            sun.transform.rotation = Quaternion.Euler(38, -32, 0);
            sun.type = LightType.Directional;
            sun.intensity = 1;
            sun.color = new Color(1, .97f, .92f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.4f, .47f, .54f);
            RenderSettings.ambientEquatorColor = new Color(.25f, .28f, .29f);
            RenderSettings.ambientGroundColor = new Color(.15f, .16f, .14f);
            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 600;
            RenderSettings.fogEndDistance = 1500;
            RenderSettings.fogColor = new Color(.52f, .64f, .7f);
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        public static void PaintTerrain(Terrain terrain) =>
            TerrainPresentation.Paint(terrain, ConnectedWaterCommand.ActiveField());

        static void InstallPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "/Pipeline.asset");
            if (pipeline == null) throw new InvalidOperationException("The sandbox render pipeline is missing.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        public static string Capture(string name)
        {
            RequireTerrain();
            InstallPipeline();
            if (name.Length == 0 || name.Any(c => !char.IsLetterOrDigit(c) && c != '-')) throw new ArgumentException("Simple capture name required.");
            var directory = ProjectContext.CaptureDirectory("sandbox");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name + ".png");
            var camera = Root.GetComponentInChildren<Camera>();
            var target = new RenderTexture(1440, 960, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previous = camera.targetTexture;
            var previousAspect = camera.aspect;
            var previousCulling = camera.overrideSceneCullingMask;
            var active = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                if (!target.Create()) throw new InvalidOperationException("Could not allocate the capture surface.");
                camera.targetTexture = target;
                camera.aspect = target.width / (float)target.height;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(camera.gameObject.scene);
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("The active URP camera does not support capture.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                camera.aspect = previousAspect;
                camera.overrideSceneCullingMask = previousCulling;
                RenderTexture.active = active;
                if (pixels != null) Object.DestroyImmediate(pixels);
                target.Release();
                Object.DestroyImmediate(target);
            }
            return path;
        }
    }
}

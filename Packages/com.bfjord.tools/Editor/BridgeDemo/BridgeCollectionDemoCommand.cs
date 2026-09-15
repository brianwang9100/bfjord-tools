using System;
using System.IO;
using System.Linq;
using Bwork.Authoring.Editor.BridgeAssets;
using Newtonsoft.Json;
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
    /// <summary>Four finite authored bridge sites; structural assets retain their independent ownership.</summary>
    public static class BridgeCollectionDemoCommand
    {
        public const string ScenePath = "Assets/Scenes/BridgeCollection.unity";
        public const string Generated = "Assets/Generated/BridgeCollection";
        const string RootName = "BFjord Bridge Collection";
        const float Extent = 360;
        const float WaterElevation = 1.35f;

        sealed class Site
        {
            public string id, label;
            public float x, deck, length, bottom;
            public int layer;
            public float[] stations;
            public float bearingHalfWidth, bearingHalfLength;
            public Vector3 World(float xx, float y, float z) => new Vector3(x + xx, deck + y, z);
            public string Batch => "collection-" + id;
            public string Placement => Generated + "/" + id + "-placement.json";
        }

        // Footprints match coastal_bridge/structures.py and the accepted coastal-arch recipe.
        static readonly Site[] Sites = {
            new Site { id = "coastal-arch-180", label = "Coastal arch", x = 0, deck = 46, length = 180,
                bottom = -41.625f, layer = 24, stations = new[] { -60f, 60f }, bearingHalfWidth = 5.2f, bearingHalfLength = 3.2f },
            new Site { id = "stone-viaduct-110", label = "Stone viaduct", x = 500, deck = 19.5f, length = 110,
                bottom = -19, layer = 25, stations = new[] { -51f, -30.6f, -10.2f, 10.2f, 30.6f, 51f }, bearingHalfWidth = 3.9f, bearingHalfLength = 1.98f },
            new Site { id = "steel-through-truss-88", label = "Steel through truss", x = 1000, deck = 13, length = 88,
                bottom = -9.7f, layer = 26, stations = new[] { -40f, 40f }, bearingHalfWidth = 4.65f, bearingHalfLength = 1.65f },
            new Site { id = "timber-trestle-80", label = "Timber trestle", x = 1500, deck = 16.9f, length = 80,
                bottom = -16.4f, layer = 27, stations = Enumerable.Range(0, 13).Select(i => -36f + i * 6).ToArray(), bearingHalfWidth = 4.8f, bearingHalfLength = .9f }
        };

        [CliCommand("bwork_bridge_collection", "Set up, inspect or capture four isolated landscaped bridge designs.", MainThreadRequired = true)]
        public static object Run(
            [CliArg("action", "setup, status, capture or refresh-presentation")] string action = "status",
            [CliArg("name", "Design ID plus -hero, -reverse, -side or -riding; coastal-warm for a material comparison")] string name = "stone-viaduct-110-hero")
        {
            ProjectContext.RequireIdle();
            var config = ProjectContext.Current;
            if (!config.additionalAllowedScenes.Contains(ScenePath))
                throw new InvalidOperationException("Opt in to " + ScenePath + " in additionalAllowedScenes before using the collection.");
            if (!new[] { "setup", "status", "capture", "refresh-presentation" }.Contains(action)) throw new ArgumentException("Unknown collection action.");
            if (action == "setup") Setup();
            var root = RequireScene();
            if (action == "refresh-presentation") RefreshPresentation(root);
            if (action == "capture") return new { scene = ScenePath, name, image = Capture(root, name) };
            return new {
                scene = ScenePath, sceneDirty = SceneManager.GetActiveScene().isDirty,
                cameras = root.GetComponentsInChildren<Camera>(true).Select(c => c.name).Concat(new[] { "coastal-warm" }).ToArray(),
                sites = Sites.Select(s => new {
                    s.id, s.label, batchId = s.Batch, manifest = ProjectContext.BridgeSource(s.id + "/manifest.json"),
                    placement = s.Placement, deckElevation = s.deck, bearingBottom = s.deck + s.bottom,
                    bridge = BridgeAssetCommand.Run("status", batchId: s.Batch)
                }).ToArray(),
                terrainCount = root.GetComponentsInChildren<Terrain>().Length,
                note = "Finite Editor presentation sites, 500 m apart. Status bridge receipts report source triangles for all three LODs; no physics, device or sustained-performance claim. Coastal-warm temporarily applies the independent warm profile and restores default surfaces."
            };
        }

        static Transform RequireScene()
        {
            var scene = ProjectContext.RequireAllowedScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Run bwork_bridge_collection action=setup to open the collection.");
            return scene.GetRootGameObjects().Single(g => g.name == RootName).transform;
        }

        static void Setup()
        {
            ProjectContext.RequireSavedScenes();
            Preflight();
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                RequireScene();
                InstallPipeline();
                ApplyBridges();
                return;
            }
            if (Directory.Exists(Generated) && Directory.EnumerateFileSystemEntries(Generated).Any())
                throw new InvalidOperationException("Collection outputs already exist without the owned scene; preserve or inspect them before retrying setup.");
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Group(null, RootName);
            Lighting(root);
            var layers = GroundLayers();
            var water = WaterMaterial();
            foreach (var site in Sites) CreateSite(root, site, layers, water);
            foreach (var site in Sites)
            {
                File.WriteAllText(site.Placement, JsonConvert.SerializeObject(new {
                    schemaVersion = 1, batchId = site.Batch, position = new { x = site.x, y = site.deck, z = 0 },
                    yawDegrees = 0, targetScene = ScenePath
                }, Formatting.Indented) + "\n");
                AssetDatabase.ImportAsset(site.Placement, ImportAssetOptions.ForceSynchronousImport);
            }
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save the collection landscape.");
            ApplyBridges();
        }

        static void Preflight()
        {
            ProjectContext.RejectLinks(Path.Combine(ProjectContext.ProjectRoot, Generated));
            var config = ProjectContext.Current;
            foreach (var path in new[] { config.sandboxGeneratedRoot, config.bridgeDemoGeneratedRoot,
                config.bridgeAssetGeneratedRoot, config.materialRoot, config.natureRoot, config.matureFirRoot })
                if (path == Generated || path.StartsWith(Generated + "/", StringComparison.OrdinalIgnoreCase) ||
                    Generated.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Collection outputs must not overlap another configured asset root: " + path);
            foreach (var site in Sites)
                if (!File.Exists(ProjectContext.BridgeSource(site.id + "/manifest.json"))) throw new FileNotFoundException("Missing prepared bridge: " + site.id);
            foreach (var path in new[] { ProjectContext.Material("Asphalt"), ProjectContext.Material("Gravel"),
                config.materialRoot + "/Textures/Ground_BaseMap.jpg", config.materialRoot + "/Textures/Gravel_BaseMap.jpg",
                config.natureRoot + "/Prefabs/rock_moss_set_01_rock01.prefab", config.natureRoot + "/Prefabs/shrub_03_a.prefab" })
                if (AssetDatabase.LoadMainAssetAtPath(path) == null) throw new FileNotFoundException("Install the configured CC0 presentation asset before setup.", path);
            foreach (var suffix in new[] { "Color", "NormalGL" })
                if (!File.Exists(ProjectContext.BridgeSource("DemoMaterials/Rock030_2K-JPG_" + suffix + ".jpg")))
                    throw new FileNotFoundException("The bridge source bundle requires the CC0 Rock030 presentation maps.");
        }

        static void RefreshPresentation(Transform root)
        {
            InstallPipeline();
            ConfigureEnvironment(root);
            var water = WaterMaterial();
            foreach (var site in Sites)
            {
                var parent = root.Find(site.id + " landscape");
                if (parent == null) throw new InvalidOperationException("The owned site landscape is missing: " + site.id);
                var terrain = parent.GetComponentInChildren<Terrain>();
                if (terrain == null || terrain.terrainData == null ||
                    AssetDatabase.GetAssetPath(terrain.terrainData) != Generated + "/" + site.id + "-Terrain.asset")
                    throw new InvalidOperationException("The owned terrain binding changed: " + site.id);
                ShapeTerrain(terrain.terrainData, site);
                EditorUtility.SetDirty(terrain.terrainData);
                var oldWater = parent.Find("Quiet river water");
                if (oldWater != null) Object.DestroyImmediate(oldWater.gameObject);
                Strip(parent, site, "Quiet river water", 2000, -1000, 1000, WaterElevation - site.deck, water);
                // Keep the existing deterministic dressing, but settle it onto the refreshed surface.
                foreach (Transform child in parent.Cast<Transform>().ToArray())
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)) continue;
                    float x = child.position.x - site.x, z = child.position.z;
                    float elevation = Ground(site, x, z);
                    if (elevation < WaterElevation + .4f) { Object.DestroyImmediate(child.gameObject); continue; }
                    bool rock = child.name.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0;
                    child.position = new Vector3(child.position.x, elevation - (rock ? child.localScale.y * .55f : .06f), z);
                }
                SetLayer(parent, site.layer);
            }
            DynamicGI.UpdateEnvironment();
            Save();
        }

        static void ApplyBridges()
        {
            foreach (var site in Sites)
            {
                BridgeAssetCommand.Run("apply", ProjectContext.BridgeSource(site.id + "/manifest.json"), site.Placement, site.Batch);
                var bridge = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "Bwork Bridge Asset [batch:" + site.Batch + "]");
                SetLayer(bridge.transform, site.layer);
            }
            Save();
        }

        static void InstallPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "/Pipeline.asset");
            if (pipeline == null) throw new InvalidOperationException("The collection pipeline asset is missing.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        static void Lighting(Transform root)
        {
            var renderer = Persist(ScriptableObject.CreateInstance<UniversalRendererData>(), "Renderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR = true; pipeline.msaaSampleCount = 4;
            pipeline.mainLightShadowmapResolution = 2048; pipeline.shadowDistance = 250; pipeline.shadowCascadeCount = 2;
            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Persist(pipeline, "Pipeline.asset");
            InstallPipeline();
            ConfigureEnvironment(root);
        }

        static void ConfigureEnvironment(Transform root)
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var sunTransform = root.Find("Soft late afternoon sun");
            if (sunTransform == null) sunTransform = Group(root, "Soft late afternoon sun");
            var sun = sunTransform.GetComponent<Light>();
            if (sun == null) sun = sunTransform.gameObject.AddComponent<Light>();
            sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(35, 58, 0);
            sun.color = new Color(1, .93f, .84f); sun.intensity = 1.65f; sun.shadows = LightShadows.Soft;
            sun.shadowBias = .025f; sun.shadowNormalBias = .25f; RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.4f, .49f, .59f);
            RenderSettings.ambientEquatorColor = new Color(.29f, .33f, .34f);
            RenderSettings.ambientGroundColor = new Color(.14f, .15f, .13f);
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetColor("_SkyTint", new Color(.50f, .56f, .62f));
            sky.SetColor("_GroundColor", new Color(.25f, .29f, .32f));
            sky.SetFloat("_SunSize", .025f);
            sky.SetFloat("_Exposure", 1.05f); sky.SetFloat("_AtmosphereThickness", .8f);
            RenderSettings.skybox = Persist(sky, "Sky.mat");
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.55f, .64f, .70f); RenderSettings.fogDensity = .0011f;
        }

        static TerrainLayer[] GroundLayers()
        {
            foreach (var suffix in new[] { "Color", "NormalGL" })
            {
                string file = "Rock030_2K-JPG_" + suffix + ".jpg", target = Generated + "/" + file;
                File.Copy(ProjectContext.BridgeSource("DemoMaterials/" + file), target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(target);
                importer.textureType = suffix == "NormalGL" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = suffix == "Color"; importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = 2048; importer.anisoLevel = 4; importer.SaveAndReimport();
            }
            var rock = Persist(new TerrainLayer {
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Generated + "/Rock030_2K-JPG_Color.jpg"),
                normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Generated + "/Rock030_2K-JPG_NormalGL.jpg"),
                tileSize = Vector2.one * 7, normalScale = .5f, smoothness = .08f,
                smoothnessSource = TerrainLayerSmoothnessSource.ConstantOnly
            }, "CC0-bedrock.terrainlayer");
            return new[] { rock, LayerFromTexture("Gravel", 3), LayerFromTexture("Ground", 4) };
        }

        static TerrainLayer LayerFromTexture(string name, float tile)
        {
            string root = ProjectContext.Current.materialRoot + "/Textures/" + name;
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>(root + "_BaseMap.jpg");
            if (color == null) throw new FileNotFoundException("The configured ground texture is missing: " + root);
            return Persist(new TerrainLayer { diffuseTexture = color,
                normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(root + "_Normal.jpg"), tileSize = Vector2.one * tile,
                normalScale = .45f, smoothness = .08f,
                smoothnessSource = TerrainLayerSmoothnessSource.ConstantOnly }, name + ".terrainlayer");
        }

        static float Mask(float x, float z, float w, float d) =>
            (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(w + .7f, w + 6, Mathf.Abs(x)))) *
            (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(d + .7f, d + 6, Mathf.Abs(z))));

        static float Ground(Site site, float x, float z)
        {
            float end = site.length * .5f, az = Mathf.Abs(z);
            // Broaden and bend the shore away from the approach; avoid a straight extruded cliff wall.
            float away = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(10, 65, Mathf.Abs(x)));
            float bend = away * (7 * Mathf.Sin(x * .027f) + 5 * Mathf.Sin(x * .059f + 1.2f));
            float bank = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(end - 16 + bend, end + 5 + away * 26 + bend, az));
            float hill = 7 * Mathf.PerlinNoise(x * .016f + 17, z * .018f + 23) + 2 * Mathf.Sin(x * .04f);
            float y = Mathf.Lerp(-3, site.deck * (1 - away * .14f) + hill, bank);
            // Exact bearing footprints plus a 0.7 m sampling margin: footings enter ground by 0.25 m.
            foreach (float station in site.stations)
                y = Mathf.Max(y, Mathf.Lerp(-3, site.deck + site.bottom + .25f,
                    Mask(x, z - station, site.bearingHalfWidth, site.bearingHalfLength)));
            if (site.id == "coastal-arch-180")
                foreach (float station in new[] { -72.5f, 72.5f })
                    y = Mathf.Max(y, Mathf.Lerp(-3, site.deck - 20.25f + .3f, Mask(x, z - station, 4.8f, 2.4f)));
            // The entire road corridor is capped below deck. Approaches meet the exact deck datum.
            float corridor = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.6f, 10, Mathf.Abs(x)));
            float road = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(end - 2, end, az));
            y = Mathf.Lerp(y, site.deck - .18f, corridor * road);
            y = Mathf.Lerp(y, Mathf.Min(y, site.deck - .22f), corridor * (1 - road));
            return y;
        }

        static void CreateSite(Transform root, Site site, TerrainLayer[] layers, Material water)
        {
            var parent = Group(root, site.id + " landscape");
            const int resolution = 513, alpha = 256;
            const float bottom = -8, height = 100;
            var data = new TerrainData { heightmapResolution = resolution, alphamapResolution = alpha,
                baseMapResolution = 1024, size = new Vector3(Extent, height, Extent), terrainLayers = layers };
            ShapeTerrain(data, site);
            data = Persist(data, site.id + "-Terrain.asset");
            var terrain = Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();
            terrain.name = "CC0 textured ravine and planted banks"; terrain.transform.SetParent(parent, false);
            terrain.transform.position = new Vector3(site.x - Extent / 2, bottom, -Extent / 2);
            terrain.materialTemplate = Persist(new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")), site.id + "-Terrain.mat");
            terrain.heightmapPixelError = 2; terrain.basemapDistance = 500;
            Strip(parent, site, "Quiet river water", 2000, -1000, 1000, WaterElevation - site.deck, water);
            var asphalt = AssetDatabase.LoadAssetAtPath<Material>(ProjectContext.Material("Asphalt"));
            var gravel = AssetDatabase.LoadAssetAtPath<Material>(ProjectContext.Material("Gravel"));
            var white = Solid("Road-white", new Color(.84f, .83f, .76f), .1f);
            var amber = Solid("Road-amber", new Color(.82f, .53f, .15f), .1f);
            foreach (int sign in new[] { -1, 1 })
            {
                float a = site.length / 2, b = a + 65, start = sign < 0 ? -b : a, end = sign < 0 ? -a : b;
                Strip(parent, site, "Gravel approach " + sign, 9, start, end, -.09f, gravel);
                Strip(parent, site, "Asphalt approach " + sign, 7, start, end, 0, asphalt);
                foreach (float xx in new[] { -2.8f, 2.8f }) Strip(parent, site, "Shoulder paint " + sign + " " + xx, .1f, start, end, .009f, white, xx);
                foreach (float xx in new[] { -.09f, .09f }) Strip(parent, site, "Centre paint " + sign + " " + xx, .09f, start, end, .01f, amber, xx);
            }
            Scatter(parent, site);
            SetLayer(parent, site.layer);
            float scale = site.length / 110;
            CameraAt(root, site, "hero", new Vector3(-100, 22, -70) * scale, new Vector3(0, site.bottom * .4f, 0), 49);
            CameraAt(root, site, "reverse", new Vector3(105, 15, 72) * scale, new Vector3(0, site.bottom * .35f, 0), 49);
            CameraAt(root, site, "side", new Vector3(-120, 4, 0) * scale, new Vector3(0, site.bottom * .4f, 0), 49);
            CameraAt(root, site, "riding", new Vector3(-1.5f, 1.65f, -site.length / 2 - 12), new Vector3(-1.5f, 1.65f, 24), 65);
        }

        static void ShapeTerrain(TerrainData data, Site site)
        {
            int resolution = data.heightmapResolution, alpha = data.alphamapResolution;
            const float bottom = -8, height = 100;
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
                heights[z, x] = (Ground(site, x * Extent / (resolution - 1) - Extent / 2, z * Extent / (resolution - 1) - Extent / 2) - bottom) / height;
            // One finite authoring update per site; native Terrain owns its LODs.
            // https://docs.unity3d.com/ScriptReference/TerrainData.SetHeights.html
            data.SetHeights(0, 0, heights);
            var map = new float[alpha, alpha, 3];
            for (int z = 0; z < alpha; z++) for (int x = 0; x < alpha; x++)
            {
                float nx = x / (float)(alpha - 1), nz = z / (float)(alpha - 1);
                float rock = Mathf.Lerp(.18f, .96f, Mathf.InverseLerp(10, 43, data.GetSteepness(nx, nz)));
                float grass = (1 - rock) * Mathf.InverseLerp(2, site.deck + 3, data.GetInterpolatedHeight(nx, nz) + bottom) * .83f;
                map[z, x, 0] = rock; map[z, x, 1] = 1 - rock - grass; map[z, x, 2] = grass;
            }
            data.SetAlphamaps(0, 0, map);
        }

        static void Scatter(Transform parent, Site site)
        {
            var random = new System.Random(1040915 + site.layer);
            var nature = ProjectContext.Current.natureRoot;
            var rock = AssetDatabase.LoadAssetAtPath<GameObject>(nature + "/Prefabs/rock_moss_set_01_rock01.prefab");
            var shrub = AssetDatabase.LoadAssetAtPath<GameObject>(nature + "/Prefabs/shrub_03_a.prefab");
            for (int i = 0; i < 180; i++)
            {
                float x = (float)(random.NextDouble() * 280 - 140), z = (float)(random.NextDouble() * 310 - 155);
                float y = Ground(site, x, z), scale = i % 3 == 0 ? 1.8f + (float)random.NextDouble() * 3 : .75f + (float)random.NextDouble() * .75f;
                if (y < WaterElevation + .4f || Mathf.Abs(x) < 8 + scale * 2 || (Mathf.Abs(z) < site.length / 2 + 5 && Mathf.Abs(x) < 15)) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(i % 3 == 0 ? rock : shrub);
                instance.transform.SetParent(parent, false); instance.transform.localScale = Vector3.one * scale;
                instance.transform.position = new Vector3(site.x + x, y - (i % 3 == 0 ? scale * .55f : .06f), z);
                instance.transform.rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
            }
        }

        static Material WaterMaterial()
        {
            const int size = 512;
            var normal = new Texture2D(size, size, TextureFormat.RGB24, true, true) { wrapMode = TextureWrapMode.Repeat };
            var modes = new[] { new Vector3(2, 1, .016f), new Vector3(5, 2, .010f), new Vector3(9, 3, .006f),
                new Vector3(17, 7, .004f), new Vector3(31, 13, .002f), new Vector3(-3, 5, .007f), new Vector3(-7, 11, .003f) };
            for (int z = 0; z < size; z++) for (int x = 0; x < size; x++)
            {
                float u = x * Mathf.PI * 2 / size, v = z * Mathf.PI * 2 / size, dx = 0, dy = 0;
                for (int i = 0; i < modes.Length; i++)
                {
                    var mode = modes[i];
                    float slope = mode.z * Mathf.Cos(u * mode.x + v * mode.y + i * 1.719f);
                    float magnitude = Mathf.Sqrt(mode.x * mode.x + mode.y * mode.y);
                    dx += slope * mode.x / magnitude; dy += slope * mode.y / magnitude;
                }
                var n = new Vector3(dx, dy, 1).normalized;
                normal.SetPixel(x, z, new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f));
            }
            normal.Apply(); string path = Generated + "/WaterNormal.png";
            File.WriteAllBytes(path, normal.EncodeToPNG()); Object.DestroyImmediate(normal);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            // This private copy uses URP Lit's packed normal sampler. Canonical Water's linear RGB maps are untouched.
            importer.textureType = TextureImporterType.NormalMap; importer.sRGBTexture = false;
            importer.convertToNormalmap = false; importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4; importer.SaveAndReimport();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(.06f, .16f, .23f)); mat.SetFloat("_Smoothness", .90f); mat.SetFloat("_Metallic", .06f);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path)); mat.SetFloat("_BumpScale", .30f);
            mat.EnableKeyword("_NORMALMAP"); mat.SetTextureScale("_BaseMap", Vector2.one / 64);
            return Persist(mat, "Quiet-water.mat");
        }

        static void Strip(Transform parent, Site site, string name, float width, float start, float end, float y, Material mat, float x = 0)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(x - width / 2, y, start), new Vector3(x - width / 2, y, end),
                new Vector3(x + width / 2, y, end), new Vector3(x + width / 2, y, start) };
            mesh.uv = new[] { new Vector2(0, start), new Vector2(0, end), new Vector2(width, end), new Vector2(width, start) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            mesh = Persist(mesh, site.id + "-" + name.Replace(' ', '_') + ".asset");
            var obj = Group(parent, name); obj.position = site.World(0, 0, 0);
            obj.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.gameObject.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void CameraAt(Transform root, Site site, string suffix, Vector3 position, Vector3 target, float fov)
        {
            var camera = Group(root, site.id + "-" + suffix).gameObject.AddComponent<Camera>();
            camera.transform.position = site.World(position.x, position.y, position.z);
            camera.transform.LookAt(site.World(target.x, target.y, target.z));
            camera.fieldOfView = fov; camera.nearClipPlane = .1f; camera.farClipPlane = 900;
            camera.allowHDR = true; camera.allowMSAA = true; camera.clearFlags = CameraClearFlags.Skybox;
            camera.cullingMask = 1 << site.layer; camera.enabled = site.layer == 25 && suffix == "hero";
            if (camera.enabled) camera.tag = "MainCamera";
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        }

        static string Capture(Transform root, string name)
        {
            InstallPipeline();
            bool warm = name == "coastal-warm";
            var camera = root.GetComponentsInChildren<Camera>(true).SingleOrDefault(c => c.name == (warm ? "coastal-arch-180-hero" : name));
            if (camera == null) throw new ArgumentException("Unknown collection camera.");
            string directory = ProjectContext.CaptureDirectory("bridge-collection"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + ".png"); ProjectContext.RejectLinks(path);
            var coastal = Sites[0];
            if (warm)
            {
                var status = Newtonsoft.Json.Linq.JObject.FromObject(BridgeSurfaceCommand.Run("status", coastal.Batch));
                if ((string)status["state"] != "default") throw new InvalidOperationException("Warm comparison requires default coastal surfaces so the existing profile is preserved.");
                BridgeSurfaceCommand.Run("apply", coastal.Batch, ProjectContext.BridgeSource("material-profiles/warm-concrete/profile.json"));
            }
            var target = new RenderTexture(1440, 960, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previous = camera.targetTexture; var aspect = camera.aspect; var culling = camera.overrideSceneCullingMask;
            var active = RenderTexture.active; Texture2D pixels = null;
            try
            {
                if (!target.Create()) throw new InvalidOperationException("Could not allocate capture target.");
                camera.targetTexture = target; camera.aspect = 1.5f;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(camera.gameObject.scene);
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("URP camera capture is unavailable.");
                RenderPipeline.SubmitRenderRequest(camera, request); RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target; pixels = new Texture2D(1440, 960, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1440, 960), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; camera.aspect = aspect; camera.overrideSceneCullingMask = culling;
                RenderTexture.active = active; if (pixels != null) Object.DestroyImmediate(pixels);
                target.Release(); Object.DestroyImmediate(target);
                if (warm) BridgeSurfaceCommand.Run("reset", coastal.Batch);
            }
            return path;
        }

        static Material Solid(string name, Color color, float smoothness)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", smoothness); return Persist(mat, name + ".mat");
        }
        static Transform Group(Transform parent, string name)
        {
            var result = new GameObject(name).transform; result.SetParent(parent, false); return result;
        }
        static void SetLayer(Transform root, int layer)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
        }
        static T Persist<T>(T value, string name) where T : Object
        {
            string path = Generated + "/" + name; var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); Object.DestroyImmediate(value); EditorUtility.SetDirty(existing); return existing;
        }
        static void Save()
        {
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath)) throw new IOException("Could not save the collection.");
        }
    }
}

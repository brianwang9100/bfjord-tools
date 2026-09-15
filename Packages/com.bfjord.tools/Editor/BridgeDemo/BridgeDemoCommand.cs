using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    /// <summary>Finite presentation fixture. Bridge models are owned by bwork_bridge_asset.</summary>
    public static class BridgeDemoCommand
    {
        public static string ScenePath => ProjectContext.Current.bridgeDemoScene;
        public static string Generated => ProjectContext.Current.bridgeDemoGeneratedRoot;
        const string RootName = "Bwork Coastal Bridge Demo";
        static string Art => ProjectContext.Current.materialRoot + "/";
        static string Nature => ProjectContext.Current.natureRoot + "/";

        sealed class Site
        {
            public string id;
            public Vector3 origin;
            public float yaw, length, span, rise, width, springBottom, pierBottom, pierStation;
            public Quaternion Rotation => Quaternion.Euler(0, yaw, 0);
            public Vector3 World(Vector3 local) => origin + Rotation * local;
        }

        static readonly Site[] Sites = {
            new Site { id = "primary", origin = new Vector3(0, 46, 0), length = 180, span = 120, rise = 35, width = 7,
                springBottom = -41.625f, pierBottom = -20.25f, pierStation = 72.5f },
            new Site { id = "compact", origin = new Vector3(250, 30, 0), yaw = 12, length = 104, span = 64, rise = 20, width = 6,
                springBottom = -25.97f, pierBottom = -13.65f, pierStation = 40 }
        };

        [Serializable] sealed class DependencyManifest { public int schemaVersion = 0; public DependencyFile[] files = null; }
        [Serializable] sealed class DependencyFile { public string path = null; public string sha256 = null; }

        [CliCommand("bwork_bridge_demo", "Set up and capture the isolated coastal arch bridge presentation scene.", MainThreadRequired = true)]
        public static object Run(
            [CliArg("action", "setup, status, capture-config, capture, refresh-approaches, refresh-ground, refresh-presentation")] string action = "status",
            [CliArg("name", "Named camera, for example primary-hero or primary-riding")] string name = "primary-hero")
        {
            GuardProbe();
            if (action == "setup") Setup();
            else if (action != "status" && action != "capture-config" && action != "capture" && action != "refresh-approaches" && action != "refresh-ground" && action != "refresh-presentation")
                throw new ArgumentException("Unknown bridge demo action.");
            var root = RequireScene();
            if (action == "refresh-presentation") RefreshPresentation(root);
            if (action == "refresh-ground") RefreshGround(root);
            if (action == "refresh-approaches")
            {
                foreach (var site in Sites)
                {
                    var parent = root.Find(site.id + " coast and approaches");
                    PaintApproaches(parent, site);
                    ShareBridgeMaterials(parent, site);
                }
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath)) throw new IOException("Could not save approach paint.");
            }
            if (action == "capture") return new { scene = ScenePath, name, image = Capture(root, name) };
            var cameras = root.GetComponentsInChildren<Camera>(true).Select(c => new {
                name = c.name, position = Point(c.transform.position), rotation = Point(c.transform.eulerAngles),
                fieldOfView = c.fieldOfView, width = 1440, height = 960
            }).ToArray();
            return new {
                scene = ScenePath, sceneDirty = SceneManager.GetActiveScene().isDirty, sceneryRoot = RootName,
                cameras, bridgeRoots = SceneManager.GetActiveScene().GetRootGameObjects()
                    .Where(g => g.name.StartsWith("Bwork Bridge Asset [batch:", StringComparison.Ordinal)).Select(g => g.name).ToArray(),
                placements = Sites.Select(s => new { id = s.id, position = Point(s.origin), yawDegrees = s.yaw, deckTop = s.origin.y }).ToArray(),
                terrainCount = root.GetComponentsInChildren<Terrain>().Length,
                rendererCount = root.GetComponentsInChildren<Renderer>().Length,
                note = "Editor presentation only; no device performance acceptance or route movement ownership."
            };
        }

        static object Point(Vector3 value) => new { x = value.x, y = value.y, z = value.z };

        static void RefreshPresentation(Transform root)
        {
            var layer = ImportTileableRock();
            foreach (var terrain in root.GetComponentsInChildren<Terrain>())
            {
                var layers = terrain.terrainData.terrainLayers;
                layers[0] = layer;
                terrain.terrainData.terrainLayers = layers;
                EditorUtility.SetDirty(terrain.terrainData);
            }
            RefreshGround(root);
            var seaObject = root.Find("Quiet coastal water");
            if (seaObject != null) Object.DestroyImmediate(seaObject.gameObject);
            CreateSea(root);
            foreach (var site in Sites)
            {
                var parent = root.Find(site.id + " coast and approaches");
                PaintApproaches(parent, site);
                ShareBridgeMaterials(parent, site);
                var rocks = parent.Find("Scanned rock outcrops");
                foreach (var rock in rocks.Cast<Transform>().ToArray())
                {
                    var local = Quaternion.Inverse(site.Rotation) * (rock.position - site.origin);
                    if (Mathf.Abs(local.z) > site.length * .5f - 14 &&
                        Mathf.Abs(local.x) < site.width * .5f + 2 + rock.localScale.x * 1.8f)
                        Object.DestroyImmediate(rock.gameObject);
                }
                float factor = site.length / 180;
                var hero = root.Find(site.id + "-hero");
                hero.position = site.World(new Vector3(-156, 25, -95) * factor);
                hero.LookAt(site.World(new Vector3(0, -site.rise * .48f, 5 * factor)));
                int siteLayer = site.id == "primary" ? 28 : 29;
                foreach (var child in parent.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = siteLayer;
                foreach (var bridge in SceneManager.GetActiveScene().GetRootGameObjects().Where(g =>
                    g.name == "Bwork Bridge Asset [batch:coastal-bridge-" + site.id + "]"))
                    foreach (var child in bridge.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = siteLayer;
                foreach (var camera in root.GetComponentsInChildren<Camera>(true).Where(c => c.name.StartsWith(site.id + "-", StringComparison.Ordinal)))
                    camera.cullingMask = ~(1 << (site.id == "primary" ? 29 : 28));
            }
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath)) throw new IOException("Could not save presentation corrections.");
        }

        static TerrainLayer ImportTileableRock()
        {
            var source = ProjectContext.BridgeSource("DemoMaterials");
            foreach (var suffix in new[] { "Color", "NormalGL" })
            {
                var filename = "Rock030_2K-JPG_" + suffix + ".jpg";
                if (!File.Exists(Path.Combine(source, filename))) throw new FileNotFoundException("Missing staged CC0 tileable rock material.", filename);
                var path = Generated + "/" + filename;
                File.Copy(Path.Combine(source, filename), path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = suffix == "NormalGL" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = suffix == "Color";
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            return Persist(new TerrainLayer {
                name = "Tileable coastal bedrock (ambientCG Rock030 CC0)",
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Generated + "/Rock030_2K-JPG_Color.jpg"),
                normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Generated + "/Rock030_2K-JPG_NormalGL.jpg"),
                tileSize = Vector2.one * 8, normalScale = .45f, metallic = 0, smoothness = .08f
            }, "CoastalRock.terrainlayer");
        }

        static void GuardProbe()
        {
            _ = ProjectContext.Current;
            ProjectContext.RequireIdle();
        }

        static Transform RequireScene()
        {
            if (SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Run bwork_bridge_demo setup to open its isolated scene alone.");
            return SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == RootName).transform;
        }

        static void Setup()
        {
            if (SceneManager.sceneCount == 1 && SceneManager.GetActiveScene().path == ScenePath) { RequireScene(); return; }
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save the current scene before opening the bridge demonstration.");
            RestoreDependencies();
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "/Pipeline.asset");
                if (existing != null) { GraphicsSettings.defaultRenderPipeline = existing; QualitySettings.renderPipeline = existing; }
                RequireScene();
                return;
            }
            // Resolve all external dependencies before changing the open scene.
            foreach (var path in new[] { Art + "Materials/Asphalt.mat", Art + "Materials/Gravel.mat",
                Nature + "Prefabs/rock_moss_set_01_rock01.prefab", Nature + "Prefabs/shrub_03_a.prefab" })
                if (AssetDatabase.LoadMainAssetAtPath(path) == null) throw new FileNotFoundException("Missing reviewed demo asset.", path);
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(RootName).transform;
            ConfigureLighting(root);
            CreateSea(root);
            foreach (var site in Sites) CreateSite(root, site);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save the bridge scene.");
            DynamicGI.UpdateEnvironment();
        }

        static void RestoreDependencies()
        {
            var source = ProjectContext.BridgeSource("demo-dependencies");
            var manifestPath = Path.Combine(source, "manifest.json");
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("Missing committed bridge demo dependency snapshot.", manifestPath);
            var manifest = JsonUtility.FromJson<DependencyManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.schemaVersion != 1 || manifest.files == null || manifest.files.Length > 100)
                throw new InvalidDataException("Invalid demo dependency snapshot.");
            var project = ProjectContext.ProjectRoot;
            // Validate the complete bundle and every destination before copying any missing file.
            foreach (var file in manifest.files)
            {
                if (string.IsNullOrEmpty(file.path) || file.path.Contains("..") || file.path.Contains("\\") ||
                    (!file.path.StartsWith("Assets/RoadQuality/Art/", StringComparison.Ordinal) &&
                     !file.path.StartsWith("Assets/Bwork/ThirdParty/PolyHaven/", StringComparison.Ordinal)))
                    throw new InvalidDataException("Dependency path is outside the owned snapshot scope.");
                var original = Path.Combine(source, file.path);
                var target = Path.Combine(project, ProjectContext.RemapLegacyAssetPath(file.path));
                ProjectContext.RejectLinks(original);
                ProjectContext.RejectLinks(target);
                if (!File.Exists(original) || Hash(original) != file.sha256) throw new InvalidDataException("Dependency snapshot hash mismatch: " + file.path);
                if (File.Exists(target) && Hash(target) != file.sha256)
                    throw new InvalidOperationException("Conflicting existing demo dependency bytes/GUID metadata; preserve or resolve this asset before setup: " + file.path);
            }
            bool restored = false;
            foreach (var file in manifest.files)
            {
                var target = Path.Combine(project, ProjectContext.RemapLegacyAssetPath(file.path));
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Path.Combine(source, file.path), target, false);
                restored = true;
            }
            if (restored) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static void ShareBridgeMaterials(Transform parent, Site site)
        {
            var bridge = SceneManager.GetActiveScene().GetRootGameObjects().SingleOrDefault(g =>
                g.name == "Bwork Bridge Asset [batch:coastal-bridge-" + site.id + "]");
            if (bridge == null) return;
            var materials = bridge.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            Material Find(string key) => materials.Single(m => m.name == key || m.name == "Bridge " + key);
            var asphalt = CopyApproachMaterial(Find("asphalt"), site.id, "asphalt");
            var white = CopyApproachMaterial(Find("roadPaintWhite"), site.id, "roadPaintWhite");
            var amber = CopyApproachMaterial(Find("roadPaintAmber"), site.id, "roadPaintAmber");
            foreach (var renderer in parent.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name.StartsWith(site.id + " asphalt approach ", StringComparison.Ordinal)) renderer.sharedMaterial = asphalt;
                else if (renderer.name.StartsWith(site.id + " shoulder line ", StringComparison.Ordinal)) renderer.sharedMaterial = white;
                else if (renderer.name.StartsWith(site.id + " centre amber ", StringComparison.Ordinal)) renderer.sharedMaterial = amber;
            }
        }

        static Material CopyApproachMaterial(Material source, string site, string key)
        {
            var result = new Material(source);
            foreach (var property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
            {
                var texture = source.GetTexture(property);
                if (texture == null) continue;
                var original = AssetDatabase.GetAssetPath(texture);
                var target = Generated + "/" + site + "-approach-" + Path.GetFileName(original);
                // The bridge command owns and deletes its generation directory. Copy its imported maps
                // with importer settings so approach materials survive bridge replacement/removal.
                if (File.Exists(target) && Hash(original) != Hash(target)) AssetDatabase.DeleteAsset(target);
                if (!File.Exists(target) && !AssetDatabase.CopyAsset(original, target))
                    throw new IOException("Could not preserve bridge-matched approach texture: " + original);
                result.SetTexture(property, AssetDatabase.LoadAssetAtPath<Texture2D>(target));
            }
            return Persist(result, site + "-Matched-" + key + ".mat");
        }

        static T Persist<T>(T value, string name) where T : Object
        {
            var path = Generated + "/" + name;
            var previous = AssetDatabase.LoadAssetAtPath<T>(path);
            if (previous == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, previous);
            Object.DestroyImmediate(value);
            EditorUtility.SetDirty(previous);
            return previous;
        }

        static Transform Group(Transform parent, string name)
        {
            var result = new GameObject(name).transform;
            result.SetParent(parent, false);
            return result;
        }

        static void ConfigureLighting(Transform root)
        {
            var renderer = Persist(ScriptableObject.CreateInstance<UniversalRendererData>(), "Renderer.asset");
            var pipeline = Persist(UniversalRenderPipelineAsset.Create(renderer), "Pipeline.asset");
            pipeline.supportsHDR = true;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.msaaSampleCount = 4;
            pipeline.mainLightShadowmapResolution = 2048;
            pipeline.shadowDistance = 300;
            pipeline.shadowCascadeCount = 2;
            var pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            var sun = Group(root, "Late afternoon coastal sun").gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(36, 65, 0);
            sun.color = new Color(1, .91f, .79f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = .035f;
            sun.shadowNormalBias = .3f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.36f, .44f, .53f);
            RenderSettings.ambientEquatorColor = new Color(.25f, .30f, .34f);
            RenderSettings.ambientGroundColor = new Color(.13f, .14f, .15f);
            RenderSettings.reflectionIntensity = .65f;
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetColor("_SkyTint", new Color(.50f, .56f, .62f));
            sky.SetColor("_GroundColor", new Color(.25f, .29f, .32f));
            sky.SetFloat("_Exposure", 1.05f);
            sky.SetFloat("_AtmosphereThickness", .8f);
            sky.SetFloat("_SunSize", .025f);
            RenderSettings.skybox = Persist(sky, "Sky.mat");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.55f, .64f, .70f);
            RenderSettings.fogDensity = .0011f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(.05f);
            color.contrast.Override(8);
            color.saturation.Override(-8);
            profile = Persist(profile, "PresentationVolume.asset");
            // Volume components must be persistent subassets to survive an Editor reload.
            foreach (var component in profile.components)
                if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
            var volume = Group(root, "Presentation tone and contrast").gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static void CreateSea(Transform root)
        {
            var sea = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            sea.SetColor("_BaseColor", new Color(.075f, .20f, .23f));
            sea.SetFloat("_Metallic", .1f);
            sea.SetFloat("_Smoothness", .87f);
            // Static small ripples support the bridge silhouette without a water-system dependency.
            const int resolution = 512;
            var normal = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true) { name = "Quiet sea normal", wrapMode = TextureWrapMode.Repeat };
            var modes = new[] { new Vector3(2, 1, .022f), new Vector3(5, 2, .016f), new Vector3(9, 3, .011f),
                new Vector3(17, 7, .008f), new Vector3(31, 13, .005f), new Vector3(-3, 5, .009f), new Vector3(-7, 11, .004f) };
            for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
            {
                float u = x / (float)resolution * Mathf.PI * 2, v = y / (float)resolution * Mathf.PI * 2;
                float dx = 0, dy = 0;
                for (int i = 0; i < modes.Length; i++)
                {
                    var mode = modes[i];
                    float slope = mode.z * Mathf.Cos(u * mode.x + v * mode.y + i * 1.719f);
                    float norm = Mathf.Sqrt(mode.x * mode.x + mode.y * mode.y);
                    dx += slope * mode.x / norm; dy += slope * mode.y / norm;
                }
                var n = new Vector3(dx, dy, 1).normalized;
                normal.SetPixel(x, y, new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1));
            }
            normal.Apply(true, false);
            var normalPath = Generated + "/SeaNormal.png";
            File.WriteAllBytes(normalPath, normal.EncodeToPNG());
            Object.DestroyImmediate(normal);
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            importer.textureType = TextureImporterType.NormalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            sea.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            sea.SetFloat("_BumpScale", .5f);
            sea.EnableKeyword("_NORMALMAP");
            sea.SetTextureScale("_BaseMap", Vector2.one / 64);
            var mat = Persist(sea, "Sea.mat");
            MakeStrip(root, "Quiet coastal water", new Vector3(120, 0, 0), Quaternion.identity, 6000, -3000, 3000, 0, mat, false);
        }

        static TerrainLayer Layer(string name, string basePath, float scale, float normalScale)
        {
            return Persist(new TerrainLayer {
                name = name,
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "_BaseMap." + (basePath.StartsWith(Nature, StringComparison.Ordinal) ? "png" : "jpg")),
                normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "_Normal." + (basePath.StartsWith(Nature, StringComparison.Ordinal) ? "png" : "jpg")),
                tileSize = Vector2.one * scale, normalScale = normalScale, metallic = 0, smoothness = .1f
            }, name + ".terrainlayer");
        }

        static float Height(Site site, float x, float z)
        {
            float az = Mathf.Abs(z), halfSpan = site.span * .5f, end = site.length * .5f;
            float edge = halfSpan - 5 + 2.7f * Mathf.Sin(x * .048f) + 1.3f * Mathf.Sin(x * .15f + 1);
            float bank = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(edge - 9, end - 3, az));
            float hill = 4 * Mathf.PerlinNoise(x * .023f + 13, z * .029f + 9) + 2 * Mathf.Sin(x * .04f + z * .055f);
            float y = Mathf.Lerp(-8, site.origin.y + hill, bank);
            // Preserve the springing rock bearing elevation and road sockets.
            float footing = Mathf.Exp(-Mathf.Pow((az - halfSpan) / 6, 2) - Mathf.Pow(x / 12, 2));
            y = Mathf.Lerp(y, 5.2f, footing);
            float road = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(site.width * .5f + .5f, site.width * .5f + 4, Mathf.Abs(x))))
                * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(end - 6, end - 2, az));
            y = Mathf.Lerp(y, site.origin.y - .16f, road);
            // Low western shore keeps the hero and side cameras over open water.
            float shore = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-108, -45, x));
            y = Mathf.Lerp(-7, y, shore);
            // Measured generator footings: bury their full footprint, including bilinear terrain sampling margins.
            float springMask = BearingMask(x, az - halfSpan, (site.width + 1) * .36f + 2.3f, 3.2f);
            float pierMask = BearingMask(x, az - site.pierStation, (site.width + 1) * .36f + 1.9f, 2.4f);
            y = Mathf.Max(y, Mathf.Lerp(-8, site.origin.y + site.springBottom + .55f, springMask));
            y = Mathf.Max(y, Mathf.Lerp(-8, site.origin.y + site.pierBottom + .45f, pierMask));
            // The driving corridor crosses the bank crest before its socket. Cap the complete corridor,
            // rather than just the approach, so no terrain crest can intersect the bridge deck.
            float clearance = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(site.width * .5f + 1.2f, site.width * .5f + 5, Mathf.Abs(x)));
            y = Mathf.Lerp(y, Mathf.Min(y, site.origin.y - .22f), clearance);
            return y;
        }

        static float BearingMask(float x, float z, float halfWidth, float halfLength)
        {
            return (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(halfWidth, halfWidth + 7, Mathf.Abs(x))))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(halfLength, halfLength + 6, Mathf.Abs(z))));
        }

        static void RefreshGround(Transform root)
        {
            foreach (var site in Sites)
            {
                var parent = root.Find(site.id + " coast and approaches");
                var terrain = parent.GetComponentInChildren<Terrain>();
                var data = terrain.terrainData;
                int resolution = data.heightmapResolution;
                var heights = new float[resolution, resolution];
                for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
                {
                    var world = terrain.transform.position + new Vector3(x / (float)(resolution - 1) * data.size.x, 0, z / (float)(resolution - 1) * data.size.z);
                    var local = Quaternion.Inverse(site.Rotation) * (world - site.origin);
                    heights[z, x] = Mathf.Clamp01((Height(site, local.x, local.z) - terrain.transform.position.y) / data.size.y);
                }
                data.SetHeights(0, 0, heights);
                EditorUtility.SetDirty(data);
                foreach (Transform rock in parent.Find("Scanned rock outcrops"))
                {
                    var local = Quaternion.Inverse(site.Rotation) * (rock.position - site.origin);
                    rock.position = site.World(new Vector3(local.x, Height(site, local.x, local.z) - site.origin.y - .7f * rock.localScale.y, local.z));
                }
            }
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath)) throw new IOException("Could not save footing ground contact.");
        }

        static void CreateSite(Transform root, Site site)
        {
            var parent = Group(root, site.id + " coast and approaches");
            const int resolution = 257;
            const float sizeX = 220, sizeZ = 360, bottom = -12, height = 100;
            // Terrain stays axis aligned; the analytic gorge and road masks share the bridge rotation.
            var data = new TerrainData { name = site.id + " rocky ravine", heightmapResolution = resolution,
                size = new Vector3(sizeX, height, sizeZ), alphamapResolution = 256, baseMapResolution = 1024 };
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
            {
                var offset = new Vector3(x / (float)(resolution - 1) * sizeX - sizeX / 2, 0, z / (float)(resolution - 1) * sizeZ - sizeZ / 2);
                var local = Quaternion.Inverse(site.Rotation) * offset;
                heights[z, x] = Mathf.Clamp01((Height(site, local.x, local.z) - bottom) / height);
            }
            data.SetHeights(0, 0, heights);
            data.terrainLayers = new[] {
                ImportTileableRock(),
                Layer("CoastalGravel", Art + "Textures/Gravel", 3, .7f),
                Layer("CoastalSoil", Art + "Textures/Dirt", 3, .55f),
                Layer("CoastalGrass", Art + "Textures/Ground", 3, .55f)
            };
            var map = new float[256, 256, 4];
            for (int z = 0; z < 256; z++) for (int x = 0; x < 256; x++)
            {
                float nx = x / 255f, nz = z / 255f;
                float slope = data.GetSteepness(nx, nz), h = data.GetInterpolatedHeight(nx, nz) + bottom;
                float rock = Mathf.Lerp(.38f, .97f, Mathf.InverseLerp(12, 38, slope));
                float grass = (1 - rock) * Mathf.InverseLerp(site.origin.y - 2, site.origin.y + 3, h)
                    * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.48f, .70f, Mathf.PerlinNoise(nx * 9 + 3, nz * 11 + 4)));
                map[z, x, 0] = rock; map[z, x, 3] = grass;
                map[z, x, 1] = (1 - rock - grass) * .65f;
                map[z, x, 2] = (1 - rock - grass) * .35f;
            }
            data.SetAlphamaps(0, 0, map);
            data = Persist(data, site.id + "-Terrain.asset");
            var terrain = Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();
            terrain.name = site.id + " textured bedrock banks";
            terrain.transform.SetParent(parent, false);
            terrain.transform.position = new Vector3(site.origin.x - sizeX / 2, bottom, site.origin.z - sizeZ / 2);
            terrain.heightmapPixelError = 2;
            terrain.basemapDistance = 600;
            terrain.materialTemplate = Persist(new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")), site.id + "-Terrain.mat");
            var asphalt = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Asphalt.mat");
            var gravel = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Gravel.mat");
            foreach (int sign in new[] { -1, 1 })
            {
                float a = site.length * .5f, b = a + 65;
                float start = sign < 0 ? -b : a, end = sign < 0 ? -a : b;
                MakeStrip(parent, site.id + " gravel approach " + sign, site.origin, site.Rotation, site.width + 2, start, end, -.08f, gravel, true);
                MakeStrip(parent, site.id + " asphalt approach " + sign, site.origin, site.Rotation, site.width, start, end, 0, asphalt, true);
            }
            PaintApproaches(parent, site);
            ScatterRocksAndShrubs(parent, site);
            float cameraScale = site.length / 180;
            AddCamera(root, site.id + "-hero", site, new Vector3(-156, 25, -95) * cameraScale, new Vector3(0, -site.rise * .48f, 5 * cameraScale), 49);
            AddCamera(root, site.id + "-side", site, new Vector3(-174, -4, -7) * cameraScale, new Vector3(0, -site.rise * .54f, 0), 48);
            AddCamera(root, site.id + "-riding", site, new Vector3(-1.6f, 1.65f, -site.length * .5f - 10), new Vector3(-1.6f, 1.5f, site.length * .4f), 62);
            AddCamera(root, site.id + "-detail", site, new Vector3(-9, 3.8f, -site.length * .25f), new Vector3(-site.width * .5f, -.5f, -site.length * .25f + 12), 48);
        }

        static void PaintApproaches(Transform parent, Site site)
        {
            if (parent == null) throw new InvalidOperationException("Missing owned approach scenery group.");
            var oldPaint = parent.Cast<Transform>().Where(t => t.name.StartsWith(site.id + " shoulder line ", StringComparison.Ordinal)
                || t.name.StartsWith(site.id + " centre dash ", StringComparison.Ordinal)
                || t.name.StartsWith(site.id + " centre amber ", StringComparison.Ordinal)).ToArray();
            foreach (var item in oldPaint) Object.DestroyImmediate(item.gameObject);
            var white = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            white.SetColor("_BaseColor", new Color(.72f, .74f, .70f));
            white.SetFloat("_Smoothness", .18f);
            white = Persist(white, site.id + "-Marking.mat");
            var amber = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            amber.SetColor("_BaseColor", new Color(.9f, .48f, .055f));
            amber.SetFloat("_Smoothness", .22f);
            amber = Persist(amber, site.id + "-AmberMarking.mat");
            foreach (int sign in new[] { -1, 1 })
            {
                float a = site.length * .5f, b = a + 65;
                float start = sign < 0 ? -b : a, end = sign < 0 ? -a : b;
                foreach (float side in new[] { -1f, 1f })
                {
                    MakeStrip(parent, site.id + " shoulder line " + sign + " " + side,
                        site.World(new Vector3(side * (site.width * .5f - .2f), 0, 0)), site.Rotation, .10f, start, end, .004f, white, false);
                    MakeStrip(parent, site.id + " centre amber " + sign + " " + side,
                        site.World(new Vector3(side * .09f, 0, 0)), site.Rotation, .09f, start, end, .004f, amber, false);
                }
            }
        }

        static void ScatterRocksAndShrubs(Transform parent, Site site)
        {
            var rocks = Group(parent, "Scanned rock outcrops");
            var shrubs = Group(parent, "Sparse wind exposed shrubs");
            var rock = AssetDatabase.LoadAssetAtPath<GameObject>(Nature + "Prefabs/rock_moss_set_01_rock01.prefab");
            var shrub = AssetDatabase.LoadAssetAtPath<GameObject>(Nature + "Prefabs/shrub_03_a.prefab");
            var random = new System.Random(site.id == "primary" ? 714 : 812);
            for (int i = 0; i < 135; i++)
            {
                float x = -63 + (float)random.NextDouble() * 142;
                float z = (i % 2 == 0 ? -1 : 1) * (site.span * .5f - 8 + (float)random.NextDouble() * (site.length * .5f - site.span * .5f + 25));
                if (Mathf.Abs(x) < site.width * .5f + 3 && Mathf.Abs(z) > site.length * .5f - 7) continue;
                // Keep the two arch ribs legible and reserve the actual concrete bearing footprint.
                if (Mathf.Abs(x) < 6 && Mathf.Abs(Mathf.Abs(z) - site.span * .5f) < 4) continue;
                float ground = Height(site, x, z);
                if (ground < 1) continue;
                float scale = 1.0f + (float)random.NextDouble() * 2.6f;
                if (Mathf.Abs(z) > site.length * .5f - 14 && Mathf.Abs(x) < site.width * .5f + 2 + scale * 1.25f * 1.8f) continue;
                var item = (GameObject)PrefabUtility.InstantiatePrefab(rock, rocks);
                item.name = "Bedrock outcrop " + i;
                item.transform.position = site.World(new Vector3(x, ground - site.origin.y - .7f * scale, z));
                item.transform.rotation = site.Rotation * Quaternion.Euler(-12 + (float)random.NextDouble() * 24, (float)random.NextDouble() * 360, -10 + (float)random.NextDouble() * 20);
                item.transform.localScale = new Vector3(scale * 1.25f, scale, scale * .95f);
            }
            for (int i = 0; i < 80; i++)
            {
                float x = -34 + (float)random.NextDouble() * 118;
                float z = (i % 2 == 0 ? -1 : 1) * (site.length * .5f + 4 + (float)random.NextDouble() * 55);
                if (Mathf.Abs(x) < site.width * .5f + 4) continue;
                float ground = Height(site, x, z);
                if (ground < site.origin.y - 2) continue;
                var item = (GameObject)PrefabUtility.InstantiatePrefab(shrub, shrubs);
                item.transform.position = site.World(new Vector3(x, ground - site.origin.y - .10f, z));
                item.transform.rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
                item.transform.localScale = Vector3.one * (.55f + (float)random.NextDouble() * .8f);
            }
        }

        static void MakeStrip(Transform root, string name, Vector3 origin, Quaternion rotation, float width,
            float start, float end, float y, Material material, bool collision)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(-width / 2, y, start), new Vector3(width / 2, y, start),
                new Vector3(-width / 2, y, end), new Vector3(width / 2, y, end) };
            mesh.uv = new[] { new Vector2(0, start), new Vector2(width, start), new Vector2(0, end), new Vector2(width, end) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            mesh = Persist(mesh, name.Replace(' ', '_') + ".asset");
            var item = Group(root, name).gameObject;
            item.transform.SetPositionAndRotation(origin, rotation);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = collision ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collision) item.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static void AddCamera(Transform root, string name, Site site, Vector3 position, Vector3 target, float fov)
        {
            var camera = Group(root, name).gameObject.AddComponent<Camera>();
            camera.transform.position = site.World(position);
            camera.transform.LookAt(site.World(target));
            camera.fieldOfView = fov;
            camera.nearClipPlane = .12f;
            camera.farClipPlane = 4500;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.enabled = name == "primary-hero";
            if (camera.enabled) camera.tag = "MainCamera";
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        static string Capture(Transform root, string name)
        {
            var camera = root.GetComponentsInChildren<Camera>(true).SingleOrDefault(c => c.name == name)
                ?? throw new ArgumentException("Unknown named bridge camera.");
            var directory = ProjectContext.CaptureDirectory("bridges");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name + ".png");
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
                if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("URP camera capture is unavailable.");
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
                target.Release(); Object.DestroyImmediate(target);
            }
            return path;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.Pipeline.Commands;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace Bwork.Authoring.Editor
{
    /// <summary>Small, pinned adapter over the installed URP Terrain Lit implementation.</summary>
    public static class TerrainAntiTiling
    {
        public const string NativeShader = "Universal Render Pipeline/Terrain/Lit";
        public const string ShaderPrefix = "BFjord/Terrain/Stochastic Lit/";
        const string BasemapPrefix = "BFjord/Terrain/Stochastic Basemap/";
        const string Urp = "Packages/com.unity.render-pipelines.universal/";
        const string TerrainRoot = Urp + "Shaders/Terrain/";
        const string Helper = "Packages/com.bfjord.tools/Shaders/BFjordTerrainSampling.hlsl";
        static readonly HashSet<string> Preflighted = new HashSet<string>();
        // Exact admitted source, not just a version label. A package patch can alter
        // sample sites, struct layouts or passes without changing its version string.
        static readonly Dictionary<string, string> Digests = new Dictionary<string, string>
        {
            ["TerrainLit.shader"] = "2580f9167eb447bc3e52875ac54d6fe50b72e0c209f10e66c3f2bc13e73c607a",
            ["TerrainLitInput.hlsl"] = "e79255f1f30baf586e1a33985c1d8d3f165017d1211bc8556de154e9131b1975",
            ["TerrainLitPasses.hlsl"] = "418a9a0b61dfd5b49b520716d83873d5ff339b4db441d974eb5e140d68297551",
            ["TerrainLitDepthNormalsPass.hlsl"] = "cefb53c8c867c459cd72e2061c098944eb2e38fdf8816adc63c3293626809d56",
            ["TerrainLitBasemapGen.shader"] = "49ddcc8f1a65f6132e6372e6e33c61ee226a0991a041e7cf22c6e517d637bda0"
        };
        const string Properties = @"
        _BFjordAppearanceSeed(""Appearance seed"", Float) = 731
        _BFjordCellTiles(""Stochastic cell (texture repeats)"", Float) = 1.0
        _BFjordMacroMeters(""Macro scale (metres)"", Float) = 37
        _BFjordMacroVariation(""Macro variation"", Float) = 0.28
        _BFjordLayerMeters(""Layer scales (metres)"", Vector) = (2,3,2.5,90)
";
        const string Uniforms = @"
    float _BFjordAppearanceSeed, _BFjordCellTiles, _BFjordMacroMeters, _BFjordMacroVariation;
    float4 _BFjordLayerMeters;
";

        [CliCommand("bwork_terrain_surface_view", "Capture a fixed terrain camera with native distance switching or a temporary detail/basemap diagnostic.", MainThreadRequired = true)]
        public static object CaptureView(
            [CliArg("view", "surface, near or far")] string view = "near",
            [CliArg("representation", "natural, detail or basemap; temporary override is restored after capture")] string representation = "natural",
            [CliArg("capture", "Screenshot basename")] string capture = "terrain-surface")
        {
            if (view != "surface" && view != "near" && view != "far") throw new ArgumentException("Terrain surface view must be surface, near or far.");
            if (representation != "natural" && representation != "detail" && representation != "basemap")
                throw new ArgumentException("Terrain representation must be natural, detail or basemap.");
            var terrain = ToolSandbox.RequireTerrain();
            float priorDistance = terrain.basemapDistance;
            object camera;
            if (view == "surface")
            {
                var cameraObject = ToolSandbox.Root.GetComponentInChildren<Camera>();
                Vector3 Ground(float x, float z, float offset)
                {
                    var p = new Vector3(x, 0, z);
                    p.y = terrain.SampleHeight(p) + terrain.transform.position.y + offset;
                    return p;
                }
                var position = Ground(222, 230, 3);
                var target = Ground(226, 224, .05f);
                cameraObject.transform.position = position;
                cameraObject.transform.LookAt(target);
                cameraObject.fieldOfView = 53;
                cameraObject.orthographic = false;
                camera = new { view, cameraPosition = new[] { position.x, position.y, position.z },
                    cameraTarget = new[] { target.x, target.y, target.z }, eyeAboveTerrainMeters = 3f };
            }
            else camera = SandboxViews.Run(view == "near" ? "terrain-ground" : "terrain");
            try
            {
                if (representation == "detail") terrain.basemapDistance = 20000;
                if (representation == "basemap") terrain.basemapDistance = 0;
                return new { view, representation, camera, image = ToolSandbox.Capture(capture) };
            }
            finally { terrain.basemapDistance = priorDistance; }
        }

        public static Dictionary<string, string> ReadVerifiedSources()
        {
            var package = PackageInfo.FindForAssetPath(Urp + "package.json");
            if (package == null || package.version != "17.6.0")
                throw new InvalidOperationException("Terrain anti-tiling requires the reviewed URP 17.6.0 package. Use antiTiling=false for native Terrain Lit.");
            var sources = Digests.Keys.ToDictionary(name => name,
                name => File.ReadAllText(Path.Combine(package.resolvedPath, "Shaders/Terrain", name)));
            ValidateSources(sources);
            return sources;
        }

        public static void ValidateSources(IReadOnlyDictionary<string, string> sources)
        {
            foreach (var dependency in Digests)
                if (!sources.TryGetValue(dependency.Key, out string source) || Hash(source) != dependency.Value)
                    throw new InvalidOperationException("Terrain anti-tiling has not admitted this URP source: " + dependency.Key +
                        ". Review the changed package before adapting it, or use antiTiling=false.");
        }

        // Pure source transform is testable without touching a scene or starting a
        // second Editor. Native geometry, lighting, holes and pass bodies are retained.
        public static Dictionary<string, string> BuildSources(IReadOnlyDictionary<string, string> native,
            string outputRoot, string identity, string helper)
        {
            ValidateSources(native);
            var output = native.ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (string file in native.Keys)
            {
                string source = output[file];
                foreach (string include in new[] { "TerrainLitInput.hlsl", "TerrainLitPasses.hlsl", "TerrainLitDepthNormalsPass.hlsl" })
                    source = source.Replace("\"" + TerrainRoot + include + "\"", "\"" + outputRoot + "/" + include + "\"")
                        .Replace("\"" + include + "\"", "\"" + outputRoot + "/" + include + "\"");
                // Integer hash and explicit-gradient sampling are supported by Metal
                // and GLES3. Leave the native deferred target (4.5) intact.
                source = source.Replace("#pragma target 2.0", "#pragma target 3.5").Replace("#pragma target 3.0", "#pragma target 3.5");
                output[file] = source;
            }
            output["TerrainLitInput.hlsl"] = output["TerrainLitInput.hlsl"].Replace("CBUFFER_START(UnityPerMaterial)",
                "CBUFFER_START(UnityPerMaterial)\n" + Uniforms) + "\n#include \"" + outputRoot + "/BFjordTerrainSampling.hlsl\"\n";
            output["TerrainLitPasses.hlsl"] = ReplaceSurfaceSamples(output["TerrainLitPasses.hlsl"], false);
            output["TerrainLitBasemapGen.shader"] = ReplaceSurfaceSamples(output["TerrainLitBasemapGen.shader"], true);
            foreach (string shader in new[] { "TerrainLit.shader", "TerrainLitBasemapGen.shader" })
            {
                output[shader] = output[shader].Replace("    Properties\n    {", "    Properties\n    {\n" + Properties)
                    .Replace("\"Hidden/Universal Render Pipeline/Terrain/Lit (Basemap Gen)\"", "\"" + BasemapPrefix + identity + "\"");
            }
            output["TerrainLit.shader"] = output["TerrainLit.shader"].Replace("Shader \"" + NativeShader + "\"", "Shader \"" + ShaderPrefix + identity + "\"");
            output["BFjordTerrainSampling.hlsl"] = helper;
            return output;
        }

        static string ReplaceSurfaceSamples(string source, bool basemap)
        {
            for (int i = 0; i < 4; i++)
            {
                string uv = (i < 2 ? "uvSplat01" : "uvSplat23") + (i % 2 == 0 ? ".xy" : ".zw");
                string meters = "_BFjordLayerMeters." + "xyzw"[i];
                if (!basemap)
                {
                    source = ReplaceExactly(source, "SAMPLE_TEXTURE2D(_Splat" + i + ", sampler_Splat0, " + uv + ")",
                        "BFjordTerrainColor(TEXTURE2D_ARGS(_Splat" + i + ", sampler_Splat0), " + uv + ", " + meters + ")", 1);
                    source = ReplaceExactly(source, "UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal" + i + ", sampler_Normal0, " + uv + "), _NormalScale" + i + ")",
                        "BFjordTerrainNormal(TEXTURE2D_ARGS(_Normal" + i + ", sampler_Normal0), " + uv + ", " + meters + ", _NormalScale" + i + ")", 1);
                }
                else uv = "IN." + uv;
                source = ReplaceExactly(source, "SAMPLE_TEXTURE2D(_Mask" + i + ", sampler_Mask0, " + uv + ")",
                    "BFjordTerrainData(TEXTURE2D_ARGS(_Mask" + i + ", sampler_Mask0), " + uv + ", " + meters + ")", basemap ? 2 : 1);
            }
            return source;
        }

        static string ReplaceExactly(string source, string old, string replacement, int count)
        {
            if ((source.Length - source.Replace(old, "").Length) / old.Length != count)
                throw new InvalidOperationException("Unsupported URP terrain sampling site: " + old);
            return source.Replace(old, replacement);
        }

        public static Shader Prepare(bool heightBlend = true)
        {
            var native = ReadVerifiedSources();
            var toolPackage = PackageInfo.FindForAssetPath(Helper);
            if (toolPackage == null) throw new InvalidOperationException("Install BFjord Tools as a Unity package before terrain anti-tiling.");
            string helper = File.ReadAllText(Path.Combine(toolPackage.resolvedPath, "Shaders/BFjordTerrainSampling.hlsl"));
            var provisional = BuildSources(native, "OUTPUT", "IDENTITY", helper);
            string identity = Hash(string.Join("\n", provisional.OrderBy(p => p.Key).Select(p => p.Key + "\n" + p.Value))).Substring(0, 16);
            string root = ToolSandbox.Generated + "/TerrainShader/" + identity;
            var output = BuildSources(native, root, identity, helper);
            var urpPackage = PackageInfo.FindForAssetPath(Urp + "package.json");
            output["LICENSE-Unity.md"] = File.ReadAllText(Path.Combine(urpPackage.resolvedPath, "LICENSE.md"));
            output["NOTICE.txt"] = "Generated locally from the installed Unity URP 17.6.0 package.\n" +
                "Unity source retains its accompanying license. BFjord alters surface sampling only.\n" +
                string.Join("\n", Digests.Select(p => p.Key + " SHA-256 " + p.Value)) + "\n";
            ProjectContext.RejectLinks(root);
            // Content-addressed outputs are immutable: never overwrite a user's edit.
            foreach (var file in output)
            {
                string path = root + "/" + file.Key;
                ProjectContext.RejectLinks(path);
                if (File.Exists(path) && File.ReadAllText(path) != file.Value)
                    throw new InvalidOperationException("Generated terrain shader was edited externally: " + path);
            }
            Directory.CreateDirectory(root);
            foreach (var file in output.OrderBy(p => p.Key.EndsWith(".shader", StringComparison.Ordinal) ? 1 : 0))
            {
                string path = root + "/" + file.Key;
                if (File.Exists(path)) continue;
                File.WriteAllText(path, file.Value, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(root + "/TerrainLit.shader");
            if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Generated terrain anti-tiling shader did not import successfully. Existing terrain appearance is retained.");
            var basemap = AssetDatabase.LoadAssetAtPath<Shader>(root + "/TerrainLitBasemapGen.shader");
            if (basemap == null || !basemap.isSupported || ShaderUtil.ShaderHasError(basemap))
                throw new InvalidOperationException("Generated terrain anti-tiling basemap shader did not import successfully.");
            Preflight(shader, heightBlend, false);
            Preflight(basemap, heightBlend, true);
            return shader;
        }

        static void Preflight(Shader shader, bool heightBlend, bool basemap)
        {
            string key = shader.name + "/heightBlend=" + heightBlend;
            if (Preflighted.Contains(key)) return;
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                // Import can succeed before fragment variants have been compiled.
                // Cover the requested height mode, holes, and ordinary/instanced
                // per-pixel terrain, without expanding every URP lighting permutation.
                for (int variant = 0; variant < (basemap ? 1 : 2); variant++)
                {
                    var keywords = new List<string> { "_MASKMAP" };
                    if (heightBlend) keywords.Add("_TERRAIN_BLEND_HEIGHT");
                    if (!basemap) { keywords.Add("_NORMALMAP"); keywords.Add("_ALPHATEST_ON"); }
                    if (variant == 1) { keywords.Add("INSTANCING_ON"); keywords.Add("_TERRAIN_INSTANCED_PERPIXEL_NORMAL"); }
                    material.shaderKeywords = keywords.ToArray();
                    material.enableInstancing = variant == 1;
                    for (int pass = 0; pass < material.passCount; pass++)
                    {
                        ShaderUtil.CompilePass(material, pass, true);
                        if (ShaderUtil.ShaderHasError(shader))
                            throw new InvalidOperationException("Terrain shader preflight failed before assignment: " + shader.name +
                                " pass " + material.GetPassName(pass) + ". " +
                                string.Join("; ", ShaderUtil.GetShaderMessages(shader).Select(message => message.message)));
                    }
                }
                Preflighted.Add(key);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        public static void Configure(Material material, TerrainPaintProfile profile, TerrainMaterialBank.Surface[] surfaces, Shader shader)
        {
            material.shader = shader;
            if (!profile.antiTiling) return;
            material.SetFloat("_BFjordAppearanceSeed", ((long)profile.appearanceSeed % 65521 + 65521) % 65521);
            material.SetFloat("_BFjordCellTiles", profile.stochasticCellTiles);
            material.SetFloat("_BFjordMacroMeters", profile.macroScaleMeters);
            material.SetFloat("_BFjordMacroVariation", profile.macroVariation);
            material.SetVector("_BFjordLayerMeters", new Vector4(surfaces[0].tileMeters, surfaces[1].tileMeters, surfaces[2].tileMeters, surfaces[3].tileMeters));
        }

        static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
    }
}

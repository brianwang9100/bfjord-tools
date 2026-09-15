using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bwork.Authoring.Editor
{
    [Serializable]
    public sealed class AuthoringProjectConfiguration
    {
        public int schemaVersion;
        public bool enabled;
        public string sandboxScene = "Assets/Scenes/WorldAuthoringTools.unity";
        public string bridgeDemoScene = "Assets/Scenes/BridgeAuthoringDemo.unity";
        public string sandboxGeneratedRoot = "Assets/Generated/AuthoringTools";
        public string bridgeDemoGeneratedRoot = "Assets/Generated/BridgeDemo";
        public string bridgeAssetGeneratedRoot = "Assets/Generated/BridgeAssets";
        public string materialRoot = "Assets/RoadQuality/Art";
        public string natureRoot = "Assets/Bwork/ThirdParty/PolyHaven";
        public string matureFirRoot = "Assets/Bwork/ThirdParty/PolyHavenMatureFir";
        public string sandboxSkyMaterial = "";
        public string bridgeSourceRoot;
        public string rockSourceRoot = "";
        public string captureRoot;
        public string[] additionalAllowedScenes = Array.Empty<string>();
    }

    /// <summary>Explicit per-project opt-in and paths; never infers authorization from a folder name.</summary>
    public static class ProjectContext
    {
        public const string ConfigurationPath = "ProjectSettings/BfjordTools.json";
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static AuthoringProjectConfiguration Current => Load(ProjectRoot);

        [CliCommand("bwork_project", "Inspect or explicitly configure this project's BFjord Tools paths.", MainThreadRequired = true)]
        public static object Run(
            [CliArg("action", "status or configure")] string action = "status",
            [CliArg("configPath", "Required JSON configuration file for configure; paths resolve against the Unity project")] string configPath = "")
        {
            var marker = Path.Combine(ProjectRoot, ConfigurationPath);
            if (action == "status")
                return File.Exists(marker)
                    ? new { configured = true, configurationPath = marker, configuration = Current }
                    : (object)new { configured = false, configurationPath = marker, instruction = "Run bwork_project action=configure configPath=<JSON> with enabled=true." };
            if (action != "configure") throw new ArgumentException("Unknown project action.");
            RequireIdle();
            if (string.IsNullOrWhiteSpace(configPath)) throw new InvalidDataException("configure requires an explicit configPath.");
            string source = ResolveExternal(ProjectRoot, configPath);
            RequireSmallFile(source);
            string json = File.ReadAllText(source);
            var next = Parse(json, ProjectRoot);
            var prior = File.Exists(marker) ? Current : null;
            if (prior != null && JsonConvert.SerializeObject(prior) == JsonConvert.SerializeObject(next))
                return new { configured = true, unchanged = true, configurationPath = marker, configuration = prior };
            RequireSavedScenes();
            if (prior != null && OwnershipChanged(prior, next) && HasOwnedContent(prior))
                throw new InvalidOperationException("Configured scene/output roots contain content. Remove owned outputs or preserve this configuration before changing ownership paths.");
            Directory.CreateDirectory(Path.GetDirectoryName(marker));
            RejectLinks(marker);
            // Preserve relative paths in the marker so a clean checkout can move between machines.
            string temporary = marker + ".pending";
            RejectLinks(temporary);
            try
            {
                File.WriteAllText(temporary, json);
                if (File.Exists(marker)) File.Replace(temporary, marker, null);
                else File.Move(temporary, marker);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return new { configured = true, unchanged = false, configurationPath = marker, configuration = next };
        }

        public static AuthoringProjectConfiguration Load(string projectRoot)
        {
            string marker = Path.Combine(projectRoot, ConfigurationPath);
            if (!File.Exists(marker)) throw new InvalidOperationException("This project has not opted in to BFjord Tools. Configure ProjectSettings/BfjordTools.json with bwork_project first.");
            RequireSmallFile(marker);
            return Parse(File.ReadAllText(marker), projectRoot);
        }

        public static AuthoringProjectConfiguration Parse(string json, string projectRoot)
        {
            AuthoringProjectConfiguration c;
            try
            {
                c = JsonConvert.DeserializeObject<AuthoringProjectConfiguration>(json, new JsonSerializerSettings
                { MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 16 });
            }
            catch (JsonException e) { throw new InvalidDataException("Invalid BFjord project configuration: " + e.Message, e); }
            Require(c != null && c.schemaVersion == 1 && c.enabled, "Configuration requires schemaVersion=1 and explicit enabled=true.");
            projectRoot = Path.GetFullPath(projectRoot);
            ValidateAssetPath(c.sandboxScene, true); ValidateAssetPath(c.bridgeDemoScene, true);
            Require(c.sandboxScene != c.bridgeDemoScene, "Sandbox and bridge demonstration scenes must differ.");
            Require(c.additionalAllowedScenes != null && c.additionalAllowedScenes.Length <= 32, "At most 32 additional scenes are supported.");
            foreach (var scene in c.additionalAllowedScenes) ValidateAssetPath(scene, true);
            var generated = new[] { c.sandboxGeneratedRoot, c.bridgeDemoGeneratedRoot, c.bridgeAssetGeneratedRoot };
            foreach (var path in generated.Concat(new[] { c.materialRoot, c.natureRoot, c.matureFirRoot }))
            { ValidateAssetPath(path, false); RejectLinks(Path.Combine(projectRoot, path)); }
            for (int i = 0; i < generated.Length; i++)
                for (int j = i + 1; j < generated.Length; j++)
                    Require(!Overlaps(generated[i], generated[j]), "Generated output roots must not overlap.");
            foreach (var output in generated)
                foreach (var input in new[] { c.materialRoot, c.natureRoot, c.matureFirRoot })
                    Require(!Overlaps(output, input), "Generated roots must not overlap source asset roots.");
            if (!string.IsNullOrEmpty(c.sandboxSkyMaterial)) ValidateAssetPath(c.sandboxSkyMaterial, false);
            c.bridgeSourceRoot = ResolveExternal(projectRoot, c.bridgeSourceRoot);
            if (!string.IsNullOrWhiteSpace(c.rockSourceRoot))
                c.rockSourceRoot = ResolveExternal(projectRoot, c.rockSourceRoot);
            c.captureRoot = ResolveExternal(projectRoot, c.captureRoot);
            Require(c.captureRoot != projectRoot && c.captureRoot != Path.GetPathRoot(c.captureRoot), "Capture output requires its own directory.");
            foreach (string protectedRoot in new[] { "Assets", "Packages", "ProjectSettings", "Library", "UserSettings", ".git" })
                Require(!Overlaps(c.captureRoot, Path.Combine(projectRoot, protectedRoot)), "Capture output must not overlap Unity project source or settings.");
            Require(!Overlaps(c.captureRoot, c.bridgeSourceRoot), "Capture output must not overlap the bridge source bundle.");
            if (!string.IsNullOrEmpty(c.rockSourceRoot))
                Require(!Overlaps(c.captureRoot, c.rockSourceRoot), "Capture output must not overlap the rock source bundle.");
            foreach (var path in new[] { c.sandboxScene, c.bridgeDemoScene }.Concat(c.additionalAllowedScenes))
                RejectLinks(Path.Combine(projectRoot, path));
            return c;
        }

        public static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Idle Edit Mode is required.");
        }

        public static void RequireSavedScenes()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save existing scene edits before changing the project configuration or opening another scene.");
        }

        public static Scene RequireAllowedScene()
        {
            var config = Current;
            RequireIdle();
            var scene = SceneManager.GetActiveScene();
            Require(SceneManager.sceneCount == 1 && scene.IsValid() && scene.isLoaded &&
                new[] { config.sandboxScene, config.bridgeDemoScene }.Concat(config.additionalAllowedScenes).Contains(scene.path),
                "Open one saved scene explicitly listed in the BFjord project configuration.");
            return scene;
        }

        public static string CaptureDirectory(string group)
        {
            Require(!string.IsNullOrEmpty(group) && group.All(c => char.IsLetterOrDigit(c) || c == '-'), "Simple capture group required.");
            var directory = Path.Combine(Current.captureRoot, group);
            RejectLinks(directory);
            return directory;
        }

        public static string BridgeSource(string relative)
        {
            ValidateRelative(relative);
            var path = Path.Combine(Current.bridgeSourceRoot, relative);
            RejectLinks(path);
            return path;
        }

        public static string Material(string name) => Current.materialRoot + "/Materials/" + name + ".mat";

        public static string RockSource(string relative)
        {
            ValidateRelative(relative);
            var root = Current.rockSourceRoot;
            Require(!string.IsNullOrEmpty(root), "Configure rockSourceRoot before authoring original rock assets.");
            var path = Path.Combine(root, relative);
            RejectLinks(path);
            return path;
        }

        public static string RemapLegacyAssetPath(string path)
        {
            var config = Current;
            foreach (var pair in new[] {
                new[] { "Assets/RoadQuality/Art/", config.materialRoot + "/" },
                new[] { "Assets/Bwork/ThirdParty/PolyHavenMatureFir/", config.matureFirRoot + "/" },
                new[] { "Assets/Bwork/ThirdParty/PolyHaven/", config.natureRoot + "/" } })
                if (path.StartsWith(pair[0], StringComparison.Ordinal)) return pair[1] + path.Substring(pair[0].Length);
            return path;
        }

        public static void ValidateAssetPath(string path, bool scene)
        {
            ValidateRelative(path);
            Require(path.StartsWith("Assets/", StringComparison.Ordinal) && (!scene || path.EndsWith(".unity", StringComparison.Ordinal)),
                scene ? "Scene paths must name a .unity file under Assets/." : "Asset roots must be beneath Assets/.");
        }

        public static void ValidateRelative(string path)
        {
            Require(!string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Contains('\\') && !path.Contains(':') &&
                path.Split('/').All(p => p.Length > 0 && p != "." && p != ".."), "A contained, forward-slash relative path is required.");
        }

        static string ResolveExternal(string projectRoot, string path)
        {
            Require(!string.IsNullOrWhiteSpace(path), "Explicit bridgeSourceRoot/captureRoot or source configuration path required.");
            var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path));
            RejectLinks(full);
            return full.TrimEnd(Path.DirectorySeparatorChar);
        }

        public static void RejectLinks(string path)
        {
            for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Symlinked authoring paths are not supported: " + current);
        }

        static void RequireSmallFile(string path)
        {
            RejectLinks(path);
            Require(File.Exists(path) && new FileInfo(path).Length <= 65536, "Configuration must be an existing JSON file no larger than 64 KiB.");
        }

        static bool Overlaps(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ||
            a.StartsWith(b.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase) || b.StartsWith(a.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);

        static bool OwnershipChanged(AuthoringProjectConfiguration a, AuthoringProjectConfiguration b) =>
            a.sandboxScene != b.sandboxScene || a.bridgeDemoScene != b.bridgeDemoScene ||
            a.sandboxGeneratedRoot != b.sandboxGeneratedRoot || a.bridgeDemoGeneratedRoot != b.bridgeDemoGeneratedRoot ||
            a.bridgeAssetGeneratedRoot != b.bridgeAssetGeneratedRoot;

        static bool HasOwnedContent(AuthoringProjectConfiguration c) => new[] { c.sandboxScene, c.bridgeDemoScene }
            .Any(p => File.Exists(Path.Combine(ProjectRoot, p))) || new[] { c.sandboxGeneratedRoot, c.bridgeDemoGeneratedRoot, c.bridgeAssetGeneratedRoot }
            .Any(p => Directory.Exists(Path.Combine(ProjectRoot, p)) && Directory.EnumerateFileSystemEntries(Path.Combine(ProjectRoot, p)).Any());

        static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    }
}

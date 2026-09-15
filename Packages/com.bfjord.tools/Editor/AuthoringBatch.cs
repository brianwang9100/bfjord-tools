using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Bwork.Authoring.Editor.BridgeAssets;

namespace Bwork.Authoring.Editor
{
    /// <summary>Public menu and -executeMethod entry point over an explicit command allowlist.</summary>
    public static class AuthoringBatch
    {
        static readonly Dictionary<string, MethodInfo> Commands = new Dictionary<string, MethodInfo>(StringComparer.Ordinal)
        {
            { "bwork_project", Entry(typeof(ProjectContext)) },
            { "bwork_sandbox", Entry(typeof(ToolSandbox)) },
            { "bwork_terrain", Entry(typeof(TerrainCommand)) },
            { "bwork_roads", Entry(typeof(RoadCommand)) },
            { "bwork_structures", Entry(typeof(StructureCommand)) },
            { "bwork_foliage", Entry(typeof(FoliageCommand)) },
            { "bwork_rocks", Entry(typeof(Rocks.RockCommand)) },
            { "bwork_river_scene", Entry(typeof(RiverSceneCommand)) },
            { "bwork_waterfall", Entry(typeof(WaterfallCommand)) },
            { "bwork_water", Entry(typeof(WaterCommand)) },
            { "bwork_water_connected", Entry(typeof(ConnectedWaterCommand)) },
            { "bwork_view", Entry(typeof(SandboxViews)) },
            { "bwork_verify", Entry(typeof(SandboxValidation)) },
            { "bwork_bridge_demo", Entry(typeof(BridgeDemoCommand)) },
            { "bwork_bridge_collection", Entry(typeof(BridgeCollectionDemoCommand)) },
            { "bwork_bridge_asset", Entry(typeof(BridgeAssetCommand)) },
            { "bwork_bridge_surface", Entry(typeof(BridgeSurfaceCommand)) }
        };

        public sealed class Request
        {
            public string command, targetScene;
            public JObject arguments;
        }

        static MethodInfo Entry(Type type) => type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Missing public authoring entry point: " + type.Name);

        public static string[] CommandNames => Commands.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();

        [CliCommand("bwork_request", "Execute the same typed JSON request as the menu and batch entry point, including its target scene.", MainThreadRequired = true)]
        public static object RunRequest([CliArg("requestPath", "JSON request file")] string requestPath)
        {
            string path = FullPath(requestPath);
            ProjectContext.RejectLinks(path);
            if (!File.Exists(path) || new FileInfo(path).Length > 65536) throw new InvalidDataException("Request must exist and be no larger than 64 KiB.");
            return ExecuteJson(File.ReadAllText(path));
        }

        public static Request ValidateRequest(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 65536) throw new InvalidDataException("Request JSON must contain at most 64 KiB.");
            JObject data;
            try
            {
                using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 16 })
                    data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            }
            catch (JsonException e) { throw new InvalidDataException("Invalid command request: " + e.Message, e); }
            if (data.Properties().Any(p => !new[] { "schemaVersion", "command", "arguments", "targetScene" }.Contains(p.Name)) ||
                data["schemaVersion"]?.Type != JTokenType.Integer || (int)data["schemaVersion"] != 1 ||
                data["command"]?.Type != JTokenType.String || !(data["arguments"] is JObject arguments))
                throw new InvalidDataException("Request requires schemaVersion=1, command and an arguments object, with optional targetScene.");
            string command = (string)data["command"];
            if (!Commands.TryGetValue(command, out var method)) throw new InvalidDataException("Unknown command. Available: " + string.Join(", ", CommandNames));
            var parameters = method.GetParameters();
            foreach (var argument in arguments.Properties())
            {
                var parameter = parameters.SingleOrDefault(p => p.Name == argument.Name);
                if (parameter == null) throw new InvalidDataException("Unknown argument for " + command + ": " + argument.Name);
                ConvertArgument(argument.Value, parameter.ParameterType, argument.Name);
            }
            foreach (var parameter in parameters)
                if (!parameter.HasDefaultValue && arguments[parameter.Name] == null)
                    throw new InvalidDataException("Missing argument: " + parameter.Name);
            string targetScene = null;
            if (data["targetScene"] != null)
            {
                if (data["targetScene"].Type != JTokenType.String) throw new InvalidDataException("targetScene must be a string.");
                targetScene = (string)data["targetScene"];
                ProjectContext.ValidateAssetPath(targetScene, true);
            }
            return new Request { command = command, arguments = arguments, targetScene = targetScene };
        }

        public static object ExecuteJson(string json)
        {
            var request = ValidateRequest(json);
            ProjectContext.RequireIdle();
            if (request.targetScene != null)
            {
                var config = ProjectContext.Current;
                if (!new[] { config.sandboxScene, config.bridgeDemoScene }.Concat(config.additionalAllowedScenes).Contains(request.targetScene))
                    throw new InvalidDataException("targetScene is not in the project configuration.");
                if (SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().path != request.targetScene)
                {
                    ProjectContext.RequireSavedScenes();
                    string path = Path.Combine(ProjectContext.ProjectRoot, request.targetScene);
                    ProjectContext.RejectLinks(path);
                    if (!File.Exists(path)) throw new FileNotFoundException("Requested scene does not exist. Run its setup command first.", path);
                    EditorSceneManager.OpenScene(request.targetScene, OpenSceneMode.Single);
                }
            }
            var method = Commands[request.command];
            var values = method.GetParameters().Select(p => request.arguments[p.Name] == null ? p.DefaultValue :
                ConvertArgument(request.arguments[p.Name], p.ParameterType, p.Name)).ToArray();
            try { return method.Invoke(null, values); }
            catch (TargetInvocationException e) when (e.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static object ConvertArgument(JToken value, Type type, string name)
        {
            if (type == typeof(string) && value.Type == JTokenType.String) return (string)value;
            if (type == typeof(bool) && value.Type == JTokenType.Boolean) return (bool)value;
            throw new InvalidDataException("Argument " + name + " requires JSON " + (type == typeof(bool) ? "boolean" : "string") + ".");
        }

        /// <summary>Unity -executeMethod Bwork.Authoring.Editor.AuthoringBatch.Run -bfjordRequest FILE -bfjordResult FILE.</summary>
        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            int status = 1;
            try { status = RunFile(Argument(args, "-bfjordRequest"), Argument(args, "-bfjordResult")) ? 0 : 1; }
            catch (Exception e) { Debug.LogError("BFjord command failed: " + e.Message); }
            if (Application.isBatchMode) EditorApplication.Exit(status);
        }

        [MenuItem("Tools/BFjord Tools/Run JSON Request")]
        public static void RunFromMenu()
        {
            string path = EditorUtility.OpenFilePanel("BFjord command request", ProjectContext.ProjectRoot, "json");
            if (string.IsNullOrEmpty(path)) return;
            string resultPath = EditorUtility.SaveFilePanel("Save BFjord command result to a new file", Path.GetDirectoryName(path),
                Path.GetFileNameWithoutExtension(path) + ".result", "json");
            if (string.IsNullOrEmpty(resultPath)) return;
            RunFile(path, resultPath);
            Debug.Log("BFjord result: " + resultPath);
        }

        [MenuItem("Tools/BFjord Tools/Project Status")]
        public static void ShowProjectStatus() => Debug.Log(JsonConvert.SerializeObject(ProjectContext.Run(), Formatting.Indented));

        public static bool RunFile(string requestPath, string resultPath)
        {
            requestPath = FullPath(requestPath); resultPath = FullPath(resultPath);
            ProjectContext.RejectLinks(requestPath); ProjectContext.RejectLinks(resultPath);
            if (string.Equals(requestPath, resultPath, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Request and result paths must differ.");
            if (!resultPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Result path must end in .json.");
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            // Reserve the result before a command can mutate assets. Keep failed/partial results for diagnosis.
            using var resultFile = new FileStream(resultPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            bool success = false;
            string result;
            try
            {
                if (!File.Exists(requestPath) || new FileInfo(requestPath).Length > 65536) throw new InvalidDataException("Request must exist and be no larger than 64 KiB.");
                var output = ExecuteJson(File.ReadAllText(requestPath));
                result = JsonConvert.SerializeObject(new { schemaVersion = 1, success = true, result = output }, Formatting.Indented);
                success = true;
            }
            catch (Exception e)
            {
                result = JsonConvert.SerializeObject(new { schemaVersion = 1, success = false, error = new { type = e.GetType().Name, message = e.Message } }, Formatting.Indented);
                Debug.LogError("BFjord command failed: " + e.Message);
            }
            using var writer = new StreamWriter(resultFile, new System.Text.UTF8Encoding(false));
            writer.Write(result + "\n");
            return success;
        }

        static string FullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("Explicit request/result path required.");
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(ProjectContext.ProjectRoot, path));
        }

        static string Argument(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            if (index < 0 || index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                throw new InvalidDataException("Required command-line argument: " + key + " FILE");
            return args[index + 1];
        }
    }
}

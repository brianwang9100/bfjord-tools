using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Bwork.Authoring.Editor.BridgeAssets
{
    [Serializable] public sealed class BridgeSurfaceProfile
    {
        public int schemaVersion;
        public string id;
        public BridgeMaterialSource[] materials;
        public BridgeSourceCredit[] sources;
    }

    public sealed class PreparedBridgeSurface
    {
        public BridgeSurfaceProfile Profile;
        public string Directory, Json, Hash;
        public string[] Files;
    }

    public static class BridgeSurfaceContract
    {
        public static PreparedBridgeSurface Prepare(string profilePath, string[] supportedKeys)
        {
            string path = Path.GetFullPath(profilePath);
            BridgeAssetContract.Require(File.Exists(path) && new FileInfo(path).Length <= 1024 * 1024, "Material profile must be a JSON file no larger than 1 MiB.");
            BridgeAssetContract.RejectLinks(path);
            string json = File.ReadAllText(path);
            BridgeSurfaceProfile profile;
            try { profile = JsonConvert.DeserializeObject<BridgeSurfaceProfile>(json, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 16 }); }
            catch (JsonException error) { throw new InvalidDataException("Invalid material profile JSON.", error); }
            BridgeAssetContract.Require(profile != null && profile.schemaVersion == 1, "Unsupported material profile schema.");
            BridgeAssetContract.ValidateId(profile.id, "material profile id");
            BridgeAssetContract.Require(profile.materials != null && profile.materials.Length > 0 && profile.materials.Length <= 8, "A profile requires 1..8 material definitions.");
            foreach (var material in profile.materials) BridgeAssetContract.ValidateMaterial(material);
            var keys = profile.materials.Select(m => m.key).ToArray();
            BridgeAssetContract.Require(keys.Distinct(StringComparer.Ordinal).Count() == keys.Length && keys.All(supportedKeys.Contains), "Material profile keys must be unique and exist on the selected structure.");
            BridgeAssetContract.Require(profile.sources != null && profile.sources.Length > 0 && profile.sources.Length <= 32 && profile.sources.All(s => s != null && !string.IsNullOrWhiteSpace(s.name) && !string.IsNullOrWhiteSpace(s.license)), "Material source/license records are required.");
            var kinds = profile.materials.SelectMany(m => new[] { (m.baseColorPath, 0), (m.normalPath, 1), (m.maskPath, 2) }).Where(p => !string.IsNullOrEmpty(p.Item1)).ToArray();
            BridgeAssetContract.Require(kinds.GroupBy(p => p.Item1, StringComparer.Ordinal).All(g => g.Select(p => p.Item2).Distinct().Count() == 1), "One texture cannot have conflicting color, normal or mask roles.");
            var files = kinds.Select(p => p.Item1).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            string directory = Path.GetDirectoryName(path);
            long bytes = 0;
            foreach (string relative in files)
            {
                string source = BridgeAssetContract.ResolveRelative(directory, relative);
                BridgeAssetContract.Require(File.Exists(source), "Missing surface texture: " + relative);
                BridgeAssetContract.RejectLinks(source);
                long length = new FileInfo(source).Length;
                BridgeAssetContract.Require(length > 0 && length <= 32L * 1024 * 1024, "Surface textures must contain 1 byte..32 MiB.");
                bytes += length;
            }
            BridgeAssetContract.Require(bytes <= 128L * 1024 * 1024, "Surface profile exceeds 128 MiB.");
            return new PreparedBridgeSurface { Profile = profile, Directory = directory, Json = json, Files = files, Hash = BridgeAssetContract.Fingerprint(directory, json, files) };
        }
    }
}

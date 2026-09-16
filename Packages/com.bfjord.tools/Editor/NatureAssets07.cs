using System.Linq;

namespace Bwork.Authoring.Editor
{
    /// <summary>Original Blender asset families; one atlas material and three LODs per model.</summary>
    public static class NatureAssets07
    {
        public static readonly string[] Shore = { "CockleShells_A", "MusselShells_A", "ShellFragments_A", "KelpWrack_A", "BladderWrack_A", "BleachedDriftwood_A" };
        public static readonly string[] Floor = { "ExposedRootFan_A", "OakBirchLeafLitter_A", "PineLitterCones_A", "BrownBoleteCluster_A", "GoldenChanterelleCluster_A", "ShelfFungiDeadwood_A", "MossyFallenBranch_A" };
        public static readonly string[] Trees = { "GiantRedwood_C", "CoastalPine_C", "MatureBeech_C" };
        public static string[] Names => Shore.Concat(Floor).Concat(Trees).ToArray();
        public static bool Contains(string id) => Shore.Contains(id) || Floor.Contains(id) || Trees.Contains(id);
        public static bool IsCanopy(string id) => Trees.Contains(id);
        public static bool IsRigid(string id) => Shore.Contains(id) || Floor.Contains(id);
        public static string Root(string id) => "Assets/BFjord/" + (Shore.Contains(id) ? "ShoreDetail" : Floor.Contains(id) ? "ForestFloorDetail" : "TreeDetail07");
        public static string Texture(string id, string channel)
        {
            string name = Shore.Contains(id) ? "ShoreAtlas_" + (channel == "Atlas" ? "Color" : channel == "Normal" ? "NormalGL" : "Mask") :
                Floor.Contains(id) ? "ForestFloor07" + channel : id + channel;
            return Root(id) + "/Textures/" + name + ".png";
        }
    }
}

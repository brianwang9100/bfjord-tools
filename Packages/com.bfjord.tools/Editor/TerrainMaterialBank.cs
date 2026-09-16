using System;
using UnityEditor;
using UnityEngine;

namespace Bwork.Authoring.Editor
{
    /// <summary>Four layers at a time preserve Terrain Lit's height-blend path.</summary>
    public static class TerrainMaterialBank
    {
        public static readonly string[] Palettes = { "temperate", "woodland", "coast", "cliff" };
        const string Root = "Assets/BFjord/TerrainDetail/Surfaces/";

        public readonly struct Surface
        {
            public readonly Texture2D color, normal, mask;
            public readonly float tileMeters;
            public Surface(Texture2D color, Texture2D normal, Texture2D mask, float tileMeters)
            { this.color = color; this.normal = normal; this.mask = mask; this.tileMeters = tileMeters; }
        }

        public static void Validate(string palette)
        {
            if (Array.IndexOf(Palettes, palette) < 0)
                throw new ArgumentException("Terrain palette must be temperate, woodland, coast or cliff.");
        }

        public static Surface[] Resolve(string palette, Material[] original, Texture2D[] masks, float[] scales)
        {
            Validate(palette);
            var result = new Surface[4];
            for (int i = 0; i < 4; i++) result[i] = new Surface(
                (Texture2D)original[i].GetTexture("_BaseMap"), (Texture2D)original[i].GetTexture("_BumpMap"), masks[i], scales[i]);
            if (palette == "temperate") return result;
            result[3] = Load("RockFace", 1.8f);
            if (palette == "woodland") result[1] = Load("ForestLitter", 2.14f);
            if (palette == "coast") result[1] = Load("CoastalShingle", 15);
            if (palette == "cliff") result[2] = Load("CoastalShingle", 15);
            return result;
        }

        static Surface Load(string id, float meters)
        {
            Texture2D Read(string suffix) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + id + suffix + ".png") ??
                throw new InvalidOperationException("Install the terrain material bank: " + id + suffix);
            return new Surface(Read("_Color"), Read("_Normal"), Read("_Mask"), meters);
        }
    }
}

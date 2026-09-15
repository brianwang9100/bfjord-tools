using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Bwork.Authoring.Editor
{
    /// <summary>Owns changed height samples only. Unrelated later edits survive restore.</summary>
    [Serializable]
    public sealed class TerrainPatchChange
    {
        public string terrainGuid;
        public int resolution;
        public Vector3 origin, size;
        public int[] indices;
        public float[] before, after;
        public int Count => indices?.Length ?? 0;

        public static TerrainPatchChange Create(Terrain terrain, float[,] before, float[,] after)
        {
            int n = terrain.terrainData.heightmapResolution;
            if (before.GetLength(0) != n || before.GetLength(1) != n || after.GetLength(0) != n || after.GetLength(1) != n)
                throw new ArgumentException("Height arrays must match the target grid.");
            var indices = new List<int>();
            var oldValues = new List<float>();
            var newValues = new List<float>();
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            {
                float a = before[z, x], b = after[z, x];
                if (!float.IsFinite(a) || !float.IsFinite(b) || a < 0 || a > 1 || b < 0 || b > 1)
                    throw new ArgumentException("Finite normalized terrain heights are required.");
                if (a == b) continue;
                indices.Add(z * n + x); oldValues.Add(a); newValues.Add(b);
            }
            return new TerrainPatchChange
            {
                terrainGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(terrain.terrainData)),
                resolution = n, origin = terrain.transform.position, size = terrain.terrainData.size,
                indices = indices.ToArray(), before = oldValues.ToArray(), after = newValues.ToArray()
            };
        }

        public void Apply(Terrain terrain) => Write(terrain, false);
        public void Restore(Terrain terrain) => Write(terrain, true);

        public float[,] RestoredCopy(Terrain terrain)
        {
            Validate(terrain);
            var heights = terrain.terrainData.GetHeights(0, 0, resolution, resolution);
            for (int i = 0; i < Count; i++)
            {
                int x = indices[i] % resolution, z = indices[i] / resolution;
                RequireCurrent(heights[z, x], after[i]);
                heights[z, x] = before[i];
            }
            return heights;
        }

        void Write(Terrain terrain, bool restore)
        {
            Validate(terrain);
            if (Count == 0) return;
            int minX = resolution, minZ = resolution, maxX = 0, maxZ = 0;
            foreach (int index in indices)
            {
                int x = index % resolution, z = index / resolution;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z);
            }
            var patch = terrain.terrainData.GetHeights(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
            for (int i = 0; i < Count; i++)
            {
                int x = indices[i] % resolution - minX, z = indices[i] / resolution - minZ;
                RequireCurrent(patch[z, x], restore ? after[i] : before[i]);
                patch[z, x] = restore ? before[i] : after[i];
            }
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, restore ? "Restore terrain edit" : "Apply terrain edit");
            terrain.terrainData.SetHeightsDelayLOD(minX, minZ, patch);
            terrain.terrainData.SyncHeightmap();
            terrain.Flush();
            EditorUtility.SetDirty(terrain.terrainData);
        }

        void Validate(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null ||
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(terrain.terrainData)) != terrainGuid ||
                terrain.terrainData.heightmapResolution != resolution || terrain.transform.position != origin ||
                terrain.terrainData.size != size || terrain.transform.rotation != Quaternion.identity || terrain.transform.lossyScale != Vector3.one)
                throw new InvalidOperationException("The target Terrain changed identity, transform or grid.");
            if (indices == null || before == null || after == null || indices.Length != before.Length || indices.Length != after.Length)
                throw new InvalidOperationException("The terrain edit record is incomplete.");
            int previous = -1;
            for (int i = 0; i < Count; i++)
            {
                if (indices[i] <= previous || indices[i] >= resolution * resolution ||
                    !float.IsFinite(before[i]) || !float.IsFinite(after[i]) || before[i] < 0 || before[i] > 1 || after[i] < 0 || after[i] > 1)
                    throw new InvalidOperationException("The terrain edit record has invalid samples.");
                previous = indices[i];
            }
        }

        static void RequireCurrent(float current, float expected)
        {
            // TerrainData stores heights at native precision; tolerate only one storage quantum.
            if (Mathf.Abs(current - expected) > 1f / 65535f)
                throw new InvalidOperationException("A later edit changed an owned terrain sample. Restore the later edit first.");
        }
    }
}

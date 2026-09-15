using System;
using System.Collections.Generic;
using System.Linq;
using Bwork.FjordCoast.Roads.Editor;
using UnityEditor;
using UnityEngine;

namespace Bwork.Authoring.Editor
{
    [Serializable] public sealed class TerrainHoleChange
    {
        public string terrainGuid;public int resolution;public Vector3 origin,size;
        public int[] cells;public bool[] before,after;public int Count=>cells?.Length??0;
        public static TerrainHoleChange Create(Terrain terrain,bool[,] before,bool[,] after)
        {
            int n=terrain.terrainData.holesResolution;
            if(before.GetLength(0)!=n||before.GetLength(1)!=n||after.GetLength(0)!=n||after.GetLength(1)!=n)throw new ArgumentException("Hole grids differ from Terrain.");
            var indices=new List<int>();var old=new List<bool>();var next=new List<bool>();
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)if(before[z,x]!=after[z,x]){indices.Add(z*n+x);old.Add(before[z,x]);next.Add(after[z,x]);}
            if(indices.Count>8192)throw new ArgumentException("Structure holes exceed 8192 changed cells.");
            return new TerrainHoleChange{terrainGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(terrain.terrainData)),resolution=n,origin=terrain.transform.position,size=terrain.terrainData.size,cells=indices.ToArray(),before=old.ToArray(),after=next.ToArray()};
        }
        public bool[,] RestoredCopy(Terrain terrain)
        {
            Validate(terrain);var current=terrain.terrainData.GetHoles(0,0,resolution,resolution);
            if(Count==0)return current;
            if(before.All(v=>v)&&after.All(v=>!v))return FjordTunnelHoleOwnership.Restore(current,cells);
            for(int i=0;i<Count;i++){int x=cells[i]%resolution,z=cells[i]/resolution;Require(current[z,x]==after[i]);current[z,x]=before[i];}return current;
        }
        public void Apply(Terrain terrain)=>Write(terrain,false);
        public void Restore(Terrain terrain)=>Write(terrain,true);
        void Write(Terrain terrain,bool restore)
        {
            Validate(terrain);if(Count==0)return;
            int x0=cells.Min(i=>i%resolution),x1=cells.Max(i=>i%resolution),z0=cells.Min(i=>i/resolution),z1=cells.Max(i=>i/resolution);
            var patch=terrain.terrainData.GetHoles(x0,z0,x1-x0+1,z1-z0+1);
            for(int i=0;i<Count;i++){int x=cells[i]%resolution-x0,z=cells[i]/resolution-z0;Require(patch[z,x]==(restore?after[i]:before[i]));patch[z,x]=restore?before[i]:after[i];}
            Undo.RegisterCompleteObjectUndo(terrain.terrainData,"Structure terrain holes");terrain.terrainData.SetHoles(x0,z0,patch);terrain.Flush();EditorUtility.SetDirty(terrain.terrainData);
        }
        void Validate(Terrain terrain)
        {
            if(terrain==null||AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(terrain.terrainData))!=terrainGuid||terrain.terrainData.holesResolution!=resolution||terrain.transform.position!=origin||terrain.terrainData.size!=size||terrain.transform.rotation!=Quaternion.identity||terrain.transform.lossyScale!=Vector3.one)throw new InvalidOperationException("Structure Terrain target changed.");
            if(cells==null||before==null||after==null||cells.Length!=before.Length||cells.Length!=after.Length||Count>8192)throw new InvalidOperationException("Invalid hole ownership receipt.");
            int prior=-1;foreach(int i in cells){if(i<=prior||i>=resolution*resolution)throw new InvalidOperationException("Invalid owned hole indices.");prior=i;}
        }
        static void Require(bool same){if(!same)throw new InvalidOperationException("A later edit changed an owned structure hole; restore that edit first.");}
    }
}

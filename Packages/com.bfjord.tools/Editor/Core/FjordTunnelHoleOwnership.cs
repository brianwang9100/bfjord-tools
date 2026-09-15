using System;
using System.Collections.Generic;

namespace Bwork.FjordCoast.Roads.Editor
{
    // Pure ownership operation, shared by reset preflight and its small regression case.
    public static class FjordTunnelHoleOwnership
    {
        public static bool[,] Restore(bool[,] current,int[] cells)
        {
            if(current==null||cells==null||cells.Length==0||cells.Length>8192)throw new ArgumentException("Bounded recorded tunnel cells required.");
            int rows=current.GetLength(0),columns=current.GetLength(1);var seen=new HashSet<int>();
            foreach(int index in cells)
            {
                if(index<0||index>=(long)rows*columns||!seen.Add(index))throw new InvalidOperationException("Tunnel ownership cell bounds/duplicate.");
                if(current[index/columns,index%columns])throw new InvalidOperationException("An owned tunnel hole was changed; reset rejected.");
            }
            var result=(bool[,])current.Clone();foreach(int index in cells)result[index/columns,index%columns]=true;return result;
        }
        public static string RunChecks()
        {
            var original=new bool[5,5];for(int z=0;z<5;z++)for(int x=0;x<5;x++)original[z,x]=true;
            original[4,4]=false;var cut=(bool[,])original.Clone();cut[1,2]=false;cut[2,2]=false;
            var restored=Restore(cut,new[]{7,12});
            for(int z=0;z<5;z++)for(int x=0;x<5;x++)if(restored[z,x]!=original[z,x])throw new Exception("Reset changed an unrelated hole/surface.");
            if(cut[1,2]||cut[2,2])throw new Exception("Reset preflight mutated its input.");
            var recut=(bool[,])restored.Clone();recut[1,2]=false;recut[2,2]=false;var second=Restore(recut,new[]{7,12});
            if(second[4,4]||!second[1,2]||!second[2,2])throw new Exception("Repeated cut/reset changed ownership.");
            Reject(()=>Restore(restored,new[]{7,12}));Reject(()=>Restore(cut,new[]{7,7}));Reject(()=>Restore(cut,new[]{7,99}));
            var changed=(bool[,])cut.Clone();changed[2,2]=true;Reject(()=>Restore(changed,new[]{7,12}));if(changed[1,2])throw new Exception("Rejected reset partially mutated cells.");
            return "FJORD_TUNNEL_OWNERSHIP_PASS restore/recut/unrelated-hole/changed-state/duplicate/bounds";
        }
        static void Reject(Action action){try{action();}catch(InvalidOperationException){return;}throw new Exception("Expected tunnel ownership rejection.");}
    }
}

using System;
using Bwork.FjordCoast.Junctions;

namespace Bwork.WorldAuthoring
{
    public static class SandboxRoadExample
    {
        // Original 512m terrain; deliberately steeper than the fitted road near the hill.
        public static float Ground(float x,float z)
        { float dx=(x-330)/75,dz=(z-255)/90;return 8+62*(float)Math.Exp(-dx*dx-dz*dz)+4*(float)Math.Sin(z/65); }
        public static SandboxRoadRecipe Recipe(bool crossroads=false)
        {
            var roads=new System.Collections.Generic.List<SandboxRoad>
            {
                Road("west","asphalt",new[]{new V3(256,0,256),new V3(195,0,256),new V3(130,0,235),new V3(48,0,185)}),
                Road("east","asphalt",new[]{new V3(256,0,256),new V3(317,0,256),new V3(387,0,277),new V3(464,0,338)}),
                Road("south","dirt",new[]{new V3(256,0,256),new V3(256,0,195),new V3(275,0,130),new V3(325,0,48)})
            };
            if(crossroads)roads.Add(Road("north","dirt",new[]{new V3(256,0,256),new V3(256,0,317),new V3(235,0,385),new V3(180,0,464)}));
            return new SandboxRoadRecipe{Roads=roads.ToArray()};
        }
        static SandboxRoad Road(string id,string surface,V3[] controls)=>new SandboxRoad
        {Id=id,SourceId="original-sandbox-"+id,StartNode="hill-junction",EndNode=id+"-end",Surface=surface,Width=8,Controls=controls};
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Bwork.FjordCoast.Junctions;
using Bwork.FjordCoast.Roads.Editor;

namespace Bwork.WorldAuthoring
{
    // Pure authoring data. These paths are candidates, never an accepted native route catalog.
    [Serializable] public sealed class SandboxRoadRecipe
    {
        public SandboxRoad[] Roads;
        public float MinX, MinZ, Size = 512, SampleSpacing = 2, MaximumGrade = .24f;
    }
    [Serializable] public sealed class SandboxRoad
    {
        public string Id, SourceId, StartNode, EndNode, Surface = "asphalt";
        public float Width = 8;
        public V3[] Controls;
        public bool UseControlHeights;
        public SandboxSpan[] Spans = Array.Empty<SandboxSpan>();
    }
    [Serializable] public sealed class SandboxSpan
    {
        public string Id, Kind; // "bridge" or "tunnel"; decoration belongs to the caller.
        public float StartMeters, EndMeters;
    }
    public sealed class SandboxPath
    {
        public SandboxRoad Source;
        public V3[] Points;
        public float[] Stations;
        public int FirstVisible, LastVisible;
    }
    public sealed class SandboxMesh
    {
        public string Id, SourceId, Surface;
        public Vertex[] Vertices;
        // Four bands: main surface, edge, near shoulder, outer earth blend.
        public int[][] Triangles;
        public float Width;
    }
    public sealed class SandboxRoadResult
    {
        public SandboxPath[] Paths;
        public SandboxMesh[] Meshes;
        public SandboxEarthworkPatch Earthwork;
        public double MaximumGradePercent, MaximumSurfaceGradePercent, MaximumHeightChange;
    }
    public readonly struct SandboxEarthworkSample
    {
        public readonly bool Affected;
        public readonly float Height, Mask, CutFill;
        public SandboxEarthworkSample(bool affected,float height,float mask,float original)
        { Affected=affected;Height=height;Mask=mask;CutFill=height-original; }
    }
    // Finite spatial index of the actual mesh footprint, shared by Terrain edits and planting exclusions.
    public sealed class SandboxEarthworkPatch
    {
        readonly Func<float,float,float> ground;
        internal readonly List<(Vertex a,Vertex b,Vertex c)> Faces = new List<(Vertex,Vertex,Vertex)>();
        readonly Dictionary<(int,int),List<(Vertex,Vertex,Vertex,float)>> cells = new Dictionary<(int,int),List<(Vertex,Vertex,Vertex,float)>>();
        readonly float minX,minZ,maxX,maxZ;
        internal SandboxEarthworkPatch(SandboxRoadRecipe recipe,Func<float,float,float> sampler)
        {ground=sampler;minX=recipe.MinX;minZ=recipe.MinZ;maxX=minX+recipe.Size;maxZ=minZ+recipe.Size;}
        internal void Add(Vertex a,Vertex b,Vertex c,float width)
        {
            Faces.Add((a,b,c));
            int x0=Cell(Math.Max(minX,Math.Min(a.Position.X,Math.Min(b.Position.X,c.Position.X)))),x1=Cell(Math.Min(maxX,Math.Max(a.Position.X,Math.Max(b.Position.X,c.Position.X))));
            int z0=Cell(Math.Max(minZ,Math.Min(a.Position.Z,Math.Min(b.Position.Z,c.Position.Z)))),z1=Cell(Math.Min(maxZ,Math.Max(a.Position.Z,Math.Max(b.Position.Z,c.Position.Z))));
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {if(!cells.TryGetValue((x,z),out var list)){list=new List<(Vertex,Vertex,Vertex,float)>();cells.Add((x,z),list);}list.Add((a,b,c,width));}
        }
        public SandboxEarthworkSample Sample(float x,float z)
        {
            float original=ground(x,z);SandboxRoadAuthoring.Need(float.IsFinite(original),"Finite ground height required.");
            if(x<minX||z<minZ||x>maxX||z>maxZ||!cells.TryGetValue((Cell(x),Cell(z)),out var faces))return new SandboxEarthworkSample(false,original,0,original);
            foreach(var face in faces)
            {
                var a=face.Item1;var b=face.Item2;var c=face.Item3;double area=Area(a.Position,b.Position,c.Position.X,c.Position.Z);
                double u=Area(b.Position,c.Position,x,z)/area,v=Area(c.Position,a.Position,x,z)/area,w=1-u-v;
                if(u<-.000001||v<-.000001||w<-.000001)continue;
                float lateral=(float)(Math.Abs(a.Mask.X)*u+Math.Abs(b.Mask.X)*v+Math.Abs(c.Mask.X)*w);
                float t=Math.Clamp((lateral-face.Item4*.5f)/(face.Item4*1.5f),0,1),mask=1-t*t*(3-2*t);
                float height=(float)(a.Position.Y*u+b.Position.Y*v+c.Position.Y*w)-.03f;
                return new SandboxEarthworkSample(true,height,mask,original);
            }
            return new SandboxEarthworkSample(false,original,0,original);
        }
        static int Cell(float x)=>(int)Math.Floor(x/16);
        static double Area(V3 a,V3 b,float x,float z)=>((double)b.X-a.X)*(z-a.Z)-((double)b.Z-a.Z)*(x-a.X);
    }
    public static class SandboxRoadAuthoring
    {
        static readonly float[] Rows={-16,-6.8f,-4,-2.8f,0,2.8f,4,6.8f,16};
        public static SandboxRoadResult Build(SandboxRoadRecipe recipe,Func<float,float,float> ground)
        {
            Need(recipe!=null&&ground!=null&&recipe.Roads!=null&&recipe.Roads.Length>0&&recipe.Roads.Length<=32,"One bounded recipe and ground callback required.");
            Need(float.IsFinite(recipe.MinX)&&float.IsFinite(recipe.MinZ)&&recipe.Size>0&&recipe.Size<=2048&&recipe.SampleSpacing>=1&&recipe.SampleSpacing<=4&&recipe.MaximumGrade>0&&recipe.MaximumGrade<=.25f,"Invalid sandbox or fitting bounds.");
            Need(recipe.Roads.All(r=>r!=null&&!string.IsNullOrEmpty(r.Id))&&recipe.Roads.Select(r=>r.Id).Distinct().Count()==recipe.Roads.Length,"Unique road IDs required.");
            var paths=recipe.Roads.Select(r=>Resample(r,recipe,ground)).ToArray();
            var nodes=new Dictionary<string,List<(SandboxPath path,bool start)>>();
            foreach(var path in paths)foreach(bool start in new[]{true,false})
            {
                string id=start?path.Source.StartNode:path.Source.EndNode;Need(!string.IsNullOrEmpty(id),"Explicit endpoint node IDs required.");
                if(!nodes.TryGetValue(id,out var arms)){arms=new List<(SandboxPath,bool)>();nodes.Add(id,arms);}arms.Add((path,start));
            }
            foreach(var node in nodes)
            {
                Need(node.Value.Count==1||node.Value.Count==3||node.Value.Count==4,"Use one-, three- or four-arm nodes; combine two-arm segments into one road.");
                var p=Endpoint(node.Value[0]);
                Need(node.Value.All(a=>Distance(Endpoint(a),p)<.001f),"Shared node endpoints must coincide exactly in XZ.");
                Need(node.Value.All(a=>Math.Abs(a.path.Source.Width-node.Value[0].path.Source.Width)<.0001f),"Connected arms must share width in this adapter.");
            }
            var fitted=FjordRoadProfiles.Fit(paths.Select(p=>new FjordRoadProfiles.Route{id=p.Source.Id,points=p.Points.Select(v=>new FjordRoadProfiles.Point(v.X,v.Y,v.Z)).ToArray()}).ToArray(),recipe.MaximumGrade);
            for(int i=0;i<paths.Length;i++)paths[i].Points=fitted.routes[i].points.Select(p=>new V3((float)p.x,(float)p.y,(float)p.z)).ToArray();
            foreach(var node in nodes.Where(n=>n.Value.Count>1))foreach(var arm in node.Value)
            {
                var p=arm.path;float trim=48*p.Source.Width/8;
                int index=arm.start?Array.FindIndex(p.Stations,s=>s>=trim):Array.FindLastIndex(p.Stations,s=>s<=p.Stations.Last()-trim);
                Need(index>0&&index<p.Points.Length-1,"Road too short for junction trim.");
                if(arm.start)p.FirstVisible=index;else p.LastVisible=index;
            }
            var meshes=new List<SandboxMesh>();var patch=new SandboxEarthworkPatch(recipe,ground);
            foreach(var path in paths)
            {
                Need(path.LastVisible>path.FirstVisible,"Junction trims consume road.");
                var vertices=new List<Vertex>();var triangles=Enumerable.Range(0,4).Select(_=>new List<int>()).ToArray();
                for(int i=path.FirstVisible;i<=path.LastVisible;i++)vertices.AddRange(Section(path,i,ground));
                for(int i=path.FirstVisible;i<path.LastVisible;i++)
                {
                    bool span=path.Source.Spans.Any(s=>path.Stations[i]>=s.StartMeters&&path.Stations[i+1]<=s.EndMeters);
                    for(int row=0;row<8;row++)
                    {
                        int band=row==3||row==4?0:row==2||row==5?1:row==1||row==6?2:3;
                        if(span&&band>=2)continue;int a=(i-path.FirstVisible)*9+row,b=a+9;
                        int[] face={a,b,a+1,a+1,b,b+1};
                        for(int k=0;k<6;k+=3)
                        {var p=vertices[face[k]].Position;var q=vertices[face[k+1]].Position;var r=vertices[face[k+2]].Position;Need(((double)q.X-p.X)*(r.Z-p.Z)-((double)q.Z-p.Z)*(r.X-p.X)<0,"Road ribbon folds; widen curve radius or narrow the road.");}
                        triangles[band].AddRange(face);
                        if(!span)for(int k=0;k<6;k+=3)patch.Add(vertices[face[k]],vertices[face[k+1]],vertices[face[k+2]],path.Source.Width);
                    }
                }
                meshes.Add(new SandboxMesh{Id=path.Source.Id,SourceId=path.Source.SourceId,Surface=path.Source.Surface,Width=path.Source.Width,Vertices=vertices.ToArray(),Triangles=triangles.Select(t=>t.ToArray()).ToArray()});
            }
            foreach(var node in nodes.Where(n=>n.Value.Count>1))
            {
                var origin=Endpoint(node.Value[0]);float scale=node.Value[0].path.Source.Width/8;
                V3 Local(V3 p)=>new V3((p.X-origin.X)/scale,(p.Y-origin.Y)/scale,(p.Z-origin.Z)/scale);
                V3 World(V3 p)=>new V3(p.X*scale+origin.X,p.Y*scale+origin.Y,p.Z*scale+origin.Z);
                Vertex Normalize(Vertex v){var p=Local(v.Position);return new Vertex(p,v.Normal,new V2(p.X,p.Z),new V2(v.Mask.X/scale,v.Mask.Y/scale));}
                var mouths=node.Value.Select(arm=>
                {
                    var path=arm.path;int index=arm.start?path.FirstVisible:path.LastVisible;
                    Need(!path.Source.Spans.Any(s=>arm.start?s.StartMeters<=path.Stations[index]:s.EndMeters>=path.Stations[index]),"Spans may not overlap junction patches.");
                    var cross=Section(path,index,ground);var d=Tangent(path.Points,index);
                    if(!arm.start){Array.Reverse(cross);d=new V3(-d.X,-d.Y,-d.Z);}
                    var native=arm.start?path.Points.Take(index+1).ToArray():path.Points.Skip(index).Reverse().ToArray();
                    return new Mouth{Id=path.Source.Id,Outward=d,CrossSection=cross.Select(Normalize).ToArray(),NativePath=native.Select(Local).ToArray()};
                }).ToArray();
                var junction=FjordThreeArmJunction.Build(new V3(0,0,0),mouths,(x,z)=>(ground(origin.X+x*scale,origin.Z+z*scale)+.03f-origin.Y)/scale,16,true,true,true,true,true);
                var mesh=new SandboxMesh{Id="junction-"+node.Key,SourceId=node.Key,Surface=node.Value.Select(a=>a.path.Source.Surface).OrderBy(s=>s=="asphalt"?0:1).First(),Width=scale*8,Vertices=junction.Vertices.Select(v=>{var p=World(v.Position);return new Vertex(p,v.Normal,new V2(p.X,p.Z),new V2(v.Mask.X*scale,v.Mask.Y*scale));}).ToArray(),Triangles=junction.Triangles};
                meshes.Add(mesh);foreach(var t in mesh.Triangles)for(int i=0;i<t.Length;i+=3)patch.Add(mesh.Vertices[t[i]],mesh.Vertices[t[i+1]],mesh.Vertices[t[i+2]],mesh.Width);
            }
            double surfaceGrade = ValidatePavement(meshes);
            return new SandboxRoadResult{Paths=paths,Meshes=meshes.ToArray(),Earthwork=patch,MaximumGradePercent=fitted.maximumGradePercent,MaximumSurfaceGradePercent=surfaceGrade*100,MaximumHeightChange=fitted.maximumChangeMeters};
        }

        // Route grade alone cannot detect transverse ridges or narrow tilted triangles.
        // Admission happens before Terrain/asset mutation and covers pavement plus edges.
        public static double ValidatePavement(IEnumerable<SandboxMesh> meshes)
        {
            double maximum = 0;
            foreach (var mesh in meshes) foreach (var triangles in mesh.Triangles.Take(2))
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = mesh.Vertices[triangles[i]].Position; var b = mesh.Vertices[triangles[i+1]].Position; var c = mesh.Vertices[triangles[i+2]].Position;
                    double ux = (double)b.X-a.X, uy = (double)b.Y-a.Y, uz = (double)b.Z-a.Z;
                    double vx = (double)c.X-a.X, vy = (double)c.Y-a.Y, vz = (double)c.Z-a.Z;
                    double nx = uy*vz-uz*vy, ny = uz*vx-ux*vz, nz = ux*vy-uy*vx;
                    Need(ny > 1e-8, "Pavement contains a folded or degenerate projected triangle.");
                    double slope = Math.Sqrt(nx*nx+nz*nz)/ny;
                    Need(double.IsFinite(slope) && slope <= FjordJunctionPathConformance.MaximumPavementSlope + .00002,
                        "Pavement surface exceeds the 30% slope limit; adjust the road recipe.");
                    maximum = Math.Max(maximum, slope);
                }
            return maximum;
        }
        static SandboxPath Resample(SandboxRoad road,SandboxRoadRecipe recipe,Func<float,float,float> ground)
        {
            Need(!string.IsNullOrEmpty(road.SourceId)&&(road.Surface=="asphalt"||road.Surface=="gravel"||road.Surface=="dirt")&&road.Controls!=null&&road.Controls.Length>=2&&road.Controls.Length<=64&&road.Width>=3&&road.Width<=12,"Road requires provenance, surface, bounded controls and 3..12m width.");
            Need(road.Controls.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z)&&p.X>=recipe.MinX&&p.X<=recipe.MinX+recipe.Size&&p.Z>=recipe.MinZ&&p.Z<=recipe.MinZ+recipe.Size),"Control outside finite sandbox.");
            var dense=new List<V3>{road.Controls[0]};
            for(int i=0;i<road.Controls.Length-1;i++)
            {
                var a=road.Controls[Math.Max(0,i-1)];var b=road.Controls[i];var c=road.Controls[i+1];var d=road.Controls[Math.Min(road.Controls.Length-1,i+2)];
                Need(Distance(b,c)>1,"Repeated/too-close road control.");int steps=(int)Math.Ceiling((Distance(a,b)+Distance(b,c)+Distance(c,d))*2);
                for(int j=1;j<=steps;j++){float t=j/(float)steps;float At(float p,float q,float r,float s)=>.5f*((2*q)+(-p+r)*t+(2*p-5*q+4*r-s)*t*t+(-p+3*q-3*r+s)*t*t*t);dense.Add(new V3(At(a.X,b.X,c.X,d.X),At(a.Y,b.Y,c.Y,d.Y),At(a.Z,b.Z,c.Z,d.Z)));}
            }
            var length=new float[dense.Count];for(int i=1;i<length.Length;i++)length[i]=length[i-1]+Distance(dense[i-1],dense[i]);
            Need(length.Last()<=8192,"Road length bound.");var stations=new SortedSet<float>{0,length.Last()};for(float s=recipe.SampleSpacing;s<length.Last();s+=recipe.SampleSpacing)stations.Add(s);
            Need(road.Spans!=null&&road.Spans.Length<=32,"Bounded explicit spans required.");
            foreach(var span in road.Spans){Need(span!=null&&!string.IsNullOrEmpty(span.Id)&&(span.Kind=="bridge"||span.Kind=="tunnel")&&span.StartMeters>=0&&span.EndMeters>span.StartMeters&&span.EndMeters<=length.Last(),"Invalid explicit span.");stations.Add(span.StartMeters);stations.Add(span.EndMeters);}
            var points=new List<V3>();int cursor=1;
            foreach(float s in stations)
            {
                while(cursor<length.Length-1&&length[cursor]<s)cursor++;float t=(s-length[cursor-1])/(length[cursor]-length[cursor-1]);var p=Lerp(dense[cursor-1],dense[cursor],t);
                Need(p.X>=recipe.MinX&&p.X<=recipe.MinX+recipe.Size&&p.Z>=recipe.MinZ&&p.Z<=recipe.MinZ+recipe.Size,"Curve exits sandbox.");
                float y=road.UseControlHeights?p.Y:ground(p.X,p.Z)+.11f;Need(float.IsFinite(y),"Finite ground/guide height required.");points.Add(new V3(p.X,y,p.Z));
            }
            return new SandboxPath{Source=road,Points=points.ToArray(),Stations=stations.ToArray(),FirstVisible=0,LastVisible=points.Count-1};
        }
        static Vertex[] Section(SandboxPath path,int index,Func<float,float,float> ground)
        {
            var p=path.Points[index];var d=Tangent(path.Points,index);float scale=path.Source.Width/8;
            return Rows.Select(row=>
            {
                float lateral=row*scale,x=p.X+d.Z*lateral,z=p.Z-d.X*lateral;
                // The near verge reaches the natural ground instead of carrying a raised
                // platform across the full earthwork envelope. Outer rows retain the same
                // bounded topology, conformance, exclusion and restoration ownership.
                float t=Math.Clamp((Math.Abs(row)-4)/2.8f,0,1),blend=t*t*(3-2*t);
                float crown=.08f*scale*Math.Min(Math.Abs(row)/2.8f,1),y=(p.Y-crown)*(1-blend)+(ground(x,z)+.03f)*blend;
                Need(float.IsFinite(y),"Finite shoulder ground height required.");float normalLength=(float)Math.Sqrt(1+d.Y*d.Y);
                var v=new V3(x,y,z);return new Vertex(v,new V3(-d.X*d.Y/normalLength,1/normalLength,-d.Z*d.Y/normalLength),new V2(x,z),new V2(lateral,path.Stations[index]));
            }).ToArray();
        }
        static V3 Tangent(V3[] points,int i){var a=points[Math.Max(0,i-1)];var b=points[Math.Min(points.Length-1,i+1)];float d=Distance(a,b);Need(d>.001f,"Degenerate curve tangent.");return new V3((b.X-a.X)/d,(b.Y-a.Y)/d,(b.Z-a.Z)/d);}
        static V3 Endpoint((SandboxPath path,bool start) arm)=>arm.path.Points[arm.start?0:arm.path.Points.Length-1];
        static V3 Lerp(V3 a,V3 b,float t)=>new V3(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);
        static float Distance(V3 a,V3 b)=>(float)Math.Sqrt(((double)b.X-a.X)*(b.X-a.X)+((double)b.Z-a.Z)*(b.Z-a.Z));
        internal static void Need(bool ok,string message){if(!ok)throw new ArgumentException(message);}
    }
}

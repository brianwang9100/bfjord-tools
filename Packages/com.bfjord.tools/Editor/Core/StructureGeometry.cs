using System;
using System.Collections.Generic;
using System.Linq;
using Bwork.FjordCoast.Junctions;
using Bwork.FjordCoast.Roads.Editor;
using Bwork.WorldAuthoring;

namespace Bwork.Authoring.Editor
{
    public sealed class StructureSpec
    {
        public string Id,Kind,Style="concrete",SourceId,PortalStyle="none";
        public V3[] Controls;
        public float Width=8,Clearance=5,Thickness=.5f,ParapetHeight=1.1f,SupportSpacing=20,PortalDepth=2,MinimumCover=.5f,ApproachLength=40;
        public float HoleCellMargin;
        public float? WaterLevel;
    }
    public sealed class StructurePart {public string Name,Material;public V3[] Vertices;public V2[] UV;public int[] Triangles;public bool Closed=true;}
    public sealed class PlacedStructure
    {
        public StructureSpec Spec;public V3[] Path;public float[] Stations;public StructurePart[] Parts;
        public double MaximumGradePercent;public float MinimumUndersideClearance;
        public SandboxEarthworkPatch ApproachEarthwork;
        public bool CutHole(float x,float z,float ground,float gridMargin)
        {
            if(Spec.Kind!="tunnel")return false;
            float nearest=float.PositiveInfinity,floor=0,station=0;
            for(int i=1;i<Path.Length;i++)
            {
                var a=Path[i-1];var b=Path[i];float dx=b.X-a.X,dz=b.Z-a.Z;
                float t=Math.Clamp(((x-a.X)*dx+(z-a.Z)*dz)/(dx*dx+dz*dz),0,1),px=a.X+t*dx,pz=a.Z+t*dz;
                float distance=(x-px)*(x-px)+(z-pz)*(z-pz);if(distance>=nearest)continue;
                nearest=distance;floor=a.Y+(b.Y-a.Y)*t;station=Stations[i-1]+(Stations[i]-Stations[i-1])*t;
            }
            // Interior ground above the arch remains a roof. Portal cells and ground
            // intersecting the lining are opened using ordinary Terrain holes.
            return nearest<=Math.Pow(Spec.Width/2+Spec.Thickness+gridMargin,2)&&ground>floor-.3f&&
                (station<Spec.PortalDepth+gridMargin||station>Stations.Last()-Spec.PortalDepth-gridMargin||ground<floor+Spec.Clearance+Spec.Thickness+Spec.MinimumCover);
        }
    }
    public static class StructureGeometry
    {
        public static StructureSpec[] Example(Func<float,float,float> ground)
        {
            V3 P(float x,float z,float offset)=>new V3(x,ground(x,z)+offset,z);
            var entry=P(290,165,.15f);var exit=P(429,169,.15f);
            return new[]{
                new StructureSpec{Id="curved-coast-bridge",SourceId="original-sandbox-bridge",Kind="bridge",Style="stone",Clearance=4,Controls=new[]{P(85,315,6),P(125,330,6),P(175,336,6),P(218,325,6)}},
                new StructureSpec{Id="hill-tunnel",SourceId="original-sandbox-tunnel",Kind="tunnel",Style="concrete",Clearance=5.5f,ApproachLength=55,
                    Controls=new[]{entry,new V3(332,entry.Y+(exit.Y-entry.Y)*.3f,157),new V3(383,entry.Y+(exit.Y-entry.Y)*.67f,165),exit}}};
        }
        public static PlacedStructure Build(StructureSpec spec,Func<float,float,float> ground,float minX=0,float minZ=0,float size=512)
        {
            Need(spec!=null&&(spec.Kind=="bridge"||spec.Kind=="tunnel")&&!string.IsNullOrEmpty(spec.Id)&&!string.IsNullOrEmpty(spec.SourceId),"Explicit bridge/tunnel identity and source required.");
            Need((spec.Style=="concrete"||spec.Style=="stone")&&spec.Width>=3&&spec.Width<=12&&spec.Clearance>=2&&spec.Clearance<=15&&spec.Thickness>=.25f&&spec.Thickness<=2&&spec.ParapetHeight>=.6f&&spec.ParapetHeight<=2&&spec.SupportSpacing>=8&&spec.SupportSpacing<=50&&spec.PortalDepth>=1&&spec.PortalDepth<=6&&spec.MinimumCover>=0&&spec.MinimumCover<=10&&spec.ApproachLength>=12&&spec.ApproachLength<=100&&spec.HoleCellMargin>=0&&spec.HoleCellMargin<=4,"Bounded structure dimensions/style required.");
            Need(spec.PortalStyle=="none"||spec.PortalStyle=="masonry"&&spec.Kind=="tunnel"&&Math.Abs(spec.Width-8)<.001f&&Math.Abs(spec.Clearance-5.5f)<.001f,"Masonry portals require an 8m bore and 5.5m clearance; assets are never stretched.");
            var road=new SandboxRoad{Id=spec.Id,SourceId=spec.SourceId,StartNode=spec.Id+"-start",EndNode=spec.Id+"-end",Width=spec.Width,Controls=spec.Controls,UseControlHeights=true};
            var sampled=SandboxRoadAuthoring.Build(new SandboxRoadRecipe{MinX=minX,MinZ=minZ,Size=size,Roads=new[]{road}},ground).Paths[0];
            var points=sampled.Points;
            if(spec.Kind=="bridge")
            {
                var fitted=FjordRoadProfiles.Fit(new[]{new FjordRoadProfiles.Route{id=spec.Id,points=points.Select(p=>new FjordRoadProfiles.Point(p.X,p.Y,p.Z)).ToArray()}},.24,(id,i)=>
                {
                    var p=points[i];var right=Right(points,i);double level=double.NegativeInfinity;
                    for(float x=-spec.Width/2;x<=spec.Width/2+.01f;x+=spec.Width/4)level=Math.Max(level,ground(p.X+right.X*x,p.Z+right.Z*x));
                    if(spec.WaterLevel.HasValue){Need(float.IsFinite(spec.WaterLevel.Value),"Finite explicit water level required.");level=Math.Max(level,spec.WaterLevel.Value);}
                    return level+spec.Clearance+spec.Thickness;
                });
                points=fitted.routes[0].points.Select(p=>new V3((float)p.x,(float)p.y,(float)p.z)).ToArray();
            }
            var output=new PlacedStructure{Spec=spec,Path=points,Stations=sampled.Stations,MinimumUndersideClearance=float.PositiveInfinity,
                ApproachEarthwork=new SandboxEarthworkPatch(new SandboxRoadRecipe{MinX=minX,MinZ=minZ,Size=size},ground)};
            for(int i=1;i<points.Length;i++)output.MaximumGradePercent=Math.Max(output.MaximumGradePercent,Math.Abs(points[i].Y-points[i-1].Y)/Flat(points[i],points[i-1])*100);
            Need(output.MaximumGradePercent<=25.001,"Structure grade exceeds 25%.");
            var parts=new List<StructurePart>();
            parts.Add(Sweep(spec.Id+" deck","asphalt",points,Rect(-spec.Width/2,spec.Width/2,-spec.Thickness,0)));
            if(spec.Kind=="bridge")
            {
                foreach(int side in new[]{-1,1})parts.Add(Sweep(spec.Id+" parapet "+side,spec.Style,points,Rect(side*spec.Width/2-.22f,side*spec.Width/2+.22f,0,spec.ParapetHeight)));
                for(float s=0;s<=sampled.Stations.Last();s+=spec.SupportSpacing)
                {
                    var p=At(points,sampled.Stations,s);int index=Math.Max(0,Array.FindLastIndex(sampled.Stations,a=>a<=s));var right=Right(points,index);
                    float footing=ground(p.X,p.Z)-.5f,top=p.Y-spec.Thickness;Need(top>footing,"Bridge support would invert.");
                    parts.Add(Box(spec.Id+" pier "+parts.Count,spec.Style,p,right,s==0?spec.Width:Math.Max(1.2f,spec.Width*.24f),1.6f,footing,top));
                }
                var end=points.Last();parts.Add(Box(spec.Id+" end abutment",spec.Style,end,Right(points,points.Length-1),spec.Width,1.6f,ground(end.X,end.Z)-.5f,end.Y-spec.Thickness));
                for(int i=0;i<points.Length;i++)
                {var r=Right(points,i);for(float x=-spec.Width/2;x<=spec.Width/2+.01f;x+=spec.Width/4){float level=ground(points[i].X+r.X*x,points[i].Z+r.Z*x);if(spec.WaterLevel.HasValue)level=Math.Max(level,spec.WaterLevel.Value);output.MinimumUndersideClearance=Math.Min(output.MinimumUndersideClearance,points[i].Y-spec.Thickness-level);}}
                Need(output.MinimumUndersideClearance>=spec.Clearance-.01f,"Bridge sampled underside clearance failed.");
            }
            else
            {
                float seamThickness=spec.Thickness+2*spec.HoleCellMargin+.5f;
                parts.Add(Sweep(spec.Id+" arched lining",spec.PortalStyle=="masonry"?"tunnel-lining":spec.Style,points,ArchRing(spec.Width/2,spec.Clearance,seamThickness)));
                foreach(bool entrance in new[]{true,false})
                {
                    float reach=spec.Width/2+seamThickness+.5f;
                    var endpoint=entrance?points[0]:points.Last();var right=Right(points,entrance?0:points.Length-1);
                    var forward=new V3(-right.Z,0,right.X);float sign=entrance?-1:1;
                    var outside=new V3(endpoint.X+sign*forward.X*reach,endpoint.Y,endpoint.Z+sign*forward.Z*reach);
                    var lipEnd=new V3(outside.X-sign*forward.X*.75f,outside.Y,outside.Z-sign*forward.Z*.75f);
                    var inside=At(points,sampled.Stations,entrance?spec.PortalDepth:sampled.Stations.Last()-spec.PortalDepth);
                    float rim=Math.Clamp(spec.Thickness+.25f,.65f,1.25f),outer=spec.Width/2+seamThickness+1,cutRadius=spec.Width/2+spec.Thickness+2*spec.HoleCellMargin;
                    float taper=(outer-spec.Width/2-rim)/(reach-.75f);
                    // The receding outer return encloses the circular end-cell envelope;
                    // the narrow front rim sits wholly beyond that envelope.
                    Need(outer>=cutRadius*Math.Sqrt(1+taper*taper)&&reach-.75f>=cutRadius,"Terrain cells are too coarse for a modest sealed portal; refine the Terrain holes grid.");
                    var front=ArchRing(spec.Width/2,spec.Clearance,rim);var back=ArchRing(spec.Width/2,spec.Clearance,seamThickness+1);
                    var portal=entrance?new[]{outside,lipEnd,endpoint,inside}:new[]{inside,endpoint,lipEnd,outside};
                    var sections=entrance?new[]{front,front,back,back}:new[]{back,back,front,front};
                    parts.Add(SweepSections(spec.Id+(entrance?" entry portal return":" exit portal return"),spec.PortalStyle=="masonry"?"tunnel-stone":spec.Style,portal,sections));
                }
            }
            if(spec.Kind=="tunnel"&&spec.PortalStyle=="masonry")
                foreach(int side in new[]{-1,1})parts.Add(Sweep(spec.Id+" raised service curb "+side,"tunnel-lining",points,Rect(side<0?-spec.Width/2:spec.Width/2-.28f,side<0?-spec.Width/2+.28f:spec.Width/2,0,.18f)));
            foreach(bool entrance in new[]{true,false})AddApproach(output,parts,entrance,ground,minX,minZ,size);
            Need(parts.Sum(p=>p.Vertices.Length)<=150000,"Structure vertex budget exceeded.");output.Parts=parts.ToArray();return output;
        }
        static void AddApproach(PlacedStructure output,List<StructurePart> parts,bool entrance,Func<float,float,float> ground,float minX,float minZ,float size)
        {
            var spec=output.Spec;var end=entrance?output.Path[0]:output.Path.Last();var right=Right(output.Path,entrance?0:output.Path.Length-1);
            var forward=new V3(-right.Z,0,right.X);float direction=entrance?-1:1,length=spec.ApproachLength;
            var start=new V3(end.X+direction*forward.X*length,0,end.Z+direction*forward.Z*length);start=new V3(start.X,ground(start.X,start.Z)+.03f,start.Z);
            // Tunnel portal returns have a level apron beneath their full raster envelope.
            float apron=spec.Kind=="tunnel"?spec.Width/2+spec.Thickness+2*spec.HoleCellMargin+1:0;
            Need(length>apron+4,"Approach must extend beyond the portal return.");float grade=Math.Abs(start.Y-end.Y)/(length-apron);Need(grade<=.25f,$"{spec.Id} {(entrance?"entry":"exit")} approach is {grade*100:F2}% over {length-apron:F1}m (deck {end.Y:F2}m, ground tie {start.Y:F2}m); relocate it or supply longer approachLength.");
            output.MaximumGradePercent=Math.Max(output.MaximumGradePercent,grade*100);
            float half=spec.Width/2,outer=half+6;int steps=(int)Math.Ceiling(length/2);var pavement=new Builder(spec.Id+" approach "+(entrance?"entry":"exit"),"asphalt");var shoulder=new Builder(spec.Id+" approach shoulders "+(entrance?"entry":"exit"),"ground");
            var strips=new Vertex[steps+1][];
            for(int i=0;i<=steps;i++)
            {
                float distance=i*length/steps,x=end.X+direction*forward.X*distance,z=end.Z+direction*forward.Z*distance;
                float t=Math.Clamp((distance-apron)/(length-apron),0,1),y=end.Y+(start.Y-end.Y)*t;
                strips[i]=new[]{-outer,-half,0,half,outer}.Select(offset=>
                {
                    float wx=x+right.X*offset,wz=z+right.Z*offset;Need(wx>=minX&&wx<=minX+size&&wz>=minZ&&wz<=minZ+size,"Approach leaves sandbox; shorten or relocate it.");
                    float py=Math.Abs(offset)>half?ground(wx,wz)+.03f:y;
                    return new Vertex(new V3(wx,py,wz),new V3(0,1,0),new V2(wx,wz),new V2(offset,distance));
                }).ToArray();
            }
            for(int i=1;i<=steps;i++)for(int row=0;row<4;row++)
            {
                var a=strips[i-1][row];var b=strips[i][row];var c=strips[i][row+1];var d=strips[i-1][row+1];
                var builder=row==0||row==3?shoulder:pavement;
                if(entrance){builder.Quad(a.Position,d.Position,c.Position,b.Position);output.ApproachEarthwork.Add(a,d,c,spec.Width);output.ApproachEarthwork.Add(a,c,b,spec.Width);}
                else{builder.Quad(a.Position,b.Position,c.Position,d.Position);output.ApproachEarthwork.Add(a,b,c,spec.Width);output.ApproachEarthwork.Add(a,c,d,spec.Width);}
            }
            var pavementPart=pavement.Finish();pavementPart.Closed=false;var shoulderPart=shoulder.Finish();shoulderPart.Closed=false;parts.Add(pavementPart);parts.Add(shoulderPart);
        }
        // Same +X/right,+Y/up,+Z/forward convention as the original Fjord tunnel builder.
        static V2[] Rect(float left,float right,float bottom,float top)=>new[]{new V2(left,bottom),new V2(right,bottom),new V2(right,top),new V2(left,top)};
        static V2[] ArchRing(float radius,float clearance,float thickness)
        {
            float spring=Math.Max(.8f,clearance-radius),rise=clearance-spring;var p=new List<V2>{new V2(-radius,0),new V2(-radius,spring)};
            for(int i=1;i<=32;i++){double a=Math.PI-i*Math.PI/32;p.Add(new V2((float)Math.Cos(a)*radius,spring+(float)Math.Sin(a)*rise));}
            p.Add(new V2(radius,0));p.Add(new V2(radius+thickness,0));p.Add(new V2(radius+thickness,spring));
            for(int i=1;i<=32;i++){double a=i*Math.PI/32;p.Add(new V2((float)Math.Cos(a)*(radius+thickness),spring+(float)Math.Sin(a)*(rise+thickness)));}
            p.Add(new V2(-radius-thickness,0));return p.ToArray();
        }
        static StructurePart Sweep(string name,string material,V3[] path,V2[] section)
            =>SweepSections(name,material,path,Enumerable.Repeat(section,path.Length).ToArray());
        static StructurePart SweepSections(string name,string material,V3[] path,V2[][] sections)
        {
            var mesh=new Builder(name,material);int n=sections[0].Length;
            Need(sections.Length==path.Length&&sections.All(s=>s.Length==n),"Corresponding bounded swept cross sections required.");
            float station=0;
            for(int i=1;i<path.Length;i++)
            {
                float next=station+Flat(path[i-1],path[i]),cross=0;
                for(int j=0;j<n;j++)
            {
                var r0=Right(path,i-1);var r1=Right(path,i);var a=Position(path[i-1],r0,sections[i-1][j]);var b=Position(path[i],r1,sections[i][j]);
                var c=Position(path[i],r1,sections[i][(j+1)%n]);var d=Position(path[i-1],r0,sections[i-1][(j+1)%n]);
                float edge=(float)Math.Sqrt(Math.Pow(sections[i-1][(j+1)%n].X-sections[i-1][j].X,2)+Math.Pow(sections[i-1][(j+1)%n].Y-sections[i-1][j].Y,2));
                mesh.QuadUV(a,d,c,b,new V2(station*.5f,cross*.5f),new V2(station*.5f,(cross+edge)*.5f),new V2(next*.5f,(cross+edge)*.5f),new V2(next*.5f,cross*.5f));cross+=edge;
                }
                station=next;
            }
            // Caps use ear clipping of the actual simple ring; no fan fills a tunnel opening.
            foreach(int end in new[]{0,path.Length-1})foreach(var face in Triangulate(sections[end]))
            {var a=Position(path[end],Right(path,end),sections[end][face[0]]);var b=Position(path[end],Right(path,end),sections[end][face[1]]);var c=Position(path[end],Right(path,end),sections[end][face[2]]);var ua=new V2(sections[end][face[0]].X*.5f,sections[end][face[0]].Y*.5f);var ub=new V2(sections[end][face[1]].X*.5f,sections[end][face[1]].Y*.5f);var uc=new V2(sections[end][face[2]].X*.5f,sections[end][face[2]].Y*.5f);if(end==0)mesh.TriangleUV(c,b,a,uc,ub,ua);else mesh.TriangleUV(a,b,c,ua,ub,uc);}
            return mesh.Finish();
        }
        static List<int[]> Triangulate(V2[] section)
        {
            double Cross(V2 a,V2 b,V2 c)=>((double)b.X-a.X)*(c.Y-a.Y)-((double)b.Y-a.Y)*(c.X-a.X);
            var ring=Enumerable.Range(0,section.Length).ToList();double area=0;for(int i=0;i<section.Length;i++)area+=section[i].X*section[(i+1)%section.Length].Y-section[i].Y*section[(i+1)%section.Length].X;
            Need(area>0,"Counterclockwise simple cross section required.");var faces=new List<int[]>();
            while(ring.Count>3)
            {
                bool clipped=false;for(int i=0;i<ring.Count;i++)
                {
                    int a=ring[(i+ring.Count-1)%ring.Count],b=ring[i],c=ring[(i+1)%ring.Count];if(Cross(section[a],section[b],section[c])<=1e-7)continue;
                    if(ring.Any(k=>k!=a&&k!=b&&k!=c&&Cross(section[a],section[b],section[k])>=-1e-7&&Cross(section[b],section[c],section[k])>=-1e-7&&Cross(section[c],section[a],section[k])>=-1e-7))continue;
                    faces.Add(new[]{a,b,c});ring.RemoveAt(i);clipped=true;break;
                }
                Need(clipped,"Structure cap triangulation failed.");
            }
            faces.Add(ring.ToArray());return faces;
        }
        static StructurePart Box(string name,string material,V3 center,V3 right,float width,float depth,float bottom,float top)
        {
            Need(top>bottom,"Invalid support height.");var forward=new V3(-right.Z,0,right.X);var a=new V3(center.X-forward.X*depth/2,bottom,center.Z-forward.Z*depth/2);var b=new V3(center.X+forward.X*depth/2,bottom,center.Z+forward.Z*depth/2);
            return Sweep(name,material,new[]{a,b},Rect(-width/2,width/2,0,top-bottom));
        }
        static V3 Position(V3 p,V3 right,V2 cross)=>new V3(p.X+right.X*cross.X,p.Y+cross.Y,p.Z+right.Z*cross.X);
        static V3 Right(V3[] p,int i){var a=p[Math.Max(0,i-1)];var b=p[Math.Min(p.Length-1,i+1)];float d=Flat(a,b);Need(d>.001f,"Collapsed structure frame.");return new V3((b.Z-a.Z)/d,0,-(b.X-a.X)/d);}
        static V3 At(V3[] p,float[] arc,float s){int i=Math.Clamp(Array.FindLastIndex(arc,a=>a<=s),0,p.Length-2);float t=Math.Clamp((s-arc[i])/(arc[i+1]-arc[i]),0,1);return new V3(p[i].X+(p[i+1].X-p[i].X)*t,p[i].Y+(p[i+1].Y-p[i].Y)*t,p[i].Z+(p[i+1].Z-p[i].Z)*t);}
        static float Flat(V3 a,V3 b)=>(float)Math.Sqrt(((double)b.X-a.X)*(b.X-a.X)+((double)b.Z-a.Z)*(b.Z-a.Z));
        public static void Need(bool value,string message){if(!value)throw new ArgumentException(message);}
        sealed class Builder
        {
            readonly string name,material;readonly List<V3> vertices=new List<V3>();readonly List<V2> uv=new List<V2>();readonly List<int> triangles=new List<int>();
            public Builder(string name,string material){this.name=name;this.material=material;}
            public void Quad(V3 a,V3 b,V3 c,V3 d){Triangle(a,b,c);Triangle(a,c,d);}
            public void QuadUV(V3 a,V3 b,V3 c,V3 d,V2 ua,V2 ub,V2 uc,V2 ud)
            {int start=uv.Count;Quad(a,b,c,d);var mapped=new[]{ua,ub,uc,ua,uc,ud};for(int i=0;i<6;i++)uv[start+i]=mapped[i];}
            public void TriangleUV(V3 a,V3 b,V3 c,V2 ua,V2 ub,V2 uc)
            {int start=uv.Count;Triangle(a,b,c);uv[start]=ua;uv[start+1]=ub;uv[start+2]=uc;}
            public void Triangle(V3 a,V3 b,V3 c)
            {
                double x=(b.Y-a.Y)*(c.Z-a.Z)-(b.Z-a.Z)*(c.Y-a.Y),y=(b.Z-a.Z)*(c.X-a.X)-(b.X-a.X)*(c.Z-a.Z),z=(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
                Need(x*x+y*y+z*z>1e-12,"Collapsed structure triangle.");
                foreach(var p in new[]{a,b,c}){Need(float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z),"Nonfinite structure mesh.");triangles.Add(vertices.Count);vertices.Add(p);uv.Add(new V2(p.X*.25f,(Math.Abs(y)>Math.Max(Math.Abs(x),Math.Abs(z))?p.Z:p.Y)*.25f));}
            }
            public StructurePart Finish()=>new StructurePart{Name=name,Material=material,Vertices=vertices.ToArray(),UV=uv.ToArray(),Triangles=triangles.ToArray()};
        }
    }
}

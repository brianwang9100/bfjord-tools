using System;
using System.Collections.Generic;
using System.Linq;
namespace Bwork.FjordCoast.Junctions
{
    public readonly struct V2 { public readonly float X,Y;public V2(float x,float y){X=x;Y=y;} }
    public readonly struct V3 { public readonly float X,Y,Z;public V3(float x,float y,float z){X=x;Y=y;Z=z;} }
    public readonly struct Vertex
    {
        public readonly V3 Position,Normal;public readonly V2 UV,Mask;
        public Vertex(V3 p,V3 n,V2 uv,V2 mask){Position=p;Normal=n;UV=uv;Mask=mask;}
    }
    public sealed class Mouth { public string Id;public V3 Outward;public Vertex[] CrossSection;public V3[] NativePath; }
    public sealed class MouthJoin { public string Id;public int[] Indices; }
    public sealed class JunctionMesh
    {
        public Vertex[] Vertices;public int[][] Triangles;public MouthJoin[] Mouths;
        public int[] OuterBoundary,GeometricVertex;public int AttributeSeamDuplicates;
    }
    public static class FjordThreeArmJunction
    {
        static readonly float[] Rows={-16,-6.8f,-4,-2.8f,0,2.8f,4,6.8f,16};
        static readonly float[] Bands={2.8f,4,6.8f,16};
        static readonly float[] WideConnectedCoreRadii={2.8f,24,28,32};
        /// <summary>One finite three- or four-arm junction patch; inputs are actual clipped road mouths looking out from the hub.
        /// Copies boundary attributes exactly. Does not trim roads, mutate terrain or admit a native route.</summary>
        public static JunctionMesh Build(V3 center,Mouth[] mouths,Func<float,float,float> groundHeight,int cornerSteps=16,bool useCoherentPavementHeight=false,bool useConnectedPavementCore=false,bool useWideConnectedPavementCore=false,bool useNativePathSurface=false,bool useCurvedMouthCoverage=false,int pavementHeightCorrectionVersion=0,JunctionHeightCorrection[] pavementHeightCorrections=null)
        {
            Need(Finite(center)&&mouths!=null&&mouths.Length>=3&&mouths.Length<=4&&groundHeight!=null,"One finite center, three or four mouths and final ground sampler required.");
            Need(mouths.Length==3||(useNativePathSurface&&useConnectedPavementCore&&useWideConnectedPavementCore&&useCurvedMouthCoverage&&pavementHeightCorrectionVersion==0),"Four-arm junctions require the native-path wide connected core with curved-mouth coverage and no legacy height corrections.");
            Need(!useCurvedMouthCoverage||(useNativePathSurface&&useConnectedPavementCore&&useWideConnectedPavementCore),"Curved-mouth coverage requires the native-path wide connected core.");
            Need(pavementHeightCorrectionVersion>=0&&pavementHeightCorrectionVersion<=1&&
                (pavementHeightCorrectionVersion==0?(pavementHeightCorrections==null||pavementHeightCorrections.Length==0):useNativePathSurface),"Unsupported pavement height correction version/ownership.");
            Need(cornerSteps>=8&&cornerSteps<=32&&cornerSteps%2==0,"Use 8..32 even corner intervals.");
            Need(mouths.All(m=>m!=null&&!string.IsNullOrEmpty(m.Id))&&mouths.Select(m=>m.Id).Distinct().Count()==mouths.Length,"Unique mouth identities required.");
            Need(mouths.All(m=>m.CrossSection!=null&&m.CrossSection.Length==9),"Expected complete nine-row mouths.");
            var sorted=mouths.OrderByDescending(m=>Math.Atan2(m.CrossSection[4].Position.Z-center.Z,m.CrossSection[4].Position.X-center.X)).ToArray();
            var frames=new List<Frame>();
            foreach(var m in sorted)
            {
                Need(m.CrossSection!=null&&m.CrossSection.Length==9&&Finite(m.Outward),"Expected complete nine-row mouth.");
                var c=m.CrossSection[4].Position;double length=Flat(m.Outward);Need(length>0,"Zero mouth tangent.");var d=new D(m.Outward)/length;
                double away=(c.X-center.X)*d.X+(c.Z-center.Z)*d.Z;Need(away>=12&&away<=160,"Mouth must be clipped 12..160 m outward from hub.");
                var right=new D(d.Z,0,-d.X);
                Need(Finite(m.CrossSection[8].Mask.X),"Nonfinite mouth polarity.");float polarity=Math.Sign(m.CrossSection[8].Mask.X);Need(polarity!=0,"Mouth mask polarity must be positive or negative.");
                for(int i=0;i<9;i++)
                {
                    var v=m.CrossSection[i];Need(Finite(v.Position)&&Finite(v.Normal)&&Finite(v.UV.X)&&Finite(v.UV.Y)&&Finite(v.Mask.X)&&Finite(v.Mask.Y),"Nonfinite mouth attribute.");
                    Need(v.UV.X==v.Position.X&&v.UV.Y==v.Position.Z,"UV0 must use accepted world-metre mapping.");
                    var expected=new D(c)+right*Rows[i];Need(Math.Abs(expected.X-v.Position.X)<.003&&Math.Abs(expected.Z-v.Position.Z)<.003,"Mouth rows differ from accepted lateral layout.");
                    Need(Math.Abs(v.Mask.X-polarity*Rows[i])<1e-5,"All nine mouth masks must share one coherent road polarity.");
                }
                frames.Add(new Frame{Input=m,Direction=d,Center=c});
            }
            var vertices=new List<Vertex>();var fixedNormals=new List<bool>();var positions=new Dictionary<(float,float,float),int>();var vertexKeys=new Dictionary<(float,float,float,float,float),int>();var geometric=new List<int>();
            int Add(Vertex v,bool exact=false)
            {
                var p=v.Position;Need(Finite(p)&&Finite(v.Mask.X)&&Finite(v.Mask.Y),"Nonfinite generated vertex.");var key=(p.X,p.Y,p.Z,v.Mask.X,v.Mask.Y);
                if(vertexKeys.TryGetValue(key,out int prior)){Need(!exact||vertices[prior].Equals(v),"Conflicting exact mouth attributes.");return prior;}
                Need(vertices.Count<2048,"Junction vertex budget.");var xyz=(p.X,p.Y,p.Z);if(!positions.TryGetValue(xyz,out int group)){group=positions.Count;positions.Add(xyz,group);}
                int index=vertices.Count;vertices.Add(v);fixedNormals.Add(exact);geometric.Add(group);vertexKeys.Add(key,index);return index;
            }
            foreach(var f in frames)f.Indices=f.Input.CrossSection.Select(v=>Add(v,true)).ToArray();
            int hub=Add(new Vertex(new V3(center.X,center.Y-.08f,center.Z),new V3(0,1,0),new V2(center.X,center.Z),new V2(0,0)));
            var triangles=Enumerable.Range(0,4).Select(_=>new List<int>()).ToArray();
            void Triangle(int slot,int a,int b,int c)
            {
                var p=vertices[a].Position;var q=vertices[b].Position;var r=vertices[c].Position;
                double area=((double)q.X-p.X)*(r.Z-p.Z)-((double)q.Z-p.Z)*(r.X-p.X);
                Need(area<0,"Folded/collapsed triangle: increase trim distance or author a different junction layout.");
                triangles[slot].Add(a);triangles[slot].Add(b);triangles[slot].Add(c);
            }
            var outer=new List<int>();var pavementFan=new List<int[]>();
            for(int arm=0;arm<frames.Count;arm++)
            {
                var a=frames[arm];var b=frames[(arm+1)%frames.Count];outer.AddRange(a.Indices);
                pavementFan.Add(new[]{hub,a.Indices[3],a.Indices[4]});pavementFan.Add(new[]{hub,a.Indices[4],a.Indices[5]});
                var curves=new V3[4][];
                for(int band=0;band<4;band++)curves[band]=Corner(center,a,b,band,cornerSteps,groundHeight,useCoherentPavementHeight,useConnectedPavementCore,useWideConnectedPavementCore);
                // Keep the complete curved 8 m road footprint within pavement/edge slots.
                // Widen only the interior edge boundary into the existing verge; all mouth
                // rows and the 6.8/16 m boundaries remain exact.
                for(int j=1;j<cornerSteps&&!useConnectedPavementCore;j++)
                {
                    float t=j/(float)cornerSteps,blend=.075f*4*t*(1-t);var p=curves[1][j];var q=curves[2][j];
                    curves[1][j]=new V3(p.X+(q.X-p.X)*blend,p.Y,p.Z+(q.Z-p.Z)*blend);
                }
                if(useCurvedMouthCoverage)
                {
                    // Curved native road edges can extend outside the straight mouth-to-core
                    // chord. Borrow a bounded part of the existing shoulder near each mouth;
                    // preserve every mouth row, the core radius and the outer patch boundary.
                    for(int j=1;j<cornerSteps;j++)
                    {
                        float t=j/(float)cornerSteps;
                        float blend=.55f*Math.Min(1,8*t)*Math.Min(1,8*(1-t))*Math.Abs(2*t-1);
                        var p=curves[1][j];var q=curves[2][j];
                        curves[1][j]=new V3(p.X+(q.X-p.X)*blend,p.Y,p.Z+(q.Z-p.Z)*blend);
                    }
                }
                int At(int band,int station,float sign)
                {
                    if(station==0)return a.Indices[5+band];if(station==cornerSteps)return b.Indices[3-band];
                    var p=curves[band][station];float t=station/(float)cornerSteps;
                    float arc=a.Input.CrossSection[4].Mask.Y*(1-t)+b.Input.CrossSection[4].Mask.Y*t;
                    return Add(new Vertex(p,new V3(0,1,0),new V2(p.X,p.Z),new V2(sign*Bands[band],arc)));
                }
                for(int j=0;j<cornerSteps;j++)
                {
                    // abs(lateral) drives current blend shaders. Keep each triangle on one signed side;
                    // opposite road parameterizations use an ordinary exact-position UV seam at t=.5.
                    float sign=j<cornerSteps/2?Math.Sign(a.Input.CrossSection[5].Mask.X):Math.Sign(b.Input.CrossSection[3].Mask.X);
                    pavementFan.Add(new[]{hub,At(0,j,sign),At(0,j+1,sign)});
                    for(int band=0;band<3;band++)
                    {
                        int ia=At(band,j,sign),ib=At(band,j+1,sign),oa=At(band+1,j,sign),ob=At(band+1,j+1,sign);
                        Triangle(band+1,ia,oa,ob);Triangle(band+1,ia,ob,ib);
                    }
                }
                for(int j=1;j<cornerSteps;j++)outer.Add(At(3,j,j<=cornerSteps/2?Math.Sign(a.Input.CrossSection[8].Mask.X):Math.Sign(b.Input.CrossSection[0].Mask.X)));
            }
            // Preserve the former fan exactly where it is valid. A bent through-road can make
            // the inner pavement concave at the fixed hub even while every outer band is sound.
            var pavement=pavementFan;
            if(pavementFan.Any(t=>AreaXZ(vertices[t[0]].Position,vertices[t[1]].Position,vertices[t[2]].Position)>=0))
            {
                var ring=new List<int>();foreach(var face in pavementFan)
                {
                    if(ring.Count==0)ring.Add(face[1]);
                    Need(geometric[ring[ring.Count-1]]==geometric[face[1]],"Disconnected pavement boundary.");
                    ring.Add(face[2]);
                }
                Need(geometric[ring[0]]==geometric[ring[ring.Count-1]],"Open pavement boundary.");ring.RemoveAt(ring.Count-1);
                pavement=TriangulatePavement(vertices,ring,hub);
            }
            foreach(var t in pavement)Triangle(0,t[0],t[1],t[2]);
            // Normals are shared across exact-position attribute seams; supplied mouth normals stay bit-identical.
            var sums=new D[positions.Count];
            foreach(var t in triangles)for(int i=0;i<t.Count;i+=3)
            {
                var p=new D(vertices[t[i]].Position);var q=new D(vertices[t[i+1]].Position);var r=new D(vertices[t[i+2]].Position);var n=Cross(q-p,r-p);
                for(int k=0;k<3;k++)sums[geometric[t[i+k]]]+=n;
            }
            for(int i=0;i<vertices.Count;i++)if(!fixedNormals[i])
            {
                var v=vertices[i];var n=sums[geometric[i]];double length=Math.Sqrt(n.X*n.X+n.Y*n.Y+n.Z*n.Z);Need(length>0,"Unused/zero normal vertex.");
                vertices[i]=new Vertex(v.Position,(n/length).Float(),v.UV,v.Mask);
            }
            var result=new JunctionMesh{Vertices=vertices.ToArray(),Triangles=triangles.Select(t=>t.ToArray()).ToArray(),Mouths=frames.Select(f=>new MouthJoin{Id=f.Input.Id,Indices=f.Indices}).ToArray(),OuterBoundary=outer.ToArray(),GeometricVertex=geometric.ToArray(),AttributeSeamDuplicates=vertices.Count-positions.Count};
            CheckTopology(result);
            if(useNativePathSurface)
            {
                var paths=mouths.Select(m=>m.NativePath).ToArray();result=FjordJunctionPathConformance.Apply(result,paths);
                if(pavementHeightCorrectionVersion==1)result=FjordJunctionHeightConformance.Apply(FjordJunctionHeightConformance.Refine(result,paths),paths,pavementHeightCorrections);
            }
            return result;
        }
        static double AreaXZ(V3 a,V3 b,V3 c)=>((double)b.X-a.X)*(c.Z-a.Z)-((double)b.Z-a.Z)*(c.X-a.X);
        internal static List<int[]> TriangulatePavement(IList<Vertex> vertices,List<int> boundary,int hub)
        {
            Need(boundary.Count>=3&&boundary.Count<=136,"Bounded complete pavement boundary required.");
            var ring=new List<int>(boundary);
            Need(ring.Select(i=>(vertices[i].Position.X,vertices[i].Position.Z)).Distinct().Count()==ring.Count,"Repeated pavement boundary point.");
            for(int i=0;i<ring.Count;i++)for(int j=i+1;j<ring.Count;j++)
            {
                if(j==i+1||(i==0&&j==ring.Count-1))continue;
                Need(!Intersects(vertices[ring[i]].Position,vertices[ring[(i+1)%ring.Count]].Position,vertices[ring[j]].Position,vertices[ring[(j+1)%ring.Count]].Position),"Pavement boundary crosses itself.");
            }
            bool Contains(int a,int b,int c,V3 p)=>AreaXZ(vertices[a].Position,vertices[b].Position,p)<=0&&AreaXZ(vertices[b].Position,vertices[c].Position,p)<=0&&AreaXZ(vertices[c].Position,vertices[a].Position,p)<=0;
            var faces=new List<int[]>();int work=0;
            while(ring.Count>3)
            {
                bool clipped=false;
                for(int i=0;i<ring.Count;i++)
                {
                    Need(++work<=Math.Max(10404,boundary.Count*boundary.Count),"Pavement triangulation work bound.");int a=ring[(i+ring.Count-1)%ring.Count],b=ring[i],c=ring[(i+1)%ring.Count];
                    if(AreaXZ(vertices[a].Position,vertices[b].Position,vertices[c].Position)>=0)continue;
                    // Inclusive containment preserves every collinear mouth/shore boundary vertex.
                    if(ring.Any(v=>v!=a&&v!=b&&v!=c&&Contains(a,b,c,vertices[v].Position)))continue;
                    faces.Add(new[]{a,b,c});ring.RemoveAt(i);clipped=true;break;
                }
                Need(clipped,"Pavement cannot be triangulated without crossing/degenerate faces.");
            }
            Need(AreaXZ(vertices[ring[0]].Position,vertices[ring[1]].Position,vertices[ring[2]].Position)<0,"Collapsed pavement remainder.");faces.Add(ring.ToArray());
            var center=vertices[hub].Position;int containing=faces.FindIndex(t=>Contains(t[0],t[1],t[2],center));Need(containing>=0,"Exact road hub lies outside pavement.");
            var found=faces[containing];int edge=-1;for(int k=0;k<3;k++)if(AreaXZ(vertices[found[k]].Position,vertices[found[(k+1)%3]].Position,center)==0)edge=k;
            if(edge<0)
            {
                faces.RemoveAt(containing);for(int k=0;k<3;k++)faces.Add(new[]{found[k],found[(k+1)%3],hub});
            }
            else
            {
                int a=found[edge],b=found[(edge+1)%3];Need(!center.Equals(vertices[a].Position)&&!center.Equals(vertices[b].Position),"Hub coincides with pavement boundary vertex.");
                var touching=faces.Where(t=>t.Contains(a)&&t.Contains(b)).ToArray();Need(touching.Length==2,"Exact road hub lies on outer pavement boundary.");
                // A hub exactly on an internal diagonal splits both incident triangles, never one.
                foreach(var t in touching)
                {
                    faces.Remove(t);int k=Array.FindIndex(t,v=>v==a||v==b);if(t[(k+1)%3]!=a&&t[(k+1)%3]!=b)k=(k+2)%3;
                    faces.Add(new[]{t[k],hub,t[(k+2)%3]});faces.Add(new[]{hub,t[(k+1)%3],t[(k+2)%3]});
                }
            }
            foreach(var t in faces)Need(AreaXZ(vertices[t[0]].Position,vertices[t[1]].Position,vertices[t[2]].Position)<0,"Hub insertion produced a folded/collapsed face.");
            return faces;
        }
        sealed class Frame { public Mouth Input;public D Direction;public V3 Center;public int[] Indices; }
        static V3[] Corner(V3 junctionCenter,Frame a,Frame b,int band,int steps,Func<float,float,float> ground,bool useCoherentPavementHeight,bool useConnectedPavementCore,bool useWideConnectedPavementCore)
        {
            float width=Bands[band];var p=new D(a.Input.CrossSection[5+band].Position);var q=new D(b.Input.CrossSection[3-band].Position);var r=a.Direction*-1;var s=b.Direction*-1;
            // Version 2 is a conventional broad polygonal intersection. Each exact mouth row
            // connects through a nested core corner, so a reflex pair of approach tangents
            // cannot place the authored hub outside pavement or invert neighbouring bands.
            if(useConnectedPavementCore)
            {
                double startAngle=Math.Atan2(p.Z-junctionCenter.Z,p.X-junctionCenter.X),endAngle=Math.Atan2(q.Z-junctionCenter.Z,q.X-junctionCenter.X);while(endAngle>startAngle)endAngle-=Math.PI*2;
                double middleAngle=(startAngle+endAngle)*.5,radius=useWideConnectedPavementCore?WideConnectedCoreRadii[band]:Bands[band];var core=new D(junctionCenter.X+Math.Cos(middleAngle)*radius,junctionCenter.Y-.08,junctionCenter.Z+Math.Sin(middleAngle)*radius);
                var connectedOutput=new V3[steps+1];connectedOutput[0]=p.Float();connectedOutput[steps]=q.Float();
                for(int j=1;j<steps;j++)
                {
                    double t=j/(double)steps;D v;
                    if(t<=.5){double u=t*2;v=p*(1-u)+core*u;}else{double u=(t-.5)*2;v=core*(1-u)+q*u;}
                    float x=(float)v.X,z=(float)v.Z;double y=v.Y;
                    if(width>4){float g=ground(x,z);Need(Finite(g),"Nonfinite final ground height.");double blend=(width-4)/12;blend=blend*blend*(3-2*blend);y=y*(1-blend)+g*blend;}
                    connectedOutput[j]=new V3(x,(float)y,z);
                }
                return connectedOutput;
            }
            double denominator=CrossXZ(r,s);D c,d;
            if(Math.Abs(denominator)<1e-7)
            {
                Need(a.Direction.X*b.Direction.X+a.Direction.Z*b.Direction.Z<-.999999,"Parallel duplicate junction arms.");c=p+(q-p)/3;d=q-(q-p)/3;
            }
            else
            {
                double u=CrossXZ(q-p,s)/denominator,v=CrossXZ(q-p,r)/denominator;
                Need(u>1&&v>1&&u<320&&v<320,"Road mouths overlap their corner; clip farther from the junction.");
                c=p+r*(u*.65);d=q+s*(v*.65);
            }
            // The shared interior core uses the outer crown height; exact crowned mouth rows remain fixed.
            // This gives level approaches one continuous pad without a raised center ridge.
            // Outer bands retain the accepted smooth 4..16 m ground blend.
            double py=a.Center.Y-.08,qy=b.Center.Y-.08;
            c=new D(c.X,py+(c.Y-p.Y),c.Z);d=new D(d.X,qy+(d.Y-q.Y),d.Z);
            var pBase=new D(p.X,py,p.Z);var qBase=new D(q.X,qy,q.Z);var output=new V3[steps+1];output[0]=p.Float();output[steps]=q.Float();
            for(int j=1;j<steps;j++)
            {
                double t=j/(double)steps,k=1-t;var v=pBase*(k*k*k)+c*(3*k*k*t)+d*(3*k*t*t)+qBase*(t*t*t);float x=(float)v.X,z=(float)v.Z;
                // Versioned authored junctions can keep the accepted plan-view fillet while
                // preventing 3D mouth tangents from bowing the inner pavement above the two
                // adjacent road crowns. Exact mouth rows at j=0/steps remain untouched.
                double y=useCoherentPavementHeight&&width<=4?py*k+qy*t:v.Y;
                if(width>4){float g=ground(x,z);Need(Finite(g),"Nonfinite final ground height.");double blend=(width-4)/12;blend=blend*blend*(3-2*blend);y=y*(1-blend)+g*blend;}
                output[j]=new V3(x,(float)y,z);
            }
            return output;
        }
        static void CheckTopology(JunctionMesh m)
        {
            var edges=new Dictionary<(int,int),int>();var used=new HashSet<int>();int faces=0;
            foreach(var t in m.Triangles)for(int i=0;i<t.Length;i+=3)
            {
                faces++;for(int k=0;k<3;k++){int a=m.GeometricVertex[t[i+k]],b=m.GeometricVertex[t[i+(k+1)%3]];used.Add(a);used.Add(b);var key=(Math.Min(a,b),Math.Max(a,b));edges.TryGetValue(key,out int n);edges[key]=n+1;}
            }
            Need(faces<=4096&&edges.All(e=>e.Value<=2),"Nonmanifold/budget junction.");
            var boundary=new HashSet<(int,int)>();
            for(int i=0;i<m.OuterBoundary.Length;i++){int a=m.GeometricVertex[m.OuterBoundary[i]],b=m.GeometricVertex[m.OuterBoundary[(i+1)%m.OuterBoundary.Length]];Need(boundary.Add((Math.Min(a,b),Math.Max(a,b))),"Repeated boundary edge.");}
            Need(boundary.SetEquals(edges.Where(e=>e.Value==1).Select(e=>e.Key))&&used.Count-edges.Count+faces==1,"Junction has unmatched internal edges or holes.");
            // An upward-oriented disk with a simple outer boundary has no overlapping interior sheets.
            int count=m.OuterBoundary.Length;
            for(int i=0;i<count;i++)for(int j=i+1;j<count;j++)
            {
                if(j==i+1||(i==0&&j==count-1))continue;
                Need(!Intersects(m.Vertices[m.OuterBoundary[i]].Position,m.Vertices[m.OuterBoundary[(i+1)%count]].Position,m.Vertices[m.OuterBoundary[j]].Position,m.Vertices[m.OuterBoundary[(j+1)%count]].Position),"Outer junction boundary crosses itself; revise trims.");
            }
        }
        static bool Intersects(V3 a,V3 b,V3 c,V3 d)
        {
            double Orient(V3 x,V3 y,V3 z)=>((double)y.X-x.X)*(z.Z-x.Z)-((double)y.Z-x.Z)*(z.X-x.X);
            bool On(V3 x,V3 y,V3 p)=>p.X>=Math.Min(x.X,y.X)&&p.X<=Math.Max(x.X,y.X)&&p.Z>=Math.Min(x.Z,y.Z)&&p.Z<=Math.Max(x.Z,y.Z);
            double p=Orient(a,b,c),q=Orient(a,b,d),r=Orient(c,d,a),s=Orient(c,d,b);
            return (p==0&&On(a,b,c))||(q==0&&On(a,b,d))||(r==0&&On(c,d,a))||(s==0&&On(c,d,b))||((p<0)!=(q<0)&&(r<0)!=(s<0));
        }
        readonly struct D
        {
            public readonly double X,Y,Z;public D(double x,double y,double z){X=x;Y=y;Z=z;}public D(V3 p):this(p.X,p.Y,p.Z){}
            public V3 Float()=>new V3((float)X,(float)Y,(float)Z);
            public static D operator +(D a,D b)=>new D(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
            public static D operator -(D a,D b)=>new D(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
            public static D operator *(D a,double b)=>new D(a.X*b,a.Y*b,a.Z*b);
            public static D operator /(D a,double b)=>a*(1/b);
        }
        static double CrossXZ(D a,D b)=>a.X*b.Z-a.Z*b.X;
        static D Cross(D a,D b)=>new D(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        static double Flat(V3 v)=>Math.Sqrt((double)v.X*v.X+(double)v.Z*v.Z);
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        static bool Finite(V3 v)=>Finite(v.X)&&Finite(v.Y)&&Finite(v.Z)&&Math.Abs(v.X)<=20000&&Math.Abs(v.Z)<=20000&&Math.Abs(v.Y)<=5000;
        static void Need(bool ok,string message){if(!ok)throw new ArgumentException(message);}
    }
}

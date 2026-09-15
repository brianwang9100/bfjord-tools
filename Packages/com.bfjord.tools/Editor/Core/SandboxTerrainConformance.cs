using System;
using Bwork.FjordCoast.Junctions;

namespace Bwork.WorldAuthoring
{
    public sealed class SandboxTerrainFit
    {
        public float[,] Heights;
        public int ConstraintVertices, LoweredSamples;
        public double BeforeMinimumClearance, MinimumClearance, MaximumAdditionalCut;
    }

    /// <summary>Fits existing height samples to the actual mesh, preserving every mesh attribute.
    /// The guarantee covers both full-resolution Terrain cell diagonals; it requires the sandbox's
    /// full-detail Terrain setting. It does not assert clearance against arbitrary coarser LODs.</summary>
    public static class SandboxTerrainConformance
    {
        public const double Clearance = .03, MaximumAdditionalCut = 4;
        const int MaximumChecks = 8000000;
        readonly struct Point
        {
            public readonly double x,y,z;
            public Point(double x,double y,double z){this.x=x;this.y=y;this.z=z;}
            public Point(V3 p):this(p.X,p.Y,p.Z){}
        }
        readonly struct Triangle
        {
            public readonly Point a,b,c;
            public readonly double area;
            public Triangle(Point a,Point b,Point c){this.a=a;this.b=b;this.c=c;area=Cross(a,b,c);}
            public double Height(Point p)=>(Cross(p,b,c)*a.y+Cross(a,p,c)*b.y+Cross(a,b,p)*c.y)/area;
        }
        public static SandboxTerrainFit Fit(SandboxEarthworkPatch patch,float[,] baseline,
            float minX,float minY,float minZ,float sizeX,float sizeY,float sizeZ)
        {
            int n=baseline.GetLength(0);
            Need(n>=33&&n<=1025&&baseline.GetLength(1)==n&&sizeX>0&&sizeZ>0&&sizeY>0,"Bounded square heightmap required.");
            var after=(float[,])baseline.Clone();double dx=sizeX/(n-1.0),dz=sizeZ/(n-1.0);
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                var sample=patch.Sample((float)(minX+x*dx),(float)(minZ+z*dz));
                if(sample.Affected)after[z,x]=(sample.Height-minY)/sizeY;
            }
            var seed=(float[,])after.Clone();
            // A full quantization step plus arithmetic slack protects the existing 3cm separation
            // when Unity stores its 16-bit heightmap. This is not an arbitrary surface offset.
            double reserve=sizeY/32766.0+.00005;
            var report=new SandboxTerrainFit{Heights=after};
            report.BeforeMinimumClearance=Visit(patch,after,minX,minY,minZ,sizeX,sizeY,sizeZ,0,false,out _);
            Visit(patch,after,minX,minY,minZ,sizeX,sizeY,sizeZ,Clearance+reserve,true,out int constraints);
            report.ConstraintVertices=constraints;
            report.MinimumClearance=Visit(patch,after,minX,minY,minZ,sizeX,sizeY,sizeZ,0,false,out _);
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                Need(float.IsFinite(after[z,x])&&after[z,x]>=0&&after[z,x]<=1,"Earthwork exceeds Terrain height range.");
                double cut=(seed[z,x]-after[z,x])*sizeY;
                Need(cut<=MaximumAdditionalCut,"Cell conformance requires more than 4m additional cut at "+x+","+z+": "+cut+"m; prefit clearance="+report.BeforeMinimumClearance+"; refine authored geometry.");
                if(cut>0){report.LoweredSamples++;report.MaximumAdditionalCut=Math.Max(report.MaximumAdditionalCut,cut);}
            }
            Need(report.MinimumClearance>=Clearance,"Full-grid conformance failed.");
            return report;
        }
        public static double Validate(SandboxEarthworkPatch patch,float[,] actual,float minX,float minY,float minZ,float sizeX,float sizeY,float sizeZ)
        {
            double clearance=Visit(patch,actual,minX,minY,minZ,sizeX,sizeY,sizeZ,0,false,out _);
            Need(clearance>=Clearance-1e-5,"Stored Terrain intersects mesh clearance envelope.");return clearance;
        }
        static double Visit(SandboxEarthworkPatch patch,float[,] heights,double minX,double minY,double minZ,double sizeX,double sizeY,double sizeZ,double required,bool lower,out int constraints)
        {
            int n=heights.GetLength(0),checks=0,counted=0;double dx=sizeX/(n-1),dz=sizeZ/(n-1),minimum=double.PositiveInfinity;
            var workA=new Point[8];var workB=new Point[8];
            foreach(var face in patch.Faces)
            {
                var road=new Triangle(new Point(face.a.Position),new Point(face.b.Position),new Point(face.c.Position));
                Need(Math.Abs(road.area)>1e-10,"Nonprojectable earthwork triangle.");
                int x0=Math.Clamp((int)Math.Floor((Math.Min(road.a.x,Math.Min(road.b.x,road.c.x))-minX)/dx),0,n-2);
                int x1=Math.Clamp((int)Math.Floor((Math.Max(road.a.x,Math.Max(road.b.x,road.c.x))-minX)/dx),0,n-2);
                int z0=Math.Clamp((int)Math.Floor((Math.Min(road.a.z,Math.Min(road.b.z,road.c.z))-minZ)/dz),0,n-2);
                int z1=Math.Clamp((int)Math.Floor((Math.Max(road.a.z,Math.Max(road.b.z,road.c.z))-minZ)/dz),0,n-2);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {
                    Check(x,z,x+1,z,x,z+1);Check(x+1,z+1,x,z+1,x+1,z);
                    Check(x,z,x+1,z,x+1,z+1);Check(x,z,x+1,z+1,x,z+1);
                }
                void Check(int ax,int az,int bx,int bz,int cx,int cz)
                {
                    Need(++checks<=MaximumChecks,"Cell intersection budget exceeded.");
                    Point At(int x,int z)=>new Point(minX+x*dx,minY+heights[z,x]*sizeY,minZ+z*dz);
                    var ground=new Triangle(At(ax,az),At(bx,bz),At(cx,cz));
                    int count=Intersection(road,ground,workA,workB);counted+=count;
                    for(int i=0;i<count;i++)
                    {
                        var p=workA[i];ground=new Triangle(At(ax,az),At(bx,bz),At(cx,cz));
                        double gap=road.Height(p)-ground.Height(p);minimum=Math.Min(minimum,gap);
                        double violation=required-gap;if(!lower||violation<=0)continue;
                        double wa=Math.Max(0,Cross(p,ground.b,ground.c)/ground.area),wb=Math.Max(0,Cross(ground.a,p,ground.c)/ground.area),wc=Math.Max(0,1-wa-wb);
                        double sum=wa*wa+wb*wb+wc*wc,step=(violation+.000002)/sum/sizeY;
                        // The minimum-norm local correction lowers only contributing samples.
                        // Both planes are affine: clipped vertices certify the entire overlap.
                        // Later corrections only lower, preserving prior inequalities.
                        heights[az,ax]-=(float)(step*wa);heights[bz,bx]-=(float)(step*wb);heights[cz,cx]-=(float)(step*wc);
                    }
                }
            }
            constraints=counted;Need(double.IsFinite(minimum),"No earthwork overlap.");return minimum;
        }
        // Reused from Bwork's WorldAuthoredIslandTerrainClearance projected intersection.
        static int Intersection(Triangle subject,Triangle clip,Point[] a,Point[] b)
        {
            a[0]=subject.a;a[1]=subject.b;a[2]=subject.c;int count=3;double direction=clip.area>0?1:-1;
            for(int edge=0;edge<3;edge++)
            {
                Point start=edge==0?clip.a:edge==1?clip.b:clip.c,end=edge==0?clip.b:edge==1?clip.c:clip.a;
                int output=0;if(count==0)return 0;Point previous=a[count-1];double before=Cross(start,end,previous)*direction;
                for(int i=0;i<count;i++)
                {
                    Point current=a[i];double after=Cross(start,end,current)*direction;bool oldInside=before>=-1e-9,newInside=after>=-1e-9;
                    if(oldInside!=newInside)
                    {
                        double t=before/(before-after);Need(output<b.Length,"Intersection bound.");
                        b[output++]=new Point(previous.x+(current.x-previous.x)*t,0,previous.z+(current.z-previous.z)*t);
                    }
                    if(newInside){Need(output<b.Length,"Intersection bound.");b[output++]=current;}
                    previous=current;before=after;
                }
                Array.Copy(b,a,output);count=output;
            }
            return count;
        }
        static double Cross(Point a,Point b,Point c)=>(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
        static void Need(bool valid,string message){if(!valid)throw new ArgumentException("Terrain conformance: "+message);}
    }
}

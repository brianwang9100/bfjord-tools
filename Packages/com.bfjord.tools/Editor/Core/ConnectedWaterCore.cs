using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Bwork.Authoring.WaterSandbox
{
    [Serializable] public sealed class WaterNode
    {
        public string id, kind = "junction"; // source, junction, lake, mouth, ocean
        public Vector3 position;
        public Vector2 radius = new Vector2(10,10); // ellipse radii; ocean uses a rectangle
        public float bedDepth = 2;
    }
    [Serializable] public sealed class WaterReach
    {
        public string id, from, to;
        public float bedDepth = 2, flowSpeed = 1;
        public RiverKnot[] knots;
    }
    [Serializable] public sealed class ConnectedWaterRecipe
    {
        public int schemaVersion = 1;
        public string id = "connected-water-001";
        public float cellSize = 2, bankFalloff = 16, waveBankFade = 4;
        public float riverWaveHeight = .12f, riverWaveLength = 14, riverWaveSpeed = .8f;
        public float oceanWaveHeight = .45f, oceanWaveLength = 28, oceanWaveSpeed = .65f;
        public float flowSpeed = .8f, normalStrength = .045f, foamStrength = .2f;
        public float lakeWaveHeight = .06f, lakeWaveLength = 18, lakeWaveSpeed = .4f;
        public float rippleTileSize = 5, detailTileSize = 1.6f, detailStrength = .4f;
        public float smoothness = .82f, oceanSmoothness = .86f, depthColorDistance = 4;
        public float shallowOpacity = .18f, deepOpacity = 1, shoreFadeDepth = .3f;
        public float foamWidth = .65f, foamTileSize = 4, foamCutoff = .52f, crestFoamStrength = .08f;
        // New fields retain the 0.4 appearance when absent from a saved recipe.
        public float riverCurrentStrength = .22f, riverStreakScale = 1, riverTurbulence = .23f;
        public float oceanSurfaceStrength = .32f, oceanWaveSharpness = 0;
        public float oceanShoreFoam = 0, oceanBreakerStrength = 0, oceanBeachDepth = 3;
        public float oceanSwashSpeed = .6f, oceanSwashDepthSpacing = 1.1f;
        public int waterPatternSeed = 0;
        public float oceanBlendDistance = 24;
        public Vector2 oceanWaveDirection = new Vector2(.8f,.6f);
        public Color shallowColor = new Color(.16f,.30f,.25f,1), deepColor = new Color(.025f,.105f,.10f,1);
        public Color oceanShallowColor = new Color(.12f,.27f,.29f,1), oceanDeepColor = new Color(.025f,.085f,.125f,1);
        public WaterNode[] nodes;
        public WaterReach[] reaches;
    }
    public readonly struct WaterFieldSample
    {
        public readonly float Distance, Height, BedDepth, Ocean, Lake, Foam;
        public readonly Vector2 Flow;
        public WaterFieldSample(float distance,float height,float bedDepth,Vector2 flow,float ocean,float lake=0,float foam=0)
        { Distance=distance;Height=height;BedDepth=bedDepth;Flow=flow;Ocean=ocean;Lake=lake;Foam=foam; }
    }

    /// <summary>Finite planar union. One welded surface and one field own wet coverage and carving.</summary>
    public sealed class ConnectedWaterField
    {
        sealed class Reach
        {
            public WaterReach recipe;
            public Vector3[] centers;
            public float[] radii,foam;
            public float startCollar,endCollar,length;
        }
        readonly struct Segment
        {
            public readonly int reach, indexInReach;
            public readonly Vector3 a,b;
            public readonly float ra,rb;
            public Segment(int reach,int indexInReach,Vector3 a,Vector3 b,float ra,float rb){this.reach=reach;this.indexInReach=indexInReach;this.a=a;this.b=b;this.ra=ra;this.rb=rb;}
        }
        const float BucketSize=16;
        readonly ConnectedWaterRecipe recipe;
        readonly Dictionary<string,WaterNode> nodes;
        readonly List<Reach> reaches=new List<Reach>();
        readonly List<Segment> segments=new List<Segment>();
        readonly Dictionary<long,List<int>> buckets=new Dictionary<long,List<int>>();
        public Bounds Bounds {get;private set;}
        public int ReachCount=>reaches.Count;
        public ConnectedWaterRecipe Recipe=>recipe;
        static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
        static long Key(int x,int z)=>((long)x<<32)^(uint)z;
        static int Bucket(float v)=>Mathf.FloorToInt(v/BucketSize);
        static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
        static void Range(float v,float lo,float hi,string name){if(!float.IsFinite(v)||v<lo||v>hi)throw new ArgumentException(name+" out of range.");}

        public ConnectedWaterField(ConnectedWaterRecipe recipe)
        {
            this.recipe=recipe??throw new ArgumentNullException(nameof(recipe));
            if(recipe.schemaVersion!=1||string.IsNullOrWhiteSpace(recipe.id)||recipe.nodes==null||recipe.nodes.Length<2||recipe.nodes.Length>32||
                recipe.reaches==null||recipe.reaches.Length==0||recipe.reaches.Length>32)throw new ArgumentException("Bounded water topology required.");
            Range(recipe.oceanBlendDistance,4,32,"oceanBlendDistance");Range(recipe.cellSize,1,4,"cellSize");Range(recipe.bankFalloff,4,40,"bankFalloff");Range(recipe.waveBankFade,2,12,"waveBankFade");
            Range(recipe.lakeWaveHeight,0,1,"lakeWaveHeight");Range(recipe.lakeWaveLength,4,80,"lakeWaveLength");Range(recipe.lakeWaveSpeed,0,3,"lakeWaveSpeed");
            Range(recipe.rippleTileSize,.5f,30,"rippleTileSize");Range(recipe.detailTileSize,.25f,15,"detailTileSize");Range(recipe.detailStrength,0,1,"detailStrength");
            Range(recipe.smoothness,0,.98f,"smoothness");Range(recipe.oceanSmoothness,0,.98f,"oceanSmoothness");Range(recipe.depthColorDistance,.2f,20,"depthColorDistance");
            Range(recipe.shallowOpacity,0,1,"shallowOpacity");Range(recipe.deepOpacity,recipe.shallowOpacity,1,"deepOpacity");Range(recipe.shoreFadeDepth,.05f,2,"shoreFadeDepth");
            Range(recipe.foamWidth,.05f,3,"foamWidth");Range(recipe.foamTileSize,.5f,20,"foamTileSize");Range(recipe.foamCutoff,.1f,.9f,"foamCutoff");Range(recipe.crestFoamStrength,0,1,"crestFoamStrength");
            foreach(var color in new[]{recipe.shallowColor,recipe.deepColor,recipe.oceanShallowColor,recipe.oceanDeepColor})
                for(int channel=0;channel<4;channel++)Range(color[channel],0,1,"water color");
            Range(recipe.riverWaveHeight,0,1,"riverWaveHeight");Range(recipe.oceanWaveHeight,0,1,"oceanWaveHeight");
            Range(recipe.riverWaveLength,4,80,"riverWaveLength");Range(recipe.oceanWaveLength,4,80,"oceanWaveLength");
            Range(recipe.riverWaveSpeed,0,3,"riverWaveSpeed");Range(recipe.oceanWaveSpeed,0,3,"oceanWaveSpeed");
            Range(recipe.flowSpeed,0,4,"flowSpeed");Range(recipe.normalStrength,0,.3f,"normalStrength");Range(recipe.foamStrength,0,1,"foamStrength");
            WaterFidelity.ValidateAppearance(recipe);
            nodes=new Dictionary<string,WaterNode>();bool first=true;
            foreach(var node in recipe.nodes)
            {
                if(node==null||string.IsNullOrWhiteSpace(node.id)||!Finite(node.position)||nodes.ContainsKey(node.id)||
                    !new[]{"source","junction","lake","mouth","ocean"}.Contains(node.kind))throw new ArgumentException("Unique finite water nodes required.");
                Range(node.radius.x,recipe.cellSize*2,300,"node radius");Range(node.radius.y,recipe.cellSize*2,300,"node radius");Range(node.bedDepth,.3f,8,"node bedDepth");
                nodes.Add(node.id,node);var b=new Bounds(node.position,new Vector3(node.radius.x*2,0,node.radius.y*2));
                if(first){Bounds=b;first=false;}else{var bounds=Bounds;bounds.Encapsulate(b);Bounds=bounds;}
            }
            if(recipe.nodes.Count(n=>n.kind=="ocean")!=1)throw new ArgumentException("Exactly one finite ocean body is required for this sample.");
            var ids=new HashSet<string>();
            foreach(var source in recipe.reaches)
            {
                if(source==null||string.IsNullOrWhiteSpace(source.id)||!ids.Add(source.id)||!nodes.TryGetValue(source.from,out var from)||
                    !nodes.TryGetValue(source.to,out var to)||from==to||from.kind=="ocean"||to.kind=="ocean"||source.knots==null||source.knots.Length<2)
                    throw new ArgumentException("Unique reaches must join explicit non-ocean nodes.");
                if(from.position.y<to.position.y)throw new ArgumentException("Reach flows uphill: "+source.id);
                if(Vector3.Distance(source.knots[0].position,from.position)>.001f||Vector3.Distance(source.knots[source.knots.Length-1].position,to.position)>.001f)
                    throw new ArgumentException("Reach endpoint differs from its shared node: "+source.id);
                Range(source.bedDepth,.3f,8,"reach bedDepth");Range(source.flowSpeed,0,4,"reach flowSpeed");
                foreach(var knot in source.knots)if(knot==null||knot.width<recipe.cellSize*4)throw new ArgumentException("Water widths must span at least four union cells.");
                AddReach(source,from,to);
            }
            ValidateGraph();
            var extent=Bounds.size;if(extent.x>700||extent.z>700)throw new ArgumentException("This connected sample is limited to a 700m patch.");
            ValidateDownstream();
        }

        void AddReach(WaterReach source,WaterNode from,WaterNode to)
        {
            var mesh=BworkRiverRibbon.Build(new RiverRecipe{id=source.id,knots=source.knots,sampleSpacing=recipe.cellSize*.5f,crossSegments=4,maximumWaveHeight=0});
            try
            {
                var vertices=mesh.vertices;var foamColors=mesh.colors;int count=vertices.Length/5;if(count>2048||segments.Count+count>12000)throw new ArgumentException("Water sampling budget exceeded.");
                var r=new Reach{recipe=source,centers=new Vector3[count],radii=new float[count],foam=new float[count]};var distance=new float[count];
                for(int i=0;i<count;i++)
                {
                    r.foam[i]=foamColors[i*5+2].g;
                    r.centers[i]=(vertices[i*5]+vertices[i*5+4])*.5f;r.radii[i]=Vector3.Distance(vertices[i*5],vertices[i*5+4])*.5f;
                    if(i>0)distance[i]=distance[i-1]+Vector2.Distance(XZ(r.centers[i-1]),XZ(r.centers[i]));
                }
                r.length=distance[count-1];float maximumRadius=r.radii.Max();
                // Flat collars cover every reach/body intersection, including the river's width.
                r.startCollar=Mathf.Max(from.radius.x,from.radius.y)+maximumRadius+recipe.cellSize*2;
                r.endCollar=Mathf.Max(to.radius.x,to.radius.y)+maximumRadius+recipe.cellSize*2;
                if(from.position.y!=to.position.y&&r.length<=r.startCollar+r.endCollar+recipe.cellSize)
                    throw new ArgumentException("Reach needs more length between flat connection collars: "+source.id);
                for(int i=0;i<count;i++)
                {
                    float t=from.position.y==to.position.y?0:Mathf.InverseLerp(r.startCollar,r.length-r.endCollar,distance[i]);
                    r.centers[i].y=Mathf.Lerp(from.position.y,to.position.y,Mathf.SmoothStep(0,1,t));
                }
                int reachIndex=reaches.Count;reaches.Add(r);
                for(int i=0;i<count-1;i++)
                {
                    var segment=new Segment(reachIndex,i,r.centers[i],r.centers[i+1],r.radii[i],r.radii[i+1]);int index=segments.Count;segments.Add(segment);
                    float margin=Mathf.Max(segment.ra,segment.rb)+recipe.bankFalloff+recipe.cellSize;
                    var min=Vector3.Min(segment.a,segment.b)-new Vector3(margin,0,margin);var max=Vector3.Max(segment.a,segment.b)+new Vector3(margin,0,margin);
                    for(int z=Bucket(min.z);z<=Bucket(max.z);z++)for(int x=Bucket(min.x);x<=Bucket(max.x);x++)
                    {var key=Key(x,z);if(!buckets.TryGetValue(key,out var values)){values=new List<int>();buckets.Add(key,values);}values.Add(index);}
                }
                var bounds=Bounds;bounds.Encapsulate(mesh.bounds);Bounds=bounds;
            }
            finally{Object.DestroyImmediate(mesh);}
        }

        void ValidateGraph()
        {
            var state=new Dictionary<string,int>();
            foreach(var node in recipe.nodes)if(node.kind!="ocean")Visit(node.id,state);
            var ocean=recipe.nodes.Single(n=>n.kind=="ocean");
            foreach(var mouth in recipe.nodes.Where(n=>n.kind=="mouth"))
                if(Mathf.Abs(mouth.position.y-ocean.position.y)>.001f||BodyDistance(ocean,XZ(mouth.position))>-recipe.cellSize*2)
                    throw new ArgumentException("Mouth nodes must lie inside the ocean at its exact level.");
            foreach(var node in recipe.nodes.Where(n=>n.kind!="ocean"))
            {
                int incoming=recipe.reaches.Count(r=>r.to==node.id),outgoing=recipe.reaches.Count(r=>r.from==node.id);
                if(node.kind=="source"?(incoming!=0||outgoing==0):node.kind=="mouth"?(incoming==0||outgoing!=0):(incoming==0||outgoing==0))
                    throw new ArgumentException("Node has invalid upstream/downstream ownership: "+node.id);
            }
        }
        void Visit(string id,Dictionary<string,int> state)
        {
            if(state.TryGetValue(id,out int prior)){if(prior==1)throw new ArgumentException("River cycles are unsupported.");return;}
            state[id]=1;foreach(var reach in recipe.reaches.Where(r=>r.from==id))Visit(reach.to,state);state[id]=2;
        }
        static float BodyDistance(WaterNode node,Vector2 p)
        {
            var d=p-XZ(node.position);
            if(node.kind=="ocean")
            {
                var q=new Vector2(Mathf.Abs(d.x)-node.radius.x,Mathf.Abs(d.y)-node.radius.y);
                return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0);
            }
            // Ellipse implicit distance is bounded and exact on its boundary; it is not an exact Euclidean SDF.
            return (new Vector2(d.x/node.radius.x,d.y/node.radius.y).magnitude-1)*Mathf.Min(node.radius.x,node.radius.y);
        }

        // Local sampling is sufficient for the wet union and its indexed carve corridor.
        public WaterFieldSample Sample(Vector2 point)=>SampleIndexed(point,0);

        /// <summary>Nearest field within a declared bank range (0–200 m); Distance is +infinity when none is in range.</summary>
        public WaterFieldSample SampleForBank(Vector2 point,float maximumDistance)
        {
            Range(maximumDistance,0,200,"bank query distance");
            if(!float.IsFinite(point.x)||!float.IsFinite(point.y))throw new ArgumentException("Finite bank query point required.");
            var sample=Sample(point);
            // Every reach within bankFalloff is already indexed in this point's bucket.
            if(maximumDistance>recipe.bankFalloff&&sample.Distance>recipe.bankFalloff)
                sample=SampleIndexed(point,maximumDistance);
            return sample.Distance<=maximumDistance?sample:new WaterFieldSample(float.PositiveInfinity,0,0,Vector2.zero,0);
        }

        /// <summary>Canonical lake/ocean footprints plus a conservative shore band; independent of shading blend weights.</summary>
        public bool IsNearLakeOrOcean(Vector2 point,float clearance)
        {
            Range(clearance,0,200,"lake/ocean clearance");
            if(!float.IsFinite(point.x)||!float.IsFinite(point.y))throw new ArgumentException("Finite shore query point required.");
            foreach(var node in recipe.nodes)
                if((node.kind=="lake"||node.kind=="ocean")&&BodyDistance(node,point)<=clearance)return true;
            return false;
        }

        WaterFieldSample SampleIndexed(Vector2 point,float searchRadius)
        {
            float distance=float.PositiveInfinity,height=0,depth=2,ocean=0,lake=0,foam=0;Vector2 flow=Vector2.zero;
            float weightSum=0,weightedHeight=0,weightedDepth=0,weightedFoam=0;Vector2 weightedFlow=Vector2.zero;
            float wetMin=float.PositiveInfinity,wetMax=float.NegativeInfinity;
            // Each reach contributes only its nearest segment, avoiding sampling-density weights.
            Span<float> nearest=stackalloc float[reaches.Count];Span<int> chosen=stackalloc int[reaches.Count];Span<float> ratios=stackalloc float[reaches.Count];
            for(int i=0;i<nearest.Length;i++){nearest[i]=float.PositiveInfinity;chosen[i]=-1;}
            // A segment is indexed across its entire width/corridor, so a square range around
            // the query includes every segment whose water edge is within that range. A bounded
            // stack bitset avoids evaluating the same segment once for each overlapping bucket.
            Span<ulong> visited=searchRadius>0?stackalloc ulong[(segments.Count+63)/64]:Span<ulong>.Empty;
            visited.Clear();
            int minX=Bucket(point.x-searchRadius),maxX=Bucket(point.x+searchRadius);
            int minZ=Bucket(point.y-searchRadius),maxZ=Bucket(point.y+searchRadius);
            for(long z=minZ;z<=maxZ;z++)for(long x=minX;x<=maxX;x++)
            if(buckets.TryGetValue(Key((int)x,(int)z),out var candidates))foreach(int index in candidates)
            {
                if(searchRadius>0)
                {
                    int word=index>>6;ulong mask=1UL<<(index&63);
                    if((visited[word]&mask)!=0)continue;
                    visited[word]|=mask;
                }
                var s=segments[index];var a=XZ(s.a);var delta=XZ(s.b)-a;float t=Mathf.Clamp01(Vector2.Dot(point-a,delta)/delta.sqrMagnitude);
                float d=Vector2.Distance(point,a+t*delta)-Mathf.Lerp(s.ra,s.rb,t);
                if(d<nearest[s.reach]){nearest[s.reach]=d;chosen[s.reach]=index;ratios[s.reach]=t;}
            }
            for(int i=0;i<reaches.Count;i++)if(chosen[i]>=0)
            {
                var s=segments[chosen[i]];float level=Mathf.Lerp(s.a.y,s.b.y,ratios[i]);var direction=(XZ(s.b)-XZ(s.a)).normalized*reaches[i].recipe.flowSpeed;
                Accumulate(nearest[i],level,reaches[i].recipe.bedDepth,direction,Mathf.Lerp(reaches[i].foam[s.indexInReach],reaches[i].foam[s.indexInReach+1],ratios[i]),ref foam,ref weightedFoam,ref distance,ref height,ref depth,ref flow,
                    ref weightSum,ref weightedHeight,ref weightedDepth,ref weightedFlow,ref wetMin,ref wetMax);
            }
            foreach(var node in recipe.nodes)
            {
                float d=BodyDistance(node,point);
                if(node.kind=="lake")lake=Mathf.Max(lake,Mathf.SmoothStep(0,1,Mathf.Clamp01(-d/12)));
                if(node.kind=="ocean")ocean=Mathf.SmoothStep(0,1,Mathf.Clamp01(-d/recipe.oceanBlendDistance));
                Accumulate(d,node.position.y,node.bedDepth,node.kind=="ocean"?new Vector2(.3f,.2f):Vector2.zero,0,ref foam,ref weightedFoam,
                    ref distance,ref height,ref depth,ref flow,ref weightSum,ref weightedHeight,ref weightedDepth,ref weightedFlow,ref wetMin,ref wetMax);
            }
            if(wetMax-wetMin>.025f)throw new ArgumentException("Incompatible water heights overlap near "+point+". Separate crossings or extend their flat collars.");
            if(weightSum>0){height=weightedHeight/weightSum;depth=weightedDepth/weightSum;flow=weightedFlow/weightSum;foam=weightedFoam/weightSum;}
            return new WaterFieldSample(distance,height,depth,flow,ocean,lake,foam);
        }
        static void Accumulate(float d,float y,float bed,Vector2 direction,float turbulence,ref float foam,ref float weightedFoam,ref float distance,ref float height,ref float depth,ref Vector2 flow,
            ref float weightSum,ref float weightedHeight,ref float weightedDepth,ref Vector2 weightedFlow,ref float wetMin,ref float wetMax)
        {
            if(d<distance){distance=d;height=y;depth=bed;flow=direction;foam=turbulence;}
            if(d>0)return;
            wetMin=Mathf.Min(wetMin,y);wetMax=Mathf.Max(wetMax,y);
            float weight=Mathf.Max(.0001f,-d);weightSum+=weight;weightedHeight+=y*weight;weightedDepth+=bed*weight;weightedFlow+=direction*weight;weightedFoam+=turbulence*weight;
        }
        void ValidateDownstream()
        {
            foreach(var reach in reaches)
            {
                float previous=float.PositiveInfinity;
                foreach(var center in reach.centers)
                {
                    var sample=Sample(XZ(center));
                    if(sample.Distance>0||sample.Height>previous+.002f)throw new ArgumentException("Union loses wet coverage or rises along "+reach.recipe.id);
                    previous=sample.Height;
                }
            }
        }

        public float[,] Carve(Terrain terrain,float[,] original)
        {
            int n=terrain.terrainData.heightmapResolution;
            if(original.GetLength(0)!=n||original.GetLength(1)!=n)throw new ArgumentException("Terrain grid mismatch.");
            var result=(float[,])original.Clone();var origin=terrain.transform.position;var size=terrain.terrainData.size;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                var p=new Vector2(origin.x+x*size.x/(n-1),origin.z+z*size.z/(n-1));var sample=Sample(p);
                if(sample.Distance>recipe.bankFalloff)continue;
                float before=origin.y+original[z,x]*size.y;
                float target=sample.Distance<=0?sample.Height-sample.BedDepth*Mathf.SmoothStep(0,1,Mathf.Clamp01(-sample.Distance/4)):
                    Mathf.Lerp(sample.Height,before,Mathf.SmoothStep(0,1,sample.Distance/recipe.bankFalloff));
                result[z,x]=Mathf.Clamp01((Mathf.Min(before,target)-origin.y)/size.y);
            }
            return result;
        }

        public Mesh BuildMesh()
        {
            float step=recipe.cellSize;int startX=Mathf.FloorToInt(Bounds.min.x/step)-1,startZ=Mathf.FloorToInt(Bounds.min.z/step)-1;
            int nx=Mathf.CeilToInt(Bounds.max.x/step)-startX+1,nz=Mathf.CeilToInt(Bounds.max.z/step)-startZ+1;
            if((long)(nx+1)*(nz+1)>300000)throw new ArgumentException("Union lattice budget exceeded.");
            var positions=new Vector2[(nx+1)*(nz+1)];var samples=new WaterFieldSample[positions.Length];
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){int i=z*(nx+1)+x;positions[i]=new Vector2((startX+x)*step,(startZ+z)*step);samples[i]=Sample(positions[i]);}
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var flow=new List<Vector2>();var colors=new List<Color>();var indices=new List<int>();
            var surfaceGradients=new List<Vector4>();var blendGradients=new List<Vector4>();
            var nodesToVertices=new Dictionary<int,int>();var edgesToVertices=new Dictionary<long,int>();
            int Vertex(int node)
            {
                if(nodesToVertices.TryGetValue(node,out int found))return found;
                int index=AddVertex(positions[node],samples[node],false);nodesToVertices.Add(node,index);return index;
            }
            int AddVertex(Vector2 p,WaterFieldSample s,bool boundary)
            {
                int index=vertices.Count;if(index>=300000)throw new ArgumentException("Union vertex budget exceeded.");
                vertices.Add(new Vector3(p.x,s.Height,p.y));uv.Add(p);flow.Add(s.Flow);
                float bank=boundary?0:Mathf.SmoothStep(0,1,Mathf.Clamp01(-s.Distance/recipe.waveBankFade));
                colors.Add(new Color(bank*bank,s.Foam,s.Ocean,s.Lake));
                // Bake smooth field derivatives once; shader normals never use triangle face derivatives.
                const float h=.25f;
                var left=Sample(p-new Vector2(h,0));var right=Sample(p+new Vector2(h,0));
                var back=Sample(p-new Vector2(0,h));var front=Sample(p+new Vector2(0,h));
                float Bank(WaterFieldSample sample){float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(-sample.Distance/recipe.waveBankFade));return t*t;}
                surfaceGradients.Add(new Vector4((right.Height-left.Height)/(2*h),(front.Height-back.Height)/(2*h),
                    boundary?0:(Bank(right)-Bank(left))/(2*h),boundary?0:(Bank(front)-Bank(back))/(2*h)));
                blendGradients.Add(new Vector4((right.Ocean-left.Ocean)/(2*h),(front.Ocean-back.Ocean)/(2*h),
                    (right.Lake-left.Lake)/(2*h),(front.Lake-back.Lake)/(2*h)));return index;
            }
            int Crossing(int a,int b)
            {
                if(a>b){int temporary=a;a=b;b=temporary;}
                if(Mathf.Abs(samples[a].Distance)<1e-6f)return Vertex(a);
                if(Mathf.Abs(samples[b].Distance)<1e-6f)return Vertex(b);
                long key=Key(a,b);if(edgesToVertices.TryGetValue(key,out int found))return found;
                float t=samples[a].Distance/(samples[a].Distance-samples[b].Distance);
                var p=Vector2.Lerp(positions[a],positions[b],t);int index=AddVertex(p,Sample(p),true);edgesToVertices.Add(key,index);return index;
            }
            void Triangle(int a,int b,int c)
            {
                Span<int> source=stackalloc int[3]{a,b,c};Span<int> polygon=stackalloc int[4];int polygonCount=0;
                for(int i=0;i<3;i++)
                {
                    int current=source[i],next=source[(i+1)%3];bool inside=samples[current].Distance<=0,nextInside=samples[next].Distance<=0;
                    if(inside)polygon[polygonCount++]=Vertex(current);if(inside!=nextInside)polygon[polygonCount++]=Crossing(current,next);
                }
                for(int i=1;i<polygonCount-1;i++)
                {
                    int ia=polygon[0],ib=polygon[i],ic=polygon[i+1];
                    if(ia==ib||ib==ic||ia==ic)continue;
                    float area=Vector3.Cross(vertices[ib]-vertices[ia],vertices[ic]-vertices[ia]).y;
                    if(area<=1e-7f)continue;indices.Add(ia);indices.Add(ib);indices.Add(ic);
                }
            }
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;Triangle(a,c,b);Triangle(b,c,d);}
            if(indices.Count==0)throw new ArgumentException("Union produced no wet triangles.");
            ValidateConnected(vertices.Count,indices);
            var mesh=new Mesh{name=recipe.id,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,flow);mesh.SetUVs(2,surfaceGradients);mesh.SetUVs(3,blendGradients);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(new Vector3(0,2*Mathf.Max(recipe.lakeWaveHeight,Mathf.Max(recipe.riverWaveHeight,recipe.oceanWaveHeight)),0));mesh.bounds=bounds;return mesh;
        }
        static void ValidateConnected(int count,List<int> triangles)
        {
            var parent=Enumerable.Range(0,count).ToArray();var used=new bool[count];
            int Find(int v){while(parent[v]!=v){parent[v]=parent[parent[v]];v=parent[v];}return v;}
            for(int i=0;i<triangles.Count;i+=3)
            {int a=triangles[i],b=triangles[i+1],c=triangles[i+2];parent[Find(b)]=Find(a);parent[Find(c)]=Find(a);used[a]=used[b]=used[c]=true;}
            int root=Find(triangles[0]);for(int i=0;i<count;i++)if(used[i]&&Find(i)!=root)throw new ArgumentException("Wet union has a disconnected component. Enlarge connections or reduce cellSize.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Bwork.FjordCoast.Roads.Editor
{
    // Pure Editor authoring math: these heights are not an accepted riding catalog.
    public static class FjordRoadProfiles
    {
        public sealed class Point
        {
            public double x,y,z;
            public Point(double x,double y,double z){this.x=x;this.y=y;this.z=z;}
        }
        public sealed class Route {public string id;public Point[] points;}
        public sealed class Result {public Route[] routes;public double maximumGradePercent,maximumChangeMeters;public int sharedNodes;}
        sealed class Node {public double guide;public int count;public readonly List<(int index,double distance)> edges=new List<(int,double)>();}
        public static Result Fit(Route[] routes,double grade=.24,Func<string,int,double?> minimumHeight=null)
        {
            if(routes==null||routes.Length==0||routes.Length>128||!(grade>0&&grade<=.25))throw new ArgumentException("Bounded road set and grade required");
            var nodes=new List<Node>();var lookup=new Dictionary<(long,long),int>();var indices=new List<int[]>();
            foreach(var route in routes)
            {
                if(route==null||route.points==null||route.points.Length<2||route.points.Length>100000)throw new ArgumentException("Bounded road samples required");
                var map=new int[route.points.Length];indices.Add(map);
                for(int i=0;i<map.Length;i++)
                {
                    var point=route.points[i];if(point==null||!double.IsFinite(point.x)||!double.IsFinite(point.y)||!double.IsFinite(point.z))throw new ArgumentException("Finite road point required");
                    var key=((long)Math.Round(point.x*100),(long)Math.Round(point.z*100));
                    if(!lookup.TryGetValue(key,out int index)){index=nodes.Count;lookup.Add(key,index);nodes.Add(new Node());}
                    map[i]=index;double guide=0;int count=0;
                    // Sixteen-meter source smoothing at the authored ~2m stations.
                    for(int j=Math.Max(0,i-8);j<=Math.Min(map.Length-1,i+8);j++){if(route.points[j]==null||!double.IsFinite(route.points[j].y))throw new ArgumentException("Finite guide required");if(Distance(point,route.points[j])>16)continue;guide+=route.points[j].y;count++;}
                    nodes[index].guide+=guide/count;nodes[index].count++;
                    if(i==0||map[i-1]==index)continue;
                    double distance=Distance(route.points[i-1],point);nodes[index].edges.Add((map[i-1],distance));nodes[map[i-1]].edges.Add((index,distance));
                }
            }
            if(nodes.Count>1000000)throw new ArgumentException("Road graph bound");
            foreach(var node in nodes)node.guide/=node.count;
            var lower=Envelope(nodes,grade,1);var inverseUpper=Envelope(nodes,grade,-1);
            for(int i=0;i<nodes.Count;i++)nodes[i].guide=(lower[i]-inverseUpper[i])*.5;
            if(minimumHeight!=null)
            {
                for(int r=0;r<routes.Length;r++)for(int i=0;i<routes[r].points.Length;i++)
                {
                    var floor=minimumHeight(routes[r].id,i);
                    if(floor.HasValue){if(!double.IsFinite(floor.Value))throw new ArgumentException("Finite road floor required");int k=indices[r][i];nodes[k].guide=Math.Max(nodes[k].guide,floor.Value);}
                }
                var raised=Envelope(nodes,grade,-1);for(int i=0;i<nodes.Count;i++)nodes[i].guide=-raised[i];
            }
            var result=new Result{routes=new Route[routes.Length],sharedNodes=nodes.Count(n=>n.count>1)};
            for(int r=0;r<routes.Length;r++)
            {
                var old=routes[r];var road=new Route{id=old.id,points=new Point[old.points.Length]};result.routes[r]=road;
                for(int i=0;i<road.points.Length;i++)
                {
                    int index=indices[r][i];var original=old.points[i];double y=nodes[index].guide;
                    road.points[i]=new Point(original.x,y,original.z);result.maximumChangeMeters=Math.Max(result.maximumChangeMeters,Math.Abs(y-original.y));
                    if(i>0){double distance=Distance(road.points[i-1],road.points[i]);if(distance>.00001)result.maximumGradePercent=Math.Max(result.maximumGradePercent,Math.Abs(y-road.points[i-1].y)/distance*100);}
                }
            }
            return result;
        }
        static double Distance(Point a,Point b)=>Math.Sqrt((b.x-a.x)*(b.x-a.x)+(b.z-a.z)*(b.z-a.z));
        // The min-plus envelope and negated envelope are both grade-Lipschitz.
        // Their midpoint preserves that bound across loops and shared junctions.
        static double[] Envelope(List<Node> nodes,double grade,double sign)
        {
            var distances=nodes.Select(n=>n.guide*sign).ToArray();var pending=new SortedSet<(double cost,int index)>();
            for(int i=0;i<nodes.Count;i++)pending.Add((distances[i],i));
            while(pending.Count>0)
            {
                var current=pending.Min;pending.Remove(current);
                foreach(var edge in nodes[current.index].edges)
                {
                    double next=current.cost+edge.distance*grade;if(next>=distances[edge.index])continue;
                    pending.Remove((distances[edge.index],edge.index));distances[edge.index]=next;pending.Add((next,edge.index));
                }
            }
            return distances;
        }
    }
}

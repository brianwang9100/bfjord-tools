using System;
using UnityEngine;

namespace Bwork.Authoring.WaterSandbox
{
    /// <summary>Pure bounds and mapping shared by water appearance admission and its tests.</summary>
    public static class WaterFidelity
    {
        public static void ValidateAppearance(ConnectedWaterRecipe r)
        {
            if(!float.IsFinite(r.oceanWaveDirection.x)||!float.IsFinite(r.oceanWaveDirection.y)||
                r.oceanWaveDirection.sqrMagnitude<.0001f||r.oceanWaveDirection.sqrMagnitude>16)
                throw new ArgumentException("A finite nonzero ocean wave direction of length at most four is required.");
            Range(r.riverCurrentStrength,0,1,"riverCurrentStrength");
            Range(r.riverStreakScale,.25f,4,"riverStreakScale");
            Range(r.riverTurbulence,0,1,"riverTurbulence");
            Range(r.oceanSurfaceStrength,0,1,"oceanSurfaceStrength");
            Range(r.oceanWaveSharpness,0,1,"oceanWaveSharpness");
            Range(r.oceanShoreFoam,0,1,"oceanShoreFoam");
            Range(r.oceanBreakerStrength,0,1,"oceanBreakerStrength");
            Range(r.oceanBeachDepth,.5f,8,"oceanBeachDepth");
            Range(r.oceanSwashSpeed,0,2,"oceanSwashSpeed");
            Range(r.oceanSwashDepthSpacing,.3f,4,"oceanSwashDepthSpacing");
        }

        /// <summary>Only appearance crosses into an existing receipt; geometry and displacement bounds stay owned.</summary>
        public static void CopyAppearance(ConnectedWaterRecipe installed,ConnectedWaterRecipe requested)
        {
            foreach(string name in new[]{"flowSpeed","normalStrength","foamStrength","rippleTileSize","detailTileSize","detailStrength",
                "smoothness","oceanSmoothness","depthColorDistance","shallowOpacity","deepOpacity","shoreFadeDepth",
                "foamWidth","foamTileSize","foamCutoff","crestFoamStrength","shallowColor","deepColor","oceanShallowColor","oceanDeepColor",
                "riverCurrentStrength","riverStreakScale","riverTurbulence","oceanSurfaceStrength","oceanWaveSharpness",
                "oceanShoreFoam","oceanBreakerStrength","oceanBeachDepth","oceanSwashSpeed","oceanSwashDepthSpacing","waterPatternSeed","oceanWaveDirection"})
            {
                var field=typeof(ConnectedWaterRecipe).GetField(name);
                field.SetValue(installed,field.GetValue(requested));
            }
        }

        static void Range(float value,float min,float max,string name)
        {
            if(!float.IsFinite(value)||value<min||value>max)throw new ArgumentException(name+" out of range.");
        }

        public static Vector2 PatternOffset(int seed)
        {
            if(seed==0)return Vector2.zero; // Keep legacy maps at their original origin.
            unchecked
            {
                uint value=(uint)seed;
                value=(value^(value>>16))*0x7feb352d;
                value=(value^(value>>15))*0x846ca68b;
                value^=value>>16;
                return new Vector2((value&65535)/65536f,(value>>16)/65536f);
            }
        }

        /// <summary>Local downstream frame. A fixed texture feature travels in +flow as time grows.</summary>
        public static Vector2 CurrentCoordinates(Vector2 point,Vector2 flow,float time,float speed,float scale=1)
        {
            var bounded=Vector2.ClampMagnitude(flow,4);
            var along=bounded.sqrMagnitude>1e-6f?bounded.normalized:Vector2.up;
            var across=new Vector2(along.y,-along.x);
            var advected=point-bounded*(time*speed);
            return new Vector2(Vector2.Dot(advected,across)/8,Vector2.Dot(advected,along)/24)*scale;
        }

        /// <summary>Convex wave shaping stays within the mesh's existing +/- amplitude envelope.</summary>
        public static float SharpenedWave(float phase,float sharpness)
        {
            float harmonic=.28f*Mathf.Clamp01(sharpness);
            return (1-harmonic)*Mathf.Sin(phase)-harmonic*Mathf.Cos(2*phase);
        }
    }
}

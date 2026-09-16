Shader "Bwork/Sandbox/Connected Wave Water"
{
    Properties
    {
        _BaseColor("River deep water", Color) = (.025,.105,.10,1)
        _ShallowColor("River shallow water", Color) = (.16,.30,.25,1)
        _OceanBaseColor("Ocean deep water", Color) = (.025,.085,.125,1)
        _OceanShallowColor("Ocean shallow water", Color) = (.12,.27,.29,1)
        _WaveHeight("River displacement metres", Range(0,1)) = .12
        _WaveLength("River wave length metres", Range(4,80)) = 14
        _WaveSpeed("River phase speed", Range(0,3)) = .8
        _LakeWaveHeight("Lake displacement metres", Range(0,1)) = .06
        _LakeWaveLength("Lake wave length metres", Range(4,80)) = 18
        _LakeWaveSpeed("Lake phase speed", Range(0,3)) = .4
        _OceanWaveHeight("Ocean displacement metres", Range(0,1)) = .45
        _OceanWaveLength("Ocean wave length metres", Range(4,80)) = 28
        _OceanWaveSpeed("Ocean phase speed", Range(0,3)) = .65
        [NoScaleOffset] _RippleNormal("Original linear RGB ripple normal", 2D) = "bump" {}
        [NoScaleOffset] _DetailNormal("Original linear RGB detail normal", 2D) = "bump" {}
        [NoScaleOffset] _FoamMap("Original foam R / variation G", 2D) = "gray" {}
        [NoScaleOffset] _RiverMotionMap("Original flowing river threads", 2D) = "black" {}
        [NoScaleOffset] _OceanMotionMap("Original broken ocean crests", 2D) = "black" {}
        _RiverCurrentStrength("River current foam", Range(0,1)) = .22
        _OceanSurfaceStrength("Broad ocean foam", Range(0,1)) = .32
        _RiverStreakScale("Downstream streak frequency", Range(.25,4)) = 1
        _RiverTurbulence("Shallow river aeration", Range(0,1)) = .23
        _OceanWaveSharpness("Bounded swell crest sharpening", Range(0,1)) = 0
        _OceanShoreFoam("Persistent shore wash", Range(0,1)) = 0
        _OceanBreakerStrength("Beach breaking fronts", Range(0,1)) = 0
        _OceanBeachDepth("Breaking zone depth metres", Range(.5,8)) = 3
        _OceanSwashSpeed("Swash cycles per second", Range(0,2)) = .6
        _OceanSwashDepthSpacing("Swash depth spacing metres", Range(.3,4)) = 1.1
        _OceanSwashRunupHeight("Visual runup height metres", Range(0,1)) = 0
        _OceanSwashRunupDistance("Visual runup apron metres", Range(0,24)) = 0
        _OceanSwashPeriod("Runup period seconds", Range(4,16)) = 8
        _OceanWaveDirection("Ocean travel direction X/Z", Vector) = (.8,.6,0,0)
        [HideInInspector] _OceanBounds("Canonical ocean center XZ / radii XZ", Vector) = (0,0,0,0)
        [HideInInspector] _PatternOffset("Seeded pattern origin", Vector) = (0,0,0,0)
        [HideInInspector] _AnimationTime("Capture time; negative uses live time", Float) = -1
        _RippleTileSize("Ripple tile metres", Float) = 5
        _DetailTileSize("Detail tile metres", Float) = 1.6
        _DetailStrength("Detail normal weight", Range(0,1)) = .4
        _FlowSpeed("Flow multiplier", Range(0,4)) = .8
        _NormalStrength("Ripple slope", Range(0,.3)) = .18
        _FoamStrength("Shore and authored foam", Range(0,1)) = .2
        _FoamWidth("Foam depth metres", Float) = .65
        _FoamTileSize("Foam tile metres", Float) = 4
        _FoamCutoff("Foam breakup threshold", Range(.1,.9)) = .52
        _CrestFoamStrength("Ocean crest foam", Range(0,1)) = .08
        _Smoothness("River/lake smoothness", Range(0,.98)) = .82
        _OceanSmoothness("Ocean smoothness", Range(0,.98)) = .86
        _DepthColorDistance("Absorption distance metres", Float) = 4
        _ShallowOpacity("Shallow opacity", Range(0,1)) = .18
        _DeepOpacity("Deep opacity", Range(0,1)) = 1
        _ShoreFadeDepth("Shore fade depth metres", Float) = .3
        _UseDepth("Owner guarantees current depth texture", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #define _SPECULAR_SETUP 1
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_RippleNormal); SAMPLER(sampler_RippleNormal);
            TEXTURE2D(_DetailNormal); SAMPLER(sampler_DetailNormal);
            TEXTURE2D(_FoamMap); SAMPLER(sampler_FoamMap);
            TEXTURE2D(_RiverMotionMap); SAMPLER(sampler_RiverMotionMap);
            TEXTURE2D(_OceanMotionMap); SAMPLER(sampler_OceanMotionMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ShallowColor, _OceanBaseColor, _OceanShallowColor;
            float _WaveHeight,_WaveLength,_WaveSpeed,_LakeWaveHeight,_LakeWaveLength,_LakeWaveSpeed;
            float _OceanWaveHeight,_OceanWaveLength,_OceanWaveSpeed;
            float _FlowSpeed,_NormalStrength,_FoamStrength,_Smoothness,_UseDepth;
            float _RippleTileSize,_DetailTileSize,_DetailStrength,_FoamWidth,_FoamTileSize,_FoamCutoff,_CrestFoamStrength;
            float _OceanSmoothness,_DepthColorDistance,_ShallowOpacity,_DeepOpacity,_ShoreFadeDepth;
            float _RiverCurrentStrength,_OceanSurfaceStrength,_AnimationTime;
            float _RiverStreakScale,_RiverTurbulence,_OceanWaveSharpness,_OceanShoreFoam,_OceanBreakerStrength;
            float _OceanBeachDepth,_OceanSwashSpeed,_OceanSwashDepthSpacing;
            float _OceanSwashRunupHeight,_OceanSwashRunupDistance,_OceanSwashPeriod;
            float4 _PatternOffset,_OceanWaveDirection,_OceanBounds;
            CBUFFER_END
            float WaterTime(){return _AnimationTime>=0?_AnimationTime:_Time.y;}
            struct Attributes
            {
                float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 flow:TEXCOORD1;
                float4 gradients:TEXCOORD2; float4 blendGradients:TEXCOORD3; float4 swash:TEXCOORD4; float4 color:COLOR;
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1;
                float2 flow:TEXCOORD2; float4 color:TEXCOORD3; float4 gradients:TEXCOORD4;
                float4 blendGradients:TEXCOORD5; float fog:TEXCOORD6; float4 swash:TEXCOORD7;
            };
            // Value and exact x/z derivatives share phase, direction, weights and amplitude.
            float3 Wave(float2 p,float height,float length,float speed,float sharpness,float2 direction)
            {
                float k=6.283185307/max(length,4),time=WaterTime()*speed;
                float2 d0=direction*rsqrt(max(dot(direction,direction),.0001));
                float2 side=float2(-d0.y,d0.x);
                float2 d1=normalize(d0*.2085+side*.978),d2=d0*.6-side*.8;
                float3 phase=float3(dot(p,d0)*k-time,dot(p,d1)*k*1.71-time*1.31,dot(p,d2)*k*2.63-time*1.62);
                // Convex harmonics sharpen a crest without exceeding the existing CPU bounds.
                // Derivatives are of the same function used for vertex displacement.
                float harmonic=.28*saturate(sharpness);
                float3 s=(1-harmonic)*sin(phase)-harmonic*cos(2*phase);
                float3 c=(1-harmonic)*cos(phase)+2*harmonic*sin(2*phase);
                float2 gradient=k*(.62*c.x*d0+.26*1.71*c.y*d1+.12*2.63*c.z*d2);
                // Positive weights sum to one: the CPU's displacement envelope remains conservative.
                return height*float3(dot(s,float3(.62,.26,.12)),gradient);
            }
            float3 WaveField(float2 p,float4 color,float4 blendGradients)
            {
                float ocean=saturate(color.b),lake=saturate(color.a);
                float3 river=Wave(p,_WaveHeight,_WaveLength,_WaveSpeed,0,float2(.8,.6));
                float3 sheltered=Wave(p,_LakeWaveHeight,_LakeWaveLength,_LakeWaveSpeed,0,float2(.8,.6));
                float3 sea=Wave(p,_OceanWaveHeight,_OceanWaveLength,_OceanWaveSpeed,_OceanWaveSharpness,_OceanWaveDirection.xy);
                float3 inland=lerp(river,sheltered,lake);
                inland.yz+=(sheltered.x-river.x)*blendGradients.zw;
                float3 result=lerp(inland,sea,ocean);
                result.yz+=(sea.x-inland.x)*blendGradients.xy;
                return result;
            }
            // Analytic shore-normal runup. The signed distance is baked from the ocean
            // boundary, so the same wave front follows each bank without a camera-space offset.
            float3 RunupField(float2 p,float4 data)
            {
                if(_UseDepth<.5||_OceanSwashRunupDistance<=0||_OceanSwashRunupHeight<=0)return 0;
                float fadeWidth=min(4,_OceanSwashRunupDistance);
                float a=saturate((data.x+18)/12),b=saturate((data.x-_OceanSwashRunupDistance+fadeWidth)/fadeWidth);
                float inward=a*a*(3-2*a),outward=1-b*b*(3-2*b);
                float envelope=inward*outward;
                float derivative=(6*a*(1-a)/12)*outward-inward*(6*b*(1-b)/fadeWidth);
                // A broad oblique variation breaks an unnaturally synchronous straight front.
                float phase=data.x*.34906585-WaterTime()*6.283185307/max(_OceanSwashPeriod,4)+p.x*.025;
                float scale=_OceanSwashRunupHeight*saturate(data.w);
                float value=scale*envelope*sin(phase);
                float2 gradient=scale*(derivative*sin(phase)*data.yz+
                    envelope*cos(phase)*(data.yz*.34906585+float2(.025,0)));
                return float3(value,gradient);
            }
            float RunupCoverage(float4 data)
            {
                if(_UseDepth<.5||_OceanSwashRunupDistance<=0||_OceanSwashRunupHeight<=0)return 0;
                return saturate(data.w)*smoothstep(-18,-6,data.x)*
                    (1-smoothstep(max(0,_OceanSwashRunupDistance-2),_OceanSwashRunupDistance,data.x));
            }
            Varyings Vert(Attributes i)
            {
                Varyings o=(Varyings)0;
                o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
                o.positionWS.y+=WaveField(i.uv,i.color,i.blendGradients).x*saturate(i.color.r)+RunupField(i.uv,i.swash).x;
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.swash=i.swash;o.uv=i.uv;o.flow=i.flow;o.color=i.color;o.gradients=i.gradients;o.blendGradients=i.blendGradients;
                o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            float2 DecodeSlope(half3 encoded)
            {
                float3 n=encoded*2-1;
                return -n.xy/max(n.z,.25);
            }
            float2 Ripple(float2 p,float detailFade)
            {
                // Different rotated scales break the shared square repeat. Rotate the sampled
                // slopes back as well: the normal must follow the mapped ripple direction.
                const float2x2 rotation=float2x2(.8,-.6,.6,.8);
                const float2x2 inverseRotation=float2x2(.8,.6,-.6,.8);
                float2 broadUV=p/max(_RippleTileSize,.5);
                float2 broad=DecodeSlope(SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,broadUV).rgb);
                float2 crossing=DecodeSlope(SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,
                    mul(rotation,broadUV)*.713+float2(.371,.619)).rgb);
                broad=broad*.6+mul(inverseRotation,crossing)*.4;
                float2 detail=DecodeSlope(SAMPLE_TEXTURE2D(_DetailNormal,sampler_DetailNormal,
                    mul(rotation,p)/max(_DetailTileSize,.25)+float2(.173,.319)).rgb);
                return broad+mul(inverseRotation,detail)*(_DetailStrength*detailFade);
            }
            half3 RiverTile(float2 sampleXZ,float2 anchor,float2 along,float2 across,float2 direction,float phase)
            {
                float2 local=sampleXZ-anchor-direction*(8*phase);
                float2 uv=float2(dot(local,across)/8,dot(local,along)/24)*_RiverStreakScale;
                // Every anchor gets a reproducible offset; rotation acts on bounded local metres.
                uv+=frac(anchor*float2(.173,.319))+_PatternOffset.xy;
                return SAMPLE_TEXTURE2D(_RiverMotionMap,sampler_RiverMotionMap,uv).rgb;
            }
            half3 RiverPattern(float2 sampleXZ,float2 along,float2 across,float2 direction,float phase)
            {
                const float tile=16;
                float2 cell=floor(sampleXZ/tile),anchor=cell*tile;
                float2 f=frac(sampleXZ/tile);f=f*f*(3-2*f);
                half3 a=RiverTile(sampleXZ,anchor,along,across,direction,phase);
                half3 b=RiverTile(sampleXZ,anchor+float2(tile,0),along,across,direction,phase);
                half3 c=RiverTile(sampleXZ,anchor+float2(0,tile),along,across,direction,phase);
                half3 d=RiverTile(sampleXZ,anchor+tile,along,across,direction,phase);
                return lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y);
            }
            float OceanShadingWeight(float2 sampleXZ,float legacy)
            {
                if(min(_OceanBounds.z,_OceanBounds.w)<1)return legacy;
                float2 edge=abs(sampleXZ-_OceanBounds.xy)-_OceanBounds.zw;
                // Shading reaches the shoreline; the old broad amplitude collar is a different field.
                return max(legacy,1-smoothstep(-1,1,max(edge.x,edge.y)));
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 wave=WaveField(i.uv,i.color,i.blendGradients);
                float3 runup=RunupField(i.uv,i.swash);float runupCoverage=RunupCoverage(i.swash);
                float2 slope=i.gradients.xy+wave.yz*saturate(i.color.r)+wave.x*i.gradients.zw+runup.yz;
                float2 direction=i.flow*min(1,4*rsqrt(max(dot(i.flow,i.flow),.0001)));
                // Dual phase advection resets without a visible snap; distance is in world metres.
                float phase0=frac(WaterTime()*_FlowSpeed*.125),phase1=frac(phase0+.5),weight=1-abs(phase0*2-1);
                float2 drift=float2(.035,.021)*WaterTime()*_FlowSpeed;
                float2 p0=i.uv-direction*(8*phase0)-drift,p1=i.uv-direction*(8*phase1)-drift;
                // Mips and slope attenuation suppress distant grazing-angle sparkle.
                float distanceToCamera=distance(GetCameraPositionWS(),i.positionWS);
                float detailFade=1-smoothstep(12,75,distanceToCamera);
                // Sea ripples cover a larger area than the river, with a coherent wind drift.
                float ocean=max(OceanShadingWeight(i.uv,saturate(i.color.b)),runupCoverage),river=(1-ocean)*(1-saturate(i.color.a));
                float2 oceanDirection=_OceanWaveDirection.xy*rsqrt(max(dot(_OceanWaveDirection.xy,_OceanWaveDirection.xy),.0001));
                float2 oceanDrift=oceanDirection*.255*WaterTime()*_OceanWaveSpeed;
                // Blend sampled normals, never world coordinates: a spatial UV lerp stretches
                // the map across the entire ocean collar and produced the old plastic bands.
                float2 riverRipple=0;
                half3 current=0;
                float flowLength=length(direction);
                float2 along=direction/max(flowLength,.001),across=float2(along.y,-along.x);
                if(ocean<.999)riverRipple=lerp(Ripple(p1,detailFade),Ripple(p0,detailFade),weight);
                if(river>.001)
                {
                    current=lerp(RiverPattern(i.uv,along,across,direction,phase1),
                        RiverPattern(i.uv,along,across,direction,phase0),weight);
                }
                float2 seaUV=(i.uv-oceanDrift)/6+_PatternOffset.xy;
                float2 seaNormal=DecodeSlope(SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,seaUV).rgb);
                float2 seaCross=DecodeSlope(SAMPLE_TEXTURE2D(_DetailNormal,sampler_DetailNormal,
                    mul(float2x2(.6,-.8,.8,.6),seaUV)*.73+float2(.13,.41)).rgb);
                seaNormal=seaNormal*.55+mul(float2x2(.6,.8,-.8,.6),seaCross)*.45;
                float2 ripple=lerp(riverRipple,seaNormal,ocean);
                slope+=ripple*lerp(_NormalStrength,.038,ocean)*lerp(.18,1,detailFade)*lerp(.18,1,saturate(i.color.r));
                float currentMask=river*saturate(flowLength)*saturate(i.color.r);
                slope+=across*(current.b-.5)*lerp(.03,.18,_RiverCurrentStrength)*currentMask*lerp(.2,1,detailFade);
                // Wind cross-ripples retain a finer surface beneath the long swell.
                float windRipple=sin(dot(i.uv,float2(.93,-.36))*3.9-WaterTime()*1.7);
                slope+=float2(.93,-.36)*windRipple*.018*ocean*detailFade;
                float3 n=normalize(float3(-slope.x,1,-slope.y));
                float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                float depth=_DepthColorDistance,shoreDepth=_DepthColorDistance;
                if(_UseDepth>.5)
                {
                    float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);
                    float raw=SampleSceneDepth(screenUV);
                    #if !UNITY_REVERSED_Z
                        raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                    #endif
                    float3 bedWS=ComputeWorldSpacePosition(screenUV,raw,UNITY_MATRIX_I_VP);
                    // Optical absorption follows the view ray. Shore fade/foam use actual
                    // vertical depth, so a grazing camera cannot turn a shallow bank opaque.
                    shoreDepth=max(0,i.positionWS.y-bedWS.y);
                    depth=distance(i.positionWS,bedWS);
                }
                half2 grain=lerp(SAMPLE_TEXTURE2D(_FoamMap,sampler_FoamMap,p1/max(_FoamTileSize,.5)+_PatternOffset.xy).rg,
                    SAMPLE_TEXTURE2D(_FoamMap,sampler_FoamMap,p0/max(_FoamTileSize,.5)+_PatternOffset.xy).rg,weight);
                // Foam occupies irregular thin patches; a uniform depth band reads as paint.
                float shoreWidth=max(.05,_FoamWidth)*lerp(.38,1.2,grain.g);
                float shore=_UseDepth>.5?1-smoothstep(0,shoreWidth,shoreDepth):0;
                float breakup=smoothstep(_FoamCutoff-.13,_FoamCutoff+.14,grain.r)*smoothstep(.18,.62,grain.g);
                half3 seaPattern=SAMPLE_TEXTURE2D(_OceanMotionMap,sampler_OceanMotionMap,
                    (i.uv-oceanDrift)/11+_PatternOffset.xy).rgb;
                // Broken fronts cover raised swell shoulders and strengthen toward the wind. Crossing
                // patches stop all three analytic wavelengths from drawing parallel foam bands.
                half3 crossingSea=SAMPLE_TEXTURE2D(_OceanMotionMap,sampler_OceanMotionMap,
                    mul(float2x2(.8,-.6,.6,.8),i.uv-oceanDrift*.63)/4.3+float2(.31,.67)+_PatternOffset.xy).rgb;
                float seaCrest=smoothstep(-.14,.43,wave.x/max(_OceanWaveHeight,.001));
                float windFace=smoothstep(-.10,.13,dot(wave.yz,oceanDirection));
                // Coverage layers are modulated, not added until whole crests saturate white.
                // The two footprints give large torn rafts and smaller connected foam rims.
                float lace=saturate(seaPattern.r*.78+crossingSea.r*.48);
                float porous=smoothstep(.035,.46,lace)*lerp(.78,1,crossingSea.b);
                float seaFoam=porous*seaCrest*lerp(.62,1,windFace)*_OceanSurfaceStrength*ocean;
                // Blended flow tiles retain thin coverage; a high post-blend threshold erased
                // those strands and let the old isotropic authored foam dominate the river.
                float filaments=smoothstep(.012,.20,current.r)*lerp(.72,1,current.g);
                float currentFoam=filaments*currentMask*_RiverCurrentStrength;
                float shallowCurrent=_UseDepth>.5?(1-smoothstep(.25,1.8,shoreDepth))*currentMask:0;
                float turbulence=shallowCurrent*filaments*lerp(.45,1,current.b)*_RiverTurbulence;
                // Depth contours follow the actual opaque bank, including curved beaches. Subtract
                // displacement to keep the breaking zone anchored to the resting surface. Increasing
                // phase moves fronts to smaller depths (toward shore), never out toward deep water.
                float restingDepth=max(0,shoreDepth-wave.x*saturate(i.color.r)-runup.x);
                float beachZone=(_UseDepth>.5?1-smoothstep(_OceanBeachDepth*.45,_OceanBeachDepth,restingDepth):0)*ocean;
                float swashPhase=restingDepth/max(_OceanSwashDepthSpacing,.3)+WaterTime()*_OceanSwashSpeed;
                swashPhase+=(grain.g-.5)*.12;
                float swash=frac(swashPhase);
                float front=smoothstep(.46,.67,swash)*(1-smoothstep(.78,.96,swash));
                float trailingWash=smoothstep(.06,.22,swash)*(1-smoothstep(.42,.76,swash));
                float beachFoam=beachZone*porous*max(front*_OceanBreakerStrength,
                    trailingWash*_OceanShoreFoam*.68);
                // Thin water at the moving terrain intersection carries foam even on the
                // retreat; the contact is displaced geometry, never a painted stationary edge.
                float movingContact=(1-smoothstep(.04,.30,shoreDepth))*runupCoverage;
                float washAtEdge=max(shore*.74,movingContact)*ocean*_OceanShoreFoam*porous;
                float breakingCap=seaCrest*porous*_OceanBreakerStrength*ocean*lerp(.55,1,beachZone);
                // Inland banks keep a narrow contact line, while authored whitewater is carried
                // by the same downstream strands. Isotropic map pores never cut moving filaments.
                float contactLine=(_UseDepth>.5?1-smoothstep(.04,.32,shoreDepth):0)*(1-ocean);
                float inlandFoam=max(max(currentFoam,turbulence),
                    filaments*saturate(i.color.g)*_FoamStrength*river);
                inlandFoam=max(inlandFoam,contactLine*breakup*_FoamStrength*.55);
                float oceanFoam=max(max(seaFoam,breakingCap),max(beachFoam,washAtEdge));
                oceanFoam=max(oceanFoam,seaCrest*porous*_CrestFoamStrength*ocean);
                float lakeFoam=shore*breakup*_FoamStrength*(1-ocean)*saturate(i.color.a);
                float foam=saturate(max(inlandFoam,max(oceanFoam,lakeFoam)));
                // Near contact, bound the optical path by vertical depth before returning
                // to view-ray absorption offshore; grazing banks keep the bed visible.
                float opticalDepth=lerp(min(depth,shoreDepth*2.5),depth,smoothstep(.15,1.4,shoreDepth));
                float absorption=1-exp2(-opticalDepth/max(_DepthColorDistance,.2)*1.8);
                SurfaceData surface=(SurfaceData)0;
                half3 shallow=lerp(_ShallowColor.rgb,_OceanShallowColor.rgb,ocean);
                half3 deep=lerp(_BaseColor.rgb,_OceanBaseColor.rgb,ocean);
                // A restrained submerged contact tint joins transparent shallows to wet bank materials.
                half3 waterTint=lerp(shallow,deep,absorption)*lerp(1,.86,shore*(1-breakup));
                surface.albedo=lerp(waterTint,half3(.93,.96,.94),foam);
                float surfaceSmoothness=lerp(_Smoothness,_OceanSmoothness,ocean)-.045*(1-detailFade);
                surface.smoothness=lerp(surfaceSmoothness+.025*(grain.g-.5),.38,foam);
                // Water's normal-incidence dielectric reflectance is about two percent.
                surface.specular=lerp(half3(.02,.02,.02),half3(.04,.04,.04),foam);
                surface.normalTS=half3(0,0,1);surface.occlusion=1;
                float fresnel=pow(1-saturate(dot(n,view)),5);
                // Ordinary deep water becomes opaque; an explicit lower DeepOpacity
                // remains available for artistic transparency profiles.
                float alpha=saturate(lerp(_ShallowOpacity,_DeepOpacity,absorption)+fresnel*.14+foam*.3);
                // The canonical footprint fades its wave envelope to zero at clipped mesh
                // edges. Reuse that bounded field to feather coverage horizontally as
                // well as vertically, including river banks and shallow lake margins.
                float edgeCoverage=max(smoothstep(0,lerp(.045,.10,grain.g),saturate(i.color.r)),runupCoverage);
                float contactFade=lerp(max(.05,_ShoreFadeDepth),.06,runupCoverage);
                float depthCoverage=_UseDepth>.5?smoothstep(0,contactFade,shoreDepth):1;
                surface.alpha=(_UseDepth>.5?alpha:_DeepOpacity)*edgeCoverage*depthCoverage;
                InputData lighting=(InputData)0;lighting.positionWS=i.positionWS;lighting.normalWS=n;
                lighting.viewDirectionWS=view;lighting.shadowCoord=TransformWorldToShadowCoord(i.positionWS);lighting.fogCoord=i.fog;
                lighting.bakedGI=SampleSH(n);lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                lighting.shadowMask=half4(1,1,1,1);
                half4 color=UniversalFragmentPBR(lighting,surface);color.rgb=MixFog(color.rgb,i.fog);color.a=surface.alpha;return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}

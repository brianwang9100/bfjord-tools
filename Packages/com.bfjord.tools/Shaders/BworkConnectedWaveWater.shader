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
        _RiverCurrentStrength("River current foam", Range(0,1)) = .11
        _OceanSurfaceStrength("Broad ocean foam", Range(0,1)) = .10
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
            CBUFFER_END
            float WaterTime(){return _AnimationTime>=0?_AnimationTime:_Time.y;}
            struct Attributes
            {
                float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 flow:TEXCOORD1;
                float4 gradients:TEXCOORD2; float4 blendGradients:TEXCOORD3; float4 color:COLOR;
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1;
                float2 flow:TEXCOORD2; float4 color:TEXCOORD3; float4 gradients:TEXCOORD4;
                float4 blendGradients:TEXCOORD5; float fog:TEXCOORD6;
            };
            // Value and exact x/z derivatives share phase, direction, weights and amplitude.
            float3 Wave(float2 p,float height,float length,float speed)
            {
                float k=6.283185307/max(length,4),time=WaterTime()*speed;
                float2 d0=float2(.8,.6),d1=normalize(float2(-.42,.9075)),d2=float2(.96,-.28);
                float3 phase=float3(dot(p,d0)*k-time,dot(p,d1)*k*1.71-time*1.31,dot(p,d2)*k*2.63-time*1.62);
                float3 s=sin(phase),c=cos(phase);
                float2 gradient=k*(.62*c.x*d0+.26*1.71*c.y*d1+.12*2.63*c.z*d2);
                // Positive weights sum to one: the CPU's displacement envelope remains conservative.
                return height*float3(dot(s,float3(.62,.26,.12)),gradient);
            }
            float3 WaveField(float2 p,float4 color,float4 blendGradients)
            {
                float ocean=saturate(color.b),lake=saturate(color.a);
                float3 river=Wave(p,_WaveHeight,_WaveLength,_WaveSpeed);
                float3 sheltered=Wave(p,_LakeWaveHeight,_LakeWaveLength,_LakeWaveSpeed);
                float3 sea=Wave(p,_OceanWaveHeight,_OceanWaveLength,_OceanWaveSpeed);
                float3 inland=lerp(river,sheltered,lake);
                inland.yz+=(sheltered.x-river.x)*blendGradients.zw;
                float3 result=lerp(inland,sea,ocean);
                result.yz+=(sea.x-inland.x)*blendGradients.xy;
                return result;
            }
            Varyings Vert(Attributes i)
            {
                Varyings o=(Varyings)0;
                o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
                o.positionWS.y+=WaveField(i.uv,i.color,i.blendGradients).x*saturate(i.color.r);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.uv=i.uv;o.flow=i.flow;o.color=i.color;o.gradients=i.gradients;o.blendGradients=i.blendGradients;
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
            half4 Frag(Varyings i):SV_Target
            {
                float3 wave=WaveField(i.uv,i.color,i.blendGradients);
                float2 slope=i.gradients.xy+wave.yz*saturate(i.color.r)+wave.x*i.gradients.zw;
                float2 direction=i.flow*min(1,4*rsqrt(max(dot(i.flow,i.flow),.0001)));
                // Dual phase advection resets without a visible snap; distance is in world metres.
                float phase0=frac(WaterTime()*_FlowSpeed*.125),phase1=frac(phase0+.5),weight=1-abs(phase0*2-1);
                float2 drift=float2(.035,.021)*WaterTime()*_FlowSpeed;
                float2 p0=i.uv-direction*(8*phase0)-drift,p1=i.uv-direction*(8*phase1)-drift;
                // Mips and slope attenuation suppress distant grazing-angle sparkle.
                float distanceToCamera=distance(GetCameraPositionWS(),i.positionWS);
                float detailFade=1-smoothstep(12,75,distanceToCamera);
                // Sea ripples cover a larger area than the river, with a coherent wind drift.
                float ocean=saturate(i.color.b),river=(1-ocean)*(1-saturate(i.color.a));
                float2 oceanDrift=float2(.23,.11)*WaterTime()*_OceanWaveSpeed;
                float2 q0=lerp(p0,(i.uv-oceanDrift)*.42,ocean);
                float2 q1=lerp(p1,(i.uv-oceanDrift-float2(1.7,2.3))*.42,ocean);
                float2 ripple=lerp(Ripple(q1,detailFade),Ripple(q0,detailFade),weight);
                slope+=ripple*_NormalStrength*lerp(.18,1,detailFade)*lerp(.18,1,saturate(i.color.r));
                float flowLength=length(direction);
                float2 along=direction/max(flowLength,.001),across=float2(along.y,-along.x);
                float2 current0=float2(dot(p0,across)/8,dot(p0,along)/15);
                float2 current1=float2(dot(p1,across)/8,dot(p1,along)/15);
                half3 current=lerp(SAMPLE_TEXTURE2D(_RiverMotionMap,sampler_RiverMotionMap,current1).rgb,
                    SAMPLE_TEXTURE2D(_RiverMotionMap,sampler_RiverMotionMap,current0).rgb,weight);
                float currentMask=river*saturate(flowLength)*saturate(i.color.r);
                slope+=across*(current.b-.5)*.045*currentMask*lerp(.2,1,detailFade);
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
                half2 grain=lerp(SAMPLE_TEXTURE2D(_FoamMap,sampler_FoamMap,p1/_FoamTileSize).rg,
                    SAMPLE_TEXTURE2D(_FoamMap,sampler_FoamMap,p0/_FoamTileSize).rg,weight);
                // Foam occupies irregular thin patches; a uniform depth band reads as paint.
                float shoreWidth=_FoamWidth*lerp(.3,1,grain.g);
                float shore=_UseDepth>.5?1-smoothstep(0,shoreWidth,shoreDepth):0;
                float breakup=smoothstep(_FoamCutoff-.08,_FoamCutoff+.18,grain.r)*smoothstep(.22,.68,grain.g);
                float crest=smoothstep(.55,.9,wave.x/max(_OceanWaveHeight,.001))*i.color.b*_CrestFoamStrength;
                half3 seaPattern=SAMPLE_TEXTURE2D(_OceanMotionMap,sampler_OceanMotionMap,
                    (i.uv-oceanDrift)/19).rgb;
                float seaCrest=smoothstep(.15,.8,wave.x/max(_OceanWaveHeight,.001));
                float seaFoam=seaPattern.r*seaCrest*_OceanSurfaceStrength*ocean;
                float currentFoam=smoothstep(.12,.72,current.r)*lerp(.5,1,current.g)*currentMask*_RiverCurrentStrength;
                float foam=saturate(saturate((shore+i.color.g)*_FoamStrength+crest)*breakup+currentFoam+seaFoam);
                float absorption=1-exp2(-depth/max(_DepthColorDistance,.2)*1.8);
                SurfaceData surface=(SurfaceData)0;
                half3 shallow=lerp(_ShallowColor.rgb,_OceanShallowColor.rgb,i.color.b);
                half3 deep=lerp(_BaseColor.rgb,_OceanBaseColor.rgb,i.color.b);
                surface.albedo=lerp(lerp(shallow,deep,absorption),half3(.72,.78,.75),foam);
                float surfaceSmoothness=lerp(_Smoothness,_OceanSmoothness,i.color.b)-.045*(1-detailFade);
                surface.smoothness=lerp(surfaceSmoothness+.025*(grain.g-.5),.38,foam);
                // Water's normal-incidence dielectric reflectance is about two percent.
                surface.specular=lerp(half3(.02,.02,.02),half3(.04,.04,.04),foam);
                surface.normalTS=half3(0,0,1);surface.occlusion=1;
                float fresnel=pow(1-saturate(dot(n,view)),5);
                // Ordinary deep water becomes opaque; an explicit lower DeepOpacity
                // remains available for artistic transparency profiles.
                float alpha=saturate(lerp(_ShallowOpacity,_DeepOpacity,absorption)+fresnel*.14+foam*.3);
                surface.alpha=_UseDepth>.5?alpha*smoothstep(0,_ShoreFadeDepth,shoreDepth):_DeepOpacity;
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

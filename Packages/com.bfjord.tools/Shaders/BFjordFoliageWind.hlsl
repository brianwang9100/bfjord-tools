// Original Bwork deformation/normal field retained in all passes.
#ifndef BFJORD_FOLIAGE_WIND_INCLUDED
#define BFJORD_FOLIAGE_WIND_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST,_BaseColor;
float _Cutoff,_AlphaClip,_BumpScale,_Smoothness,_Metallic,_WindRootY,_WindHeight,_WindAmplitude,_WindSpeed,_Transmission;
CBUFFER_END
TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
TEXTURE2D(_MetallicGlossMap);SAMPLER(sampler_MetallicGlossMap);
float4 _BFjordWindPreview; // x enables a bounded Editor capture; y is seconds.
// Editor/demo wind uses one common engine time in every rendering pass; no per-plant CPU update.
struct FoliageAttributes
{
    float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct FoliageVaryings
{
    float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;float fog:TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
// Root/height are shared between a model's LODs; phase depends only on its common object origin.
void FoliageDeform(FoliageAttributes input,out float3 positionWS,out float3 normalWS,out float4 tangentWS)
{
    positionWS=TransformObjectToWorld(input.positionOS.xyz);normalWS=TransformObjectToWorldNormal(input.normalOS);
    float3 tangent=TransformObjectToWorldDir(input.tangentOS.xyz);
    float3 origin=TransformObjectToWorld(float3(0,0,0));
    float phase=dot(origin.xz,float2(.137,.193));
    float time=lerp(_Time.y,_BFjordWindPreview.y,saturate(_BFjordWindPreview.x))*.16*_WindSpeed;
    float main=.72*sin(time*6+phase)+.28*sin(time*11+phase*1.7);
    float across=.18*sin(time*9+phase*.73);
    float2 direction=normalize(float2(.8,.6));
    float3 bend=float3(direction.x*main-direction.y*across,0,direction.y*main+direction.x*across)*_WindAmplitude;
    float h=saturate((input.positionOS.y-_WindRootY)/max(_WindHeight,.1));
    float weight=h*h*(3-2*h),slope=6*h*(1-h)/max(_WindHeight,.1);
    float3 gradient=GetWorldToObjectMatrix()[1].xyz*slope;
    positionWS+=bend*weight;
    // Inverse-transpose of I + bend * gradient^T; tangent uses its forward Jacobian.
    normalWS=normalize(normalWS-gradient*dot(bend,normalWS)/(1+dot(gradient,bend)));
    tangent=normalize(tangent+bend*dot(gradient,tangent));
    tangentWS=float4(normalize(tangent-normalWS*dot(tangent,normalWS)),input.tangentOS.w*GetOddNegativeScale());
}
FoliageVaryings FoliageVertex(FoliageAttributes input)
{
    FoliageVaryings o=(FoliageVaryings)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    FoliageDeform(input,o.positionWS,o.normalWS,o.tangentWS);o.positionCS=TransformWorldToHClip(o.positionWS);o.uv=TRANSFORM_TEX(input.uv,_BaseMap);o.fog=ComputeFogFactor(o.positionCS.z);return o;
}
half4 FoliageBase(FoliageVaryings input)
{
    half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv)*_BaseColor;
    clip(base.a-lerp(-1,_Cutoff,_AlphaClip));
    #if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
    #endif
    return base;
}
float3 FoliageNormal(FoliageVaryings input,bool front)
{
    float3 n=normalize(input.normalWS);
    #if defined(_NORMALMAP)
    float3 t=normalize(input.tangentWS.xyz),b=cross(n,t)*input.tangentWS.w;
    half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,input.uv),_BumpScale);
    n=normalize(t*normalTS.x+b*normalTS.y+n*normalTS.z);
    #endif
    return front?n:-n;
}
half4 FoliageForward(FoliageVaryings input,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half4 base=FoliageBase(input);SurfaceData surface=(SurfaceData)0;
    surface.albedo=base.rgb;surface.alpha=base.a;surface.metallic=_Metallic;surface.smoothness=_Smoothness;surface.occlusion=1;surface.normalTS=half3(0,0,1);
    #if defined(_METALLICSPECGLOSSMAP)
    half4 packed=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,input.uv);surface.metallic*=packed.r;surface.smoothness*=packed.a;
    #endif
    InputData data=(InputData)0;data.positionWS=input.positionWS;data.normalWS=FoliageNormal(input,IS_FRONT_VFACE(face,true,false));data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    data.shadowCoord=ComputeScreenPos(TransformWorldToHClip(input.positionWS));
    #else
    data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
    #endif
    data.bakedGI=SampleSH(data.normalWS);data.shadowMask=half4(1,1,1,1);data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);data.fogCoord=input.fog;
    half4 color=UniversalFragmentPBR(data,surface);
    Light sun=GetMainLight(data.shadowCoord);
    half back=saturate(dot(-data.normalWS,sun.direction))*_Transmission*_AlphaClip;
    color.rgb+=base.rgb*sun.color*back*sun.shadowAttenuation;
    color.rgb=MixFog(color.rgb,input.fog);return color;
}
float3 _LightDirection;
float3 _LightPosition;
FoliageVaryings FoliageShadowVertex(FoliageAttributes input)
{
    FoliageVaryings o=FoliageVertex(input);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 direction=normalize(_LightPosition-o.positionWS);
    #else
    float3 direction=_LightDirection;
    #endif
    o.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,direction)));return o;
}
half4 FoliageDepth(FoliageVaryings input):SV_Target
{UNITY_SETUP_INSTANCE_ID(input);FoliageBase(input);return input.positionCS.z;}
half4 FoliageDepthNormals(FoliageVaryings input,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);FoliageBase(input);float3 normal=FoliageNormal(input,IS_FRONT_VFACE(face,true,false));
    #if defined(_GBUFFER_NORMALS_OCT)
    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal)*.5+.5)),0);
    #else
    return half4(normal,0);
    #endif
}
#endif

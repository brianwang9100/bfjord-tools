#ifndef BWORK_SURFACE_BLEND_PASSES_INCLUDED
#define BWORK_SURFACE_BLEND_PASSES_INCLUDED
#include "SurfaceBlendInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct RoadAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 meters : TEXCOORD0;
    float2 road : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct RoadVaryings
{
    float4 positionCS : SV_POSITION;
    float4 coordinates : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    half4 fogAndVertexLight : TEXCOORD4;
    half3 vertexSH : TEXCOORD5;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD6;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
RoadVaryings RoadVertex(RoadAttributes input)
{
    RoadVaryings output = (RoadVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.coordinates = float4(input.meters, input.road);
    output.normalWS = normal.normalWS;
    output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    half fog = 0;
#if !defined(_FOG_FRAGMENT)
    fog = ComputeFogFactor(position.positionCS.z);
#endif
    output.fogAndVertexLight = half4(fog, VertexLighting(position.positionWS, normal.normalWS));
    // UV1 contains road metadata; this study uses realtime light plus SH probes, not lightmaps/APV.
    OUTPUT_SH(normal.normalWS, output.vertexSH);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(position);
#endif
    return output;
}
half3 RoadWorldNormal(RoadVaryings input, half3 normalTS)
{
    float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
    return NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,
        half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
}
void RoadFragment(RoadVaryings input, out half4 color : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint renderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    SurfaceData surface;
    RoadSurface(input.coordinates.xy, input.coordinates.zw, surface);
    InputData lighting = (InputData)0;
    lighting.positionWS = input.positionWS;
    lighting.normalWS = RoadWorldNormal(input, surface.normalTS);
    lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    lighting.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    lighting.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1), input.fogAndVertexLight.x);
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    lighting.vertexLighting = input.fogAndVertexLight.yzw;
#endif
    lighting.bakedGI = SAMPLE_GI(float2(0, 0), input.vertexSH, lighting.normalWS);
    lighting.shadowMask = SAMPLE_SHADOWMASK(float2(0, 0));
    lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    color = UniversalFragmentPBR(lighting, surface);
    color.rgb = MixFog(color.rgb, lighting.fogCoord);
    color.a = 1;
#ifdef _WRITE_RENDERING_LAYERS
    renderingLayers = EncodeMeshRenderingLayer();
#endif
}
void RoadDepthNormals(RoadVaryings input, out half4 normal : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint renderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    SurfaceData surface;
    RoadSurface(input.coordinates.xy, input.coordinates.zw, surface);
    half3 normalWS = RoadWorldNormal(input, surface.normalTS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = saturate(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5);
    normal = half4(PackFloat2To888(oct), 0);
#else
    normal = half4(normalWS, 0);
#endif
#ifdef _WRITE_SMOOTHNESS
    normal.a = surface.smoothness;
#endif
#ifdef _WRITE_RENDERING_LAYERS
    renderingLayers = EncodeMeshRenderingLayer();
#endif
}

struct RoadDepthVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
RoadDepthVaryings RoadDepthVertex(RoadAttributes input)
{
    RoadDepthVaryings output = (RoadDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}
float3 _LightDirection, _LightPosition;
RoadDepthVaryings RoadShadowVertex(RoadAttributes input)
{
    RoadDepthVaryings output = (RoadDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 direction = normalize(_LightPosition - positionWS);
#else
    float3 direction = _LightDirection;
#endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, direction)));
    return output;
}
half4 RoadDepthFragment(RoadDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    return input.positionCS.z;
}
#endif

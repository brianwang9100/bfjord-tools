#ifndef BWORK_SURFACE_BLEND_INPUT_INCLUDED
#define BWORK_SURFACE_BLEND_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "SurfaceBlendMask.hlsl"
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMapA_ST, _BaseMapB_ST;
    half4 _ColorA, _ColorB;
    half _NormalScaleA, _NormalScaleB, _SmoothnessA, _SmoothnessB;
    float _BlendMode, _WeightOverride;
CBUFFER_END
TEXTURE2D(_BaseMapA); SAMPLER(sampler_BaseMapA);
TEXTURE2D(_NormalMapA); SAMPLER(sampler_NormalMapA);
TEXTURE2D(_MaskMapA); SAMPLER(sampler_MaskMapA);
TEXTURE2D(_BaseMapB); SAMPLER(sampler_BaseMapB);
TEXTURE2D(_NormalMapB); SAMPLER(sampler_NormalMapB);
TEXTURE2D(_MaskMapB); SAMPLER(sampler_MaskMapB);
half3 RoadNormal(half4 packed, half scale)
{
#if BUMP_SCALE_NOT_SUPPORTED
    return UnpackNormal(packed);
#else
    return UnpackNormalScale(packed, scale);
#endif
}
void RoadSurface(float2 meters, float2 road, out SurfaceData s)
{
    float2 uvA = meters * _BaseMapA_ST.xy + _BaseMapA_ST.zw;
    float2 uvB = meters * _BaseMapB_ST.xy + _BaseMapB_ST.zw;
    // All samples execute before weight selection, retaining normal texture derivatives/mips.
    half3 colorA = SAMPLE_TEXTURE2D(_BaseMapA, sampler_BaseMapA, uvA).rgb * _ColorA.rgb;
    half3 colorB = SAMPLE_TEXTURE2D(_BaseMapB, sampler_BaseMapB, uvB).rgb * _ColorB.rgb;
    half4 maskA = SAMPLE_TEXTURE2D(_MaskMapA, sampler_MaskMapA, uvA);
    half4 maskB = SAMPLE_TEXTURE2D(_MaskMapB, sampler_MaskMapB, uvB);
    half3 normalA = RoadNormal(SAMPLE_TEXTURE2D(_NormalMapA, sampler_NormalMapA, uvA), _NormalScaleA);
    half3 normalB = RoadNormal(SAMPLE_TEXTURE2D(_NormalMapB, sampler_NormalMapB, uvB), _NormalScaleB);
    float footprint = max(length(ddx(meters)), length(ddy(meters)));
    float weight = _WeightOverride >= 0.0 ? saturate(_WeightOverride) :
        RoadBlendWeight(_BlendMode, meters.x, meters.y, road.x, road.y, footprint);
    s = (SurfaceData)0;
    s.alpha = 1.0; s.occlusion = 1.0;
    if (weight <= 0.0)
    { s.albedo = colorA; s.normalTS = normalA; s.metallic = maskA.r; s.smoothness = maskA.a * _SmoothnessA; }
    else if (weight >= 1.0)
    { s.albedo = colorB; s.normalTS = normalB; s.metallic = maskB.r; s.smoothness = maskB.a * _SmoothnessB; }
    else
    {
        s.albedo = lerp(colorA, colorB, weight);
        s.normalTS = normalize(lerp(normalize(normalA), normalize(normalB), weight));
        s.metallic = lerp(maskA.r, maskB.r, weight);
        s.smoothness = lerp(maskA.a * _SmoothnessA, maskB.a * _SmoothnessB, weight);
    }
}
#endif

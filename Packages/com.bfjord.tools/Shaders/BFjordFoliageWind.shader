// Original Bwork shader adapted for the independent BFjord Tools demo.
Shader "BFjord/Foliage Wind"
{
    Properties
    {
        [MainTexture] _BaseMap("Base / Cutout",2D)="white"{}
        [MainColor] _BaseColor("Color",Color)=(1,1,1,1)
        [ToggleUI] _AlphaClip("Alpha Clip",Float)=1
        _Cutoff("Cutoff",Range(0,1))=.45
        [Normal] _BumpMap("Normal",2D)="bump"{}
        _BumpScale("Normal Scale",Range(0,2))=1
        _MetallicGlossMap("Metallic / Smoothness",2D)="white"{}
        _Metallic("Metallic",Range(0,1))=0
        _Smoothness("Smoothness",Range(0,1))=.2
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=0
        _WindRootY("Shared LOD Root Y",Float)=0
        _WindHeight("Shared LOD Height",Float)=10
        _WindAmplitude("Wind Amplitude Meters",Range(0,.6))=.25
        _WindSpeed("Wind Speed",Range(0,3))=1
        _Transmission("Leaf Transmission",Range(0,.3))=.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" "DisableBatching"="True" }
        Cull [_Cull] ZWrite On
        HLSLINCLUDE
        #include "BFjordFoliageWind.hlsl"
        ENDHLSL
        Pass
        {
            Name "Forward" Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FoliageVertex
            #pragma fragment FoliageForward
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _METALLICSPECGLOSSMAP
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FoliageShadowVertex
            #pragma fragment FoliageDepth
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FoliageVertex
            #pragma fragment FoliageDepth
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FoliageVertex
            #pragma fragment FoliageDepthNormals
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma shader_feature_local _NORMALMAP
            ENDHLSL
        }
    }
    Fallback Off
}

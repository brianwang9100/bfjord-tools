Shader "Bwork/Sandbox/Wave Water"
{
    Properties
    {
        _BaseColor("Deep water", Color) = (.045,.135,.135,1)
        _ShallowColor("Shallow water", Color) = (.15,.26,.23,1)
        _WaveHeight("Maximum visual displacement metres", Range(0,1)) = .12
        _WaveLength("Broad wave length metres", Range(4,80)) = 14
        _WaveSpeed("Wave phase speed", Range(0,3)) = .8
        _FlowSpeed("River ripple advection", Range(0,4)) = .8
        _NormalStrength("Small ripple slope", Range(0,.3)) = .05
        _FoamStrength("Foam", Range(0,1)) = .2
        _Smoothness("Smoothness", Range(0,1)) = .88
        _UseDepth("Owner guarantees current depth texture", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForward"}
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ShallowColor;
            float _WaveHeight,_WaveLength,_WaveSpeed,_FlowSpeed,_NormalStrength,_FoamStrength,_Smoothness,_UseDepth;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float2 flow:TEXCOORD1;float4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 uv:TEXCOORD1;float2 flow:TEXCOORD2;float4 color:TEXCOORD3;float3 normalWS:TEXCOORD4;float fog:TEXCOORD5;};
            float WaveHeight(float2 p)
            {
                float k=6.283185307/max(_WaveLength,4),time=_Time.y*_WaveSpeed;
                // Weights sum to one, providing the mesh's exact displacement envelope.
                return saturate(_WaveHeight)*(.62*sin(dot(p,float2(.8,.6))*k-time)+.26*sin(dot(p,float2(-.42,.9075))*k*1.71-time*1.31)+.12*sin(dot(p,float2(.96,-.28))*k*2.63-time*1.62));
            }
            Varyings Vert(Attributes i)
            {
                Varyings o=(Varyings)0;
                o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
                o.positionWS.y+=WaveHeight(o.positionWS.xz)*saturate(i.color.r);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS);
                o.uv=i.uv;o.flow=i.flow;o.color=i.color;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            float FilteredCos(float phase) {return cos(phase)*(1-smoothstep(.4,2.2,fwidth(phase)));}
            float2 Ripple(float2 p)
            {
                float a=dot(p,float2(1.2,.45)),b=dot(p,float2(-.7,1.6));
                return float2(1.2,.45)*FilteredCos(a)*.55+float2(-.7,1.6)*FilteredCos(b)*.28;
            }
            half4 Frag(Varyings i):SV_Target
            {
                // Derivatives include bank-envelope changes and all displaced surface slopes.
                float3 n=normalize(cross(ddy(i.positionWS),ddx(i.positionWS)));
                if(dot(n,i.normalWS)<0)n=-n;
                float2 direction=i.flow*rsqrt(max(dot(i.flow,i.flow),.0001));
                float phase0=frac(_Time.y*_FlowSpeed*.25),phase1=frac(phase0+.5),weight=1-abs(phase0*2-1);
                float2 ripple=lerp(Ripple(i.uv-direction*(4*phase1)),Ripple(i.uv-direction*(4*phase0)),weight);
                float3 perturb=float3(ripple.x,0,ripple.y)*_NormalStrength;
                n=normalize(n+perturb-n*dot(n,perturb));
                float depth=3;
                if(_UseDepth>.5)
                {
                    float raw=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
                    depth=max(0,LinearEyeDepth(raw,_ZBufferParams)+TransformWorldToView(i.positionWS).z);
                }
                float grain=saturate(.5+dot(ripple,float2(.3,.25)));
                float shore=_UseDepth>.5?1-saturate(depth/.5):0;
                float foam=saturate((shore*.6+i.color.g)*_FoamStrength*grain);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=lerp(lerp(_ShallowColor.rgb,_BaseColor.rgb,saturate(depth/4)),half3(.58,.66,.63),foam);
                surface.smoothness=lerp(_Smoothness,.35,foam);surface.normalTS=half3(0,0,1);surface.occlusion=1;
                surface.alpha=_UseDepth>.5?lerp(.48,.95,saturate(depth/4)):.9;
                InputData lighting=(InputData)0;lighting.positionWS=i.positionWS;lighting.normalWS=n;
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                lighting.shadowCoord=TransformWorldToShadowCoord(i.positionWS);lighting.fogCoord=i.fog;
                lighting.bakedGI=SampleSH(n);lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                lighting.shadowMask=half4(1,1,1,1);
                half4 color=UniversalFragmentPBR(lighting,surface);color.rgb=MixFog(color.rgb,i.fog);color.a=surface.alpha;return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}

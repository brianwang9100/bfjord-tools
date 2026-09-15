Shader "Bwork/Sandbox/Waterfall"
{
    Properties
    {
        [NoScaleOffset] _WaterfallMap("Original streak / coverage / breakup", 2D) = "white" {}
        [NoScaleOffset] _FoamMap("Original foam filaments", 2D) = "gray" {}
        _WaterColor("Aerated water", Color) = (.13,.28,.28,1)
        _FoamColor("Whitewater", Color) = (.83,.89,.88,1)
        _FallSpeed("Flow metres per second", Range(.1,20)) = 7
        _Opacity("Sheet opacity", Range(0,1)) = .86
        _PlungeWaveHeight("Receiving water wave height", Float) = .45
        _PlungeWaveLength("Receiving water wavelength", Float) = 28
        _PlungeWaveSpeed("Receiving water phase speed", Float) = .65
        _Plunge("Plunge foam surface", Float) = 0
        [HideInInspector] _AnimationTime("Capture time; negative uses live time", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
        Pass
        {
            Name "WaterfallForward"
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
            #define _SPECULAR_SETUP 1
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_WaterfallMap); SAMPLER(sampler_WaterfallMap);
            TEXTURE2D(_FoamMap); SAMPLER(sampler_FoamMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _WaterColor,_FoamColor;
            float _FallSpeed,_Opacity,_Plunge,_AnimationTime;
            float _PlungeWaveHeight,_PlungeWaveLength,_PlungeWaveSpeed;
            CBUFFER_END
            // Same phase, directions and normalized weights as the connected ocean surface.
            float3 ReceivingWave(float2 p)
            {
                float time=(_AnimationTime>=0?_AnimationTime:_Time.y)*_PlungeWaveSpeed;
                float k=6.283185307/max(_PlungeWaveLength,4);
                float2 d0=float2(.8,.6),d1=normalize(float2(-.42,.9075)),d2=float2(.96,-.28);
                float3 phase=float3(dot(p,d0)*k-time,dot(p,d1)*k*1.71-time*1.31,dot(p,d2)*k*2.63-time*1.62);
                float3 c=cos(phase);
                return _PlungeWaveHeight*float3(dot(sin(phase),float3(.62,.26,.12)),
                    k*(.62*c.x*d0+.26*1.71*c.y*d1+.12*2.63*c.z*d2));
            }
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float2 metres:TEXCOORD1;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;float2 metres:TEXCOORD3;float fog:TEXCOORD4;};
            Varyings Vert(Attributes i)
            {
                Varyings o=(Varyings)0;
                o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
                float3 wave=ReceivingWave(o.positionWS.xz);
                o.positionWS.y+=wave.x*lerp(pow(i.uv.y,6),1,_Plunge);
                o.normalWS=_Plunge>.5?normalize(float3(-wave.y,1,-wave.z)):TransformObjectToWorldNormal(i.normalOS);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.uv=i.uv;o.metres=i.metres;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float time=_AnimationTime>=0?_AnimationTime:_Time.y;
                // V grows from lip to landing. Subtracting time makes visible features fall.
                float2 flowUV=float2(i.metres.x/5,(i.metres.y-time*_FallSpeed)/9);
                half3 sheet=SAMPLE_TEXTURE2D(_WaterfallMap,sampler_WaterfallMap,flowUV).rgb;
                half3 detail=SAMPLE_TEXTURE2D(_WaterfallMap,sampler_WaterfallMap,flowUV*float2(1.71,.83)+float2(.31,-time*.11)).rgb;
                float edgeNoise=(sheet.b-.5)*.045;
                float side=smoothstep(edgeNoise,.11+edgeNoise,i.uv.x)*smoothstep(-edgeNoise,.11-edgeNoise,1-i.uv.x);
                float ends=smoothstep(0,.065,i.uv.y)*(1-smoothstep(.88,1,i.uv.y));
                float acceleration=smoothstep(.03,.38,i.uv.y);
                float foam=saturate(.38+sheet.r*.5+detail.r*.3+acceleration*.15);
                float alpha=saturate(lerp(.32,1,sheet.g)*lerp(.8,1,detail.g)+acceleration*.1)*side*ends*_Opacity;
                float3 normal=normalize(i.normalWS);
                // A coherent, small lateral perturbation retains the shape of the falling sheet.
                normal=normalize(normal+float3((sheet.b-.5)*.17,(detail.b-.5)*.06,0)*(1-_Plunge));
                if(_Plunge>.5)
                {
                    float2 p=i.uv*2-1;float radius=length(p);
                    float2 outward=p/max(radius,.08);
                    float2 spread=i.metres/6-outward*time*.32;
                    half3 spray=SAMPLE_TEXTURE2D(_WaterfallMap,sampler_WaterfallMap,spread).rgb;
                    half2 bubbles=SAMPLE_TEXTURE2D(_FoamMap,sampler_FoamMap,i.metres/3-time*float2(.1,.13)).rg;
                    float edge=1-smoothstep(.25+spray.b*.13,.96,radius);
                    foam=saturate(.65+spray.r*.28+bubbles.r*.08);
                    alpha=edge*lerp(.25,.9,spray.g)*lerp(.75,1,bubbles.g)*_Opacity;
                    // Dense centre dissipates into broken, expanding fringes.
                    alpha=max(alpha,(1-smoothstep(.05,.35,radius))*.8*_Opacity);
                }
                float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                normal=dot(normal,view)<0?-normal:normal;
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=lerp(_WaterColor.rgb,_FoamColor.rgb,foam);
                surface.specular=half3(.025,.025,.025);surface.smoothness=lerp(.82,.36,foam);
                surface.normalTS=half3(0,0,1);surface.occlusion=1;surface.alpha=alpha;
                InputData lighting=(InputData)0;lighting.positionWS=i.positionWS;lighting.normalWS=normal;
                lighting.viewDirectionWS=view;lighting.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                lighting.bakedGI=SampleSH(normal);lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                lighting.shadowMask=half4(1,1,1,1);lighting.fogCoord=i.fog;
                half4 result=UniversalFragmentPBR(lighting,surface);
                result.rgb=MixFog(result.rgb,i.fog);result.a=alpha;return result;
            }
            ENDHLSL
        }
    }
    Fallback Off
}

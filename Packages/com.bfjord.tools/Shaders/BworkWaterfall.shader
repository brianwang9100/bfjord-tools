Shader "Bwork/Sandbox/Waterfall"
{
    Properties
    {
        [NoScaleOffset] _WaterfallMap("Original streak / coverage / breakup", 2D) = "white" {}
        [NoScaleOffset] _FoamMap("Original foam filaments", 2D) = "gray" {}
        _WaterColor("Clear falling water", Color) = (.065,.17,.16,1)
        _FoamColor("Whitewater", Color) = (.86,.91,.90,1)
        _FallSpeed("Flow metres per second", Range(.1,20)) = 7
        _Opacity("Sheet opacity", Range(0,1)) = .86
        _PlungeWaveHeight("Receiving water wave height", Float) = .45
        _PlungeWaveLength("Receiving water wavelength", Float) = 28
        _PlungeWaveSpeed("Receiving water phase speed", Float) = .65
        _Plunge("Plunge foam surface", Float) = 0
        _UseDepth("Owner guarantees current depth texture", Float) = 0
        [HideInInspector] _WaterAxes("Across XZ / downstream XZ", Vector) = (1,0,0,1)
        [HideInInspector] _PlungeDimensions("Receiving width / length metres", Vector) = (16,23,0,0)
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_WaterfallMap); SAMPLER(sampler_WaterfallMap);
            TEXTURE2D(_FoamMap); SAMPLER(sampler_FoamMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _WaterColor,_FoamColor;
            float _FallSpeed,_Opacity,_Plunge,_AnimationTime,_UseDepth;
            float4 _WaterAxes,_PlungeDimensions;
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
                float edgeNoise=(sheet.b-.5)*.065;
                float side=smoothstep(edgeNoise,.10+edgeNoise,i.uv.x)*smoothstep(-edgeNoise,.10-edgeNoise,1-i.uv.x);
                float ends=smoothstep(0,.055,i.uv.y)*(1-smoothstep(.94,1,i.uv.y));
                float acceleration=smoothstep(.08,.65,i.uv.y);
                // Broad clear lanes persist across the drop, with finer aeration moving through them.
                // Layered thresholds preserve transparent gaps instead of whitening the entire sheet.
                float lane=.5+.5*sin(i.uv.x*24+sin(i.uv.x*13+1.7)*1.8);
                float streak=smoothstep(.22,.78,sheet.r*.68+detail.r*.32);
                float foam=saturate(streak*lerp(.50,1,acceleration)+lane*.09);
                float alpha=lerp(.15,.92,saturate(sheet.g*.45+streak*.75))*side*ends*_Opacity;
                float3 normal=normalize(i.normalWS);
                float2 perturbation=_WaterAxes.xy*(sheet.b-.5)*.20+_WaterAxes.zw*(detail.b-.5)*.05;
                normal=normalize(normal+float3(perturbation.x,0,perturbation.y)*(1-_Plunge));
                if(_Plunge>.5)
                {
                    // The actual toe is 13% of the authored length upstream of the mesh centre.
                    float2 metres=i.metres+float2(0,_PlungeDimensions.y*.13);
                    float2 p=metres/(max(_PlungeDimensions.xy,float2(1,1))*.5);
                    float radius=length(p);float2 outward=p/max(radius,.04);
                    float2 spread=metres/5.5-outward*time*.28;
                    half3 spray=SAMPLE_TEXTURE2D(_WaterfallMap,sampler_WaterfallMap,spread).rgb;
                    half2 bubbles=SAMPLE_TEXTURE2D(_FoamMap,sampler_FoamMap,
                        metres/2.4-outward*time*.16).rg;
                    // A compact impact core, downstream eddies and broken radial fronts.
                    float downstream=smoothstep(-.42,.55,p.y);
                    float edge=1-smoothstep(.30+spray.b*.16,.88+downstream*.18,radius);
                    float core=1-smoothstep(.10,.38,length(p*float2(1.2,1)));
                    float rings=pow(saturate(.5+.5*sin(radius*37-time*3.5+spray.b*4)),5);
                    float filaments=smoothstep(.28,.77,spray.r*.52+bubbles.r*.48);
                    foam=saturate(.65+filaments*.32);
                    alpha=edge*saturate(core*.75+filaments*.52+rings*.18*(1-core))*_Opacity;
                    // Only normals ripple outside the impact. The surface retains the exact receiving-wave height.
                    float2 radialSlope=outward*cos(radius*37-time*3.5)*edge*(1-core)*.055;
                    float2 ripple=_WaterAxes.xy*radialSlope.x+_WaterAxes.zw*radialSlope.y;
                    normal=normalize(normal+float3(ripple.x,0,ripple.y));
                }
                if(_UseDepth>.5)
                {
                    float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);
                    float raw=SampleSceneDepth(screenUV);
                    #if !UNITY_REVERSED_Z
                        raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                    #endif
                    float3 contactWS=ComputeWorldSpacePosition(screenUV,raw,UNITY_MATRIX_I_VP);
                    // Soft intersection with opaque ledges and wet stones; receiving transparent water
                    // is deliberately excluded from the depth sample and retains its authored wave seam.
                    float contact=distance(i.positionWS,contactWS);
                    alpha*=smoothstep(.015,lerp(.10,.22,_Plunge),contact);
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

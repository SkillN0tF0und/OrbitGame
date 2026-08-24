Shader "PlanetGeneration/OceanVisuals"
{
    Properties
    {
        [HideInInspector] _ShallowColor("Shallow Color", Color) = (0.1, 0.6, 0.8, 0.8)
        [HideInInspector] _DeepColor("Deep Color", Color) = (0.01, 0.1, 0.4, 0.95)
        [HideInInspector] _DepthMultiplier("Depth Multiplier", Float) = 0.5
        [HideInInspector] _AlphaMultiplier("Alpha Multiplier", Float) = 1.0
        
        [HideInInspector] _WaveNormalA("Wave Normal A", 2D) = "bump" {}
        [HideInInspector] _WaveNormalB("Wave Normal B", 2D) = "bump" {}
        [HideInInspector] _WaveStrength("Wave Strength", Float) = 0.15
        [HideInInspector] _WaveScale("Wave Scale", Float) = 0.05
        [HideInInspector] _WaveSpeed("Wave Speed", Float) = 0.5
        [HideInInspector] _Smoothness("Smoothness", Float) = 0.95
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5 

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma require depth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _ShallowColor;
            float4 _DeepColor;
            float _DepthMultiplier;
            float _AlphaMultiplier;

            TEXTURE2D(_WaveNormalA); SAMPLER(sampler_WaveNormalA);
            TEXTURE2D(_WaveNormalB); SAMPLER(sampler_WaveNormalB);
            
            float _WaveStrength;
            float _WaveScale;
            float _WaveSpeed;
            float _Smoothness;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : NORMAL;
                float4 screenPos : TEXCOORD1;
            };

            // Triplanar mapping for seamless spherical wave normals
            float3 triplanarNormal(float3 posWS, float3 sphereNormalWS, float scale, float2 offset, TEXTURE2D_PARAM(tex, samp))
            {
                float3 blendWeight = abs(sphereNormalWS);
                blendWeight /= dot(blendWeight, 1.0);
                
                float2 uvX = posWS.zy * scale + offset;
                float2 uvY = posWS.xz * scale + offset;
                float2 uvZ = posWS.xy * scale + offset;
                
                // Sample and unpack normal maps
                float3 tx = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uvX));
                float3 ty = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uvY));
                float3 tz = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uvZ));
                
                // Blend them together based on the sphere's facing direction
                float3 localNormal = tx * blendWeight.x + ty * blendWeight.y + tz * blendWeight.z;
                
                // Align the resulting tangent normal to the sphere's surface
                float3 tangent = cross(sphereNormalWS, float3(0, 1, 0));
                if (length(tangent) < 0.001) tangent = cross(sphereNormalWS, float3(1, 0, 0));
                tangent = normalize(tangent);
                float3 bitangent = normalize(cross(sphereNormalWS, tangent));
                
                return normalize(tangent * localNormal.x + bitangent * localNormal.y + sphereNormalWS * localNormal.z);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 baseNormalWS = normalize(IN.normalWS);

                // --- 1. DEPTH CALCULATION (Shallow vs Deep) ---
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                
                // Convert depth values to physical distances
                float sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);
                float thisZ = LinearEyeDepth(IN.positionHCS.z, _ZBufferParams);
                
                // Calculate physical water depth
                float depthDifference = max(0.0, sceneZ - thisZ);
                
                // Exponential falloff for color and transparency
                float colorT = 1.0 - exp(-depthDifference * _DepthMultiplier);
                float alphaT = 1.0 - exp(-depthDifference * _AlphaMultiplier);
                
                float4 waterColor = lerp(_ShallowColor, _DeepColor, colorT);
                waterColor.a *= alphaT;

                // --- 2. WAVE NORMALS ---
                float time = _Time.y * _WaveSpeed;
                float2 offsetA = float2(time, time * 0.8);
                float2 offsetB = float2(time * -0.8, time * -0.3);
                
                float3 waveA = triplanarNormal(IN.positionWS, baseNormalWS, _WaveScale, offsetA, TEXTURE2D_ARGS(_WaveNormalA, sampler_WaveNormalA));
                float3 waveB = triplanarNormal(IN.positionWS, baseNormalWS, _WaveScale, offsetB, TEXTURE2D_ARGS(_WaveNormalB, sampler_WaveNormalB));
                
                float3 finalNormalWS = normalize(lerp(baseNormalWS, normalize(waveA + waveB), _WaveStrength));

                // --- 3. URP PBR LIGHTING ---
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = waterColor.rgb;
                surfaceData.metallic = 0.0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = float3(0, 0, 1); 
                surfaceData.emission = float3(0, 0, 0);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = waterColor.a;

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = finalNormalWS; 
                inputData.viewDirectionWS = normalize(GetCameraPositionWS() - IN.positionWS);
                
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATION)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                
                inputData.bakedGI = SampleSH(finalNormalWS);
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.shadowMask = float4(1, 1, 1, 1);

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
}
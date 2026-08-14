Shader "PlanetGeneration/PlanetVisuals"
{
    Properties
    {
        _ColorBlendSharpness("Color Blend Sharpness", Float) = 5.0
        _GlobalSteepnessThreshold("Global Steepness Threshold", Float) = 0.5
        _AmbientStrength("Ambient Light Multiplier", Range(0.0, 2.0)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue"="Geometry"}

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5 

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "FastNoiseLite.hlsl"

            float _ColorBlendSharpness;
            float _GlobalSteepnessThreshold;
            float _AmbientStrength;

            // Exactly 128 Bytes
            struct SurfaceBiomeData
            {
                float startThreshold;
                float baseHeightOffset;
                float amplitude;
                float frequency;
                
                int fractalType;
                int octaves;
                float lacunarity;
                float gain;
                
                float4 groundColor;
                float4 cliffColor;
                float4 noiseColor;
                
                int visualNoiseType;
                float visualFrequency;
                float noiseThreshold;
                float stretchX;
                
                float stretchY;
                float stretchZ;
                float metallic;
                float smoothness;

                float bumpStrength;
                float pad1;
                float pad2;
                float pad3;
            };

            StructuredBuffer<SurfaceBiomeData> _BiomeSurfaceBuffer;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL; 
                float3 blendData : TEXCOORD0; 
                float3 weights : TEXCOORD1;   
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1; 
                float3 normalWS : NORMAL; 
                nointerpolation uint3 biomeIndices : TEXCOORD2; 
                float3 biomeWeights : TEXCOORD3;
            };

            struct BiomePayload
            {
                float3 color;
                float3 normalOffset;
                float metallic;
                float smoothness;
            };

            BiomePayload GetBiomeSurfaceData(SurfaceBiomeData biome, float3 positionOS, float3 positionWS, float3 normalWS, float steepness)
            {
                BiomePayload payload = (BiomePayload)0;

                // 1. Cliff Check
                float isCliff = step(_GlobalSteepnessThreshold, steepness);
                if (isCliff >= 1.0)
                {
                    payload.color = biome.cliffColor.rgb;
                    payload.metallic = biome.metallic;
                    payload.smoothness = biome.smoothness * 0.5;
                    payload.normalOffset = float3(0,0,0);
                    return payload;
                }

                // 2. Safe Vector Stretching
                float3 stretchDir = float3(biome.stretchX, biome.stretchY, biome.stretchZ);
                float stretchMag = length(stretchDir);
                float3 samplePos = positionOS;

                if (stretchMag > 0.01)
                {
                    float3 dirNorm = stretchDir / stretchMag;
                    float3 parallelVec = dot(positionOS, dirNorm) * dirNorm;
                    float3 orthoVec = positionOS - parallelVec;
                    samplePos = orthoVec + (parallelVec / max(1.0, stretchMag));
                }

                // 3. Fast Noise Evaluation
                fnl_state state = fnlCreateState(1337);
                state.noise_type = biome.visualNoiseType;
                state.fractal_type = FNL_FRACTAL_NONE; 
                state.frequency = biome.visualFrequency;

                float noise = fnlGetNoise3D(state, samplePos.x, samplePos.y, samplePos.z);
                
                // 4. Safe Gradient Normal Calculation
                float3 dpdx = ddx(positionWS);
                float3 dpdy = ddy(positionWS);
                
                float dhdx = ddx(noise) * biome.bumpStrength;
                float dhdy = ddy(noise) * biome.bumpStrength;
                
                float3 tangent = cross(dpdy, normalWS);
                float3 bitangent = cross(normalWS, dpdx);

                // Prevent zero-length cross-products from generating NaN
                if (length(tangent) > 0.001 && length(bitangent) > 0.001)
                {
                    payload.normalOffset = (normalize(tangent) * dhdx + normalize(bitangent) * dhdy);
                }

                // 5. Output Color & PBR
                float mask = step(biome.noiseThreshold, noise); 
                payload.color = lerp(biome.groundColor.rgb, biome.noiseColor.rgb, mask);
                payload.metallic = biome.metallic;
                payload.smoothness = biome.smoothness;

                return payload; 
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz); 
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.biomeIndices = uint((uint)IN.blendData.x, (uint)IN.blendData.y, (uint)IN.blendData.z);
                OUT.biomeWeights = IN.weights;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 tweakedWeights = pow(IN.biomeWeights, _ColorBlendSharpness);
                float weightSum = tweakedWeights.x + tweakedWeights.y + tweakedWeights.z;
                tweakedWeights = weightSum > 0.0001 ? (tweakedWeights / weightSum) : float3(1,0,0);

                float3 normalWS = normalize(IN.normalWS);
                float3 upVectorWS = normalize(IN.positionWS - TransformObjectToWorld(float3(0,0,0)));
                float steepness = 1.0 - saturate(dot(normalWS, upVectorWS));

                float3 finalColor = float3(0,0,0);
                float3 finalNormalOffset = float3(0,0,0);
                float finalMetallic = 0.0;
                float finalSmoothness = 0.0;

                if (tweakedWeights.x > 0.0)
                {
                    SurfaceBiomeData biomeA = _BiomeSurfaceBuffer[IN.biomeIndices.x];
                    BiomePayload pA = GetBiomeSurfaceData(biomeA, IN.positionOS, IN.positionWS, normalWS, steepness);
                    finalColor += pA.color * tweakedWeights.x;
                    finalNormalOffset += pA.normalOffset * tweakedWeights.x;
                    finalMetallic += pA.metallic * tweakedWeights.x;
                    finalSmoothness += pA.smoothness * tweakedWeights.x;
                }
                if (tweakedWeights.y > 0.0 && IN.biomeIndices.y != IN.biomeIndices.x)
                {
                    SurfaceBiomeData biomeB = _BiomeSurfaceBuffer[IN.biomeIndices.y];
                    BiomePayload pB = GetBiomeSurfaceData(biomeB, IN.positionOS, IN.positionWS, normalWS, steepness);
                    finalColor += pB.color * tweakedWeights.y;
                    finalNormalOffset += pB.normalOffset * tweakedWeights.y;
                    finalMetallic += pB.metallic * tweakedWeights.y;
                    finalSmoothness += pB.smoothness * tweakedWeights.y;
                }
                if (tweakedWeights.z > 0.0 && IN.biomeIndices.z != IN.biomeIndices.x && IN.biomeIndices.z != IN.biomeIndices.y)
                {
                    SurfaceBiomeData biomeC = _BiomeSurfaceBuffer[IN.biomeIndices.z];
                    BiomePayload pC = GetBiomeSurfaceData(biomeC, IN.positionOS, IN.positionWS, normalWS, steepness);
                    finalColor += pC.color * tweakedWeights.z;
                    finalNormalOffset += pC.normalOffset * tweakedWeights.z;
                    finalMetallic += pC.metallic * tweakedWeights.z;
                    finalSmoothness += pC.smoothness * tweakedWeights.z;
                }

                // Apply safe bump offset
                normalWS = normalize(normalWS - finalNormalOffset);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalColor;
                surfaceData.metallic = saturate(finalMetallic);
                surfaceData.smoothness = saturate(finalSmoothness);
                surfaceData.normalTS = float3(0, 0, 1); 
                surfaceData.emission = float3(0, 0, 0);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS; 
                inputData.viewDirectionWS = normalize(GetCameraPositionWS() - IN.positionWS);
                
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATION)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                
                inputData.bakedGI = SampleSH(normalWS) * _AmbientStrength;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                inputData.shadowMask = float4(1, 1, 1, 1);

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                
                OUT.positionHCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return 0; 
            }
            ENDHLSL
        }
    }
}
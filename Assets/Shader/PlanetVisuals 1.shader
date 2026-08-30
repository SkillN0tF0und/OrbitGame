Shader "PlanetGeneration/PlanetVisuals"
{
    Properties
    {
        //blend sharpness between biomes
        _ColorBlendSharpness("Color Blend Sharpness", Float) = 5.0
        //at what steepness is the cliff color applied
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
            
            ZWrite On
            ZTest LEqual
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            //to work with buffers
            #pragma target 4.5 
            
            //needed since MAINLIGHT would need a directional light, but this uses a pointlight as the sun. 
            #pragma multi_compile _ADDITIONAL_LIGHTS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "FastNoiseLite.hlsl"

            float _ColorBlendSharpness;
            float _GlobalSteepnessThreshold;
            float _AmbientStrength;

            // Variables injected by PlanetFace.cs 
            
            //amount of points scattered on the surface to generate voronoi cells
            uint _BiomePointCount;
            // planet radius
            float _Radius;
            // physical biome blend distance, this is also applied to the geometry
            float _BiomeBlendDistance;
            // warp is used to distort voronoi cell edges
            int _EnableWarp;
            float _WarpAmplitude;
            float _WarpFrequency;
            // seed for repeatable results
            int _MacroSeed;

            // 128 Bytes Strict Alignment
            // This is set by a biomeData ScriptableObject. each biome has its own values here to generate more interesting terrain
            struct SurfaceBiomeData
            {
                // single offset value from the planet surface
                float baseHeightOffset;
                //noise variables for the terrain
                float amplitude;
                float frequency;
                // different types for different visuals
                int fractalType;
                
                // overlay different npise frequencies
                int octaves;
                // frequency multiplier
                float lacunarity;
                // difference between each noise layer
                float gain;
                float pad0; // Ensures the color block starts on a 16-byte boundary
                
                float4 groundColor;
                float4 cliffColor;
                // multiple colors for each biome as stylistic choice
                float4 noiseColor;
                
                // stylistic controll for color noise
                int visualNoiseType;
                float visualFrequency;
                float noiseThreshold;
                
                //stretch color noise(right now just stretches the 3D noise in 3d space)
                float stretchX;
                float stretchY;
                float stretchZ;
                
                float metallic;
                float smoothness;
                // normal variation (did not work how i wanted)
                float bumpStrength;
                
                float pad1;
                float pad2;
                float pad3;
            };
            
            // single seed point of each voronoi cell for the biomes
            struct GPUBiomePoint
            {
                float3 position;
                int biomeIndex;
            };

            //
            StructuredBuffer<SurfaceBiomeData> _BiomeSurfaceBuffer;
            StructuredBuffer<GPUBiomePoint> _BiomePoints;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL; 
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1; 
                float3 normalWS : NORMAL; 
            };
            
            //used in fragment shader
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

                // cliffs first since they would override everything else. - based on vertex normals
                float isCliff = step(_GlobalSteepnessThreshold, steepness);
                if (isCliff >= 1.0)
                {
                    payload.color = biome.cliffColor.rgb;
                    payload.metallic = biome.metallic;
                    payload.smoothness = biome.smoothness * 0.5; 
                    payload.normalOffset = float3(0,0,0);
                    return payload;
                }

                // Vector stretching for visual noise
                float3 stretchDir = float3(biome.stretchX, biome.stretchY, biome.stretchZ);
                float stretchMag = length(stretchDir);
                float3 samplePos = positionOS;
                
                //offset sample position along the stretch axis
                if (stretchMag > 0.001)
                {
                    float3 dirNorm = stretchDir / stretchMag;
                    float3 parallelVec = dot(positionOS, dirNorm) * dirNorm;
                    float3 orthoVec = positionOS - parallelVec;
                    samplePos = orthoVec + (parallelVec / max(1.0, stretchMag));
                }
                
                //sample noise
                fnl_state state = fnlCreateState(_MacroSeed);
                state.noise_type = biome.visualNoiseType;
                state.fractal_type = FNL_FRACTAL_NONE; 
                state.frequency = biome.visualFrequency;

                float noise = fnlGetNoise3D(state, samplePos.x, samplePos.y, samplePos.z);
                
                //bump
                payload.normalOffset = (0,0,0);
                
                // apply visual noise between the two biome colors
                float mask = step(biome.noiseThreshold, noise); 
                payload.color = lerp(biome.groundColor.rgb, biome.noiseColor.rgb, mask);
                payload.metallic = biome.metallic;
                payload.smoothness = biome.smoothness;

                return payload; 
            }


            // PER-PIXEL VORONOI EVALUATION

            void GetPixelBiomeWeights(float3 positionOS, out uint3 outIndices, out float3 outWeights)
            {
                float3 unitVector = normalize(positionOS);
                float3 samplePos = unitVector * _Radius;
                float3 warpedDir = unitVector;
                
                // Warp to distort voronoi cell edges (must match compute shader)
                if (_EnableWarp == 1)
                {
                    fnl_state warpState = fnlCreateState(_MacroSeed + 1);
                    warpState.domain_warp_type = FNL_DOMAIN_WARP_OPENSIMPLEX2;
                    warpState.domain_warp_amp = _WarpAmplitude;
                    warpState.frequency = _WarpFrequency;
                    
                    float wx = samplePos.x;
                    float wy = samplePos.y;
                    float wz = samplePos.z;
                    fnlDomainWarp3D(warpState, wx, wy, wz);
                    warpedDir = normalize(float3(wx, wy, wz));
                }

                // find 3 closest voronoi seeds
                float d1 = 3.402823466e+38F;
                float d2 = 3.402823466e+38F;
                float d3 = 3.402823466e+38F;
                uint idx1 = 0, idx2 = 0, idx3 = 0;

                for (uint i = 0; i < _BiomePointCount; i++)
                {
                    float dist = length(warpedDir - _BiomePoints[i].position); 
                    if (dist < d1)
                    {
                        d3 = d2; idx3 = idx2;
                        d2 = d1; idx2 = idx1;
                        d1 = dist; idx1 = i;
                    }
                    else if (dist < d2)
                    {
                        d3 = d2; idx3 = idx2;
                        d2 = dist; idx2 = i;
                    }
                    else if (dist < d3)
                    {
                        d3 = dist; idx3 = i;
                    }
                }
                
                // final indices
                outIndices = uint3(_BiomePoints[idx1].biomeIndex, _BiomePoints[idx2].biomeIndex, _BiomePoints[idx3].biomeIndex);

                // blend biome weights and apply blenddistance
                // by subtracting the distance of the closest biome point d1 and the second closest d2 we get the distance to the border.
                // then we devide by the blenddistance to get a percentage in relation to the blenddistance which is clampled down if it is above 1.0
                float t2 = clamp((d2 - d1) / _BiomeBlendDistance, 0.0, 1.0);
                float w2 = smoothstep(1.0, 0.0, t2);

                float t3 = clamp((d3 - d1) / _BiomeBlendDistance, 0.0, 1.0);
                float w3 = smoothstep(1.0, 0.0, t3);

                // calculate and normalize final biome weights
                float w1 = 1.0;
                float weightSum = w1 + w2 + w3;
                outWeights = float3(w1 / weightSum, w2 / weightSum, w3 / weightSum);

                // Merge weights if cells share the same parent biome
                if (outIndices.y == outIndices.x) { outWeights.x += outWeights.y; outWeights.y = 0.0; }
                if (outIndices.z == outIndices.x) { outWeights.x += outWeights.z; outWeights.z = 0.0; }
                else if (outIndices.z == outIndices.y) { outWeights.y += outWeights.z; outWeights.z = 0.0; }
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // needed for the noise noise be consistent when the planet moves
                OUT.positionOS = IN.positionOS.xyz;
                // needed for light calculation
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz); 
                // screen s
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                // needed for lighting and steepness
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // STEEPNESS
                float3 normalWS = normalize(IN.normalWS);
                // get direction from center of the planet
                float3 upVectorWS = normalize(IN.positionWS - TransformObjectToWorld(float3(0,0,0)));
                // 1 is flat| 0 is 90 degrees between up and 
                float steepness = 1.0 - dot(normalWS, upVectorWS);

                // Get Biome Weights for each fragment
                uint3 bIndices;
                float3 bWeights;
                GetPixelBiomeWeights(IN.positionOS, bIndices, bWeights);

                // Apply sharpness curve with sharpness as exponent
                bWeights = pow(bWeights, _ColorBlendSharpness);
                bWeights /= (bWeights.x + bWeights.y + bWeights.z);
                
                BiomePayload finalPayload = (BiomePayload)0;

                for (int i = 0; i < 3; i++)
                {
                    if (bWeights[i] > 0.0)
                    {
                        //fetch data for the biome
                        SurfaceBiomeData bData = _BiomeSurfaceBuffer[bIndices[i]];
                        
                        //calculate fragment value per individual biome
                        BiomePayload p = GetBiomeSurfaceData(bData, IN.positionOS, IN.positionWS, normalWS, steepness);
                        
                        //apply it according to the calculated biome weights
                        finalPayload.color += p.color * bWeights[i];
                        finalPayload.normalOffset += p.normalOffset * bWeights[i];
                        finalPayload.metallic += p.metallic * bWeights[i];
                        finalPayload.smoothness += p.smoothness * bWeights[i];
                    }
                }

                // PBR
                
                //apply bump(not implemented currently)
                normalWS = normalize(normalWS - finalPayload.normalOffset);
                
                //apply pbr properties 
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalPayload.color;
                surfaceData.metallic = finalPayload.metallic;
                surfaceData.smoothness = finalPayload.smoothness;
                surfaceData.normalTS = float3(0, 0, 1); 
                surfaceData.emission = float3(0, 0, 0);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;
                
                //get necessary data
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS; 
                inputData.viewDirectionWS = normalize(GetCameraPositionWS() - IN.positionWS);
                
                inputData.shadowCoord = float4(0, 0, 0, 0);
                
                inputData.bakedGI = SampleSH(normalWS) * _AmbientStrength;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                inputData.shadowMask = float4(1, 1, 1, 1);
                
                
                //hand off for final rendering
                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
}


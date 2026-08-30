Shader "PlanetGeneration/OceanVisuals"
{
    Properties
    {
        // base color of the ocean with alpha for transparency
        [HideInInspector] _DeepColor("Deep Color", Color) = (0.01, 0.4, 0.8, 0.9)
        
        [HideInInspector] _WaveNormalA("Wave Normal A", 2D) = "bump" {}
        [HideInInspector] _WaveNormalB("Wave Normal B", 2D) = "bump" {}
        
        [HideInInspector] _WaveStrength("Wave Strength", Float) = 0.15
        [HideInInspector] _WaveScale("Wave Scale", Float) = 0.05
        [HideInInspector] _WaveSpeed("Wave Speed", Float) = 0.5
        [HideInInspector] _Smoothness("Smoothness", Float) = 0.95

        [HideInInspector] _AmbientStrength("Ambient Strength", Float) = 0.1
    }

    SubShader
    {
        // render as transparent so it draws after the opaque terrain
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // alpha blending
            Blend SrcAlpha OneMinusSrcAlpha
            

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma target 4.5 

            // needed since MAINLIGHT would need a directional light, but this uses a pointlight as the sun.
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _DeepColor;

            TEXTURE2D(_WaveNormalA); SAMPLER(sampler_WaveNormalA);
            TEXTURE2D(_WaveNormalB); SAMPLER(sampler_WaveNormalB);
            
            float _WaveStrength;
            float _WaveScale;
            float _WaveSpeed;
            float _Smoothness;
            float _AmbientStrength;

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
            };

            // map textures on sphere
            float3 triplanarNormal(float3 posWS, float3 sphereNormalWS, float scale, float2 offset, TEXTURE2D_PARAM(tex, samp))
            {
                // get blend weights based on the direction the sphere faces
                float3 blendWeight = abs(sphereNormalWS);
                blendWeight /= dot(blendWeight, 1.0);
                
                // calculate uvs for all 3 axes with time offset
                float2 uvX = posWS.zy * scale + offset;
                float2 uvY = posWS.xz * scale + offset;
                float2 uvZ = posWS.xy * scale + offset;
                
                // sample normal maps
                float3 tx = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uvX));
                float3 ty = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uvY));
                float3 tz = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uvZ));
                
                // blend samples
                float3 localNormal = tx * blendWeight.x + ty * blendWeight.y + tz * blendWeight.z;
                
                // align the flat normal map to sphere surface
                float3 tangent = cross(sphereNormalWS, float3(0, 1, 0));
                if (length(tangent) < 0.001) tangent = cross(sphereNormalWS, float3(1, 0, 0));
                tangent = normalize(tangent);
                float3 bitangent = normalize(cross(sphereNormalWS, tangent));
                
                // apply tangent math to the local normal
                return normalize(tangent * localNormal.x + bitangent * localNormal.y + sphereNormalWS * localNormal.z);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // needed for triplanar projection and lighting
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                // screen position
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                // needed for lighting and triplanar upvector
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 baseNormalWS = normalize(IN.normalWS);

                // WAVE NORMALS
                
                // animate uvs based on time and speed
                float time = _Time.y * _WaveSpeed;
                float2 offsetA = float2(time, time * 0.8);
                float2 offsetB = float2(time * -0.8, time * -0.3);
                
                // sample triplanar normals twice in opposing directions
                float3 waveA = triplanarNormal(IN.positionWS, baseNormalWS, _WaveScale, offsetA, TEXTURE2D_ARGS(_WaveNormalA, sampler_WaveNormalA));
                float3 waveB = triplanarNormal(IN.positionWS, baseNormalWS, _WaveScale, offsetB, TEXTURE2D_ARGS(_WaveNormalB, sampler_WaveNormalB));
                
                // combine the two wave layers and apply intensity
                float3 finalNormalWS = normalize(lerp(baseNormalWS, normalize(waveA + waveB), _WaveStrength));

                // PBR
                
                // apply pbr properties
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = _DeepColor.rgb;
                surfaceData.metallic = 0.0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = float3(0, 0, 1); 
                surfaceData.emission = float3(0, 0, 0);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = _DeepColor.a;

                // get necessary data
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = finalNormalWS; 
                inputData.viewDirectionWS = normalize(GetCameraPositionWS() - IN.positionWS);
                
                inputData.shadowCoord = float4(0, 0, 0, 0);
                
                // apply ambient strength
                inputData.bakedGI = SampleSH(finalNormalWS) * _AmbientStrength;
                
                inputData.normalizedScreenSpaceUV = float2(0, 0);
                inputData.shadowMask = float4(1, 1, 1, 1);

                // hand off 
                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
}
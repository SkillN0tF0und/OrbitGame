Shader "PlanetGeneration/LightDebug"
{
    Properties { }

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

            // CRITICAL: This is the exact keyword URP requires to pass Point Lights to the shader
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 1. Ask URP how many point/spot lights are assigned to this object
                uint lightCount = GetAdditionalLightsCount();
                
                if (lightCount > 0)
                {
                    // 2. Fetch the data for the first point light it finds
                    Light light = GetAdditionalLight(0, IN.positionWS);
                    
                    // 3. Check if the light actually has the range to reach this pixel
                    if (light.distanceAttenuation > 0.001)
                    {
                        // SUCCESS: Light is registered and touching the surface
                        return half4(0.0, 1.0, 0.0, 1.0); // GREEN
                    }
                    else
                    {
                        // FAIL: URP sees the light, but it is physically too far away or its Range is too small
                        return half4(1.0, 1.0, 0.0, 1.0); // YELLOW
                    }
                }

                // FAIL: URP refuses to send any point lights to this shader
                return half4(1.0, 0.0, 0.0, 1.0); // RED
            }
            ENDHLSL
        }
    }
}
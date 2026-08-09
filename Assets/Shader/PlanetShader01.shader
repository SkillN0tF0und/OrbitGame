Shader "Custom/Planet01"
{
    Properties
    {
        _DotColor ("Vertex Color", Color) = (1, 1, 1, 1)
        _VertexSize ("Vertex Size", Range(0, 0.5)) = 0.1
        _IceThreshold ("Ice Coverage", Range(0, 1)) = 0.5
        _IceSmoothness ("Ice Edge Softness", Range(0.01, 0.4)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma target 4.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2g
            {
                float4 pos : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };
            
            struct g2f { 
                float4 pos : SV_POSITION; 
                float3 localPos : TEXCOORD0; 
                float3 bary : TEXCOORD1; 
            };

            float4 _DotColor;
            float _VertexSize;
            float _IceThreshold;
            float _IceSmoothness;

            v2g vert (appdata v) {
                v2g o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g i[3], inout TriangleStream<g2f> triStream) {
                
                //
                float3 barys[3] = { float3(1,0,0), float3(0,1,0), float3(0,0,1) };
                for (int n = 0; n < 3; n++) {
                    g2f o;
                    o.pos = i[n].pos;
                    o.localPos = i[n].localPos;
                    o.bary = barys[n];
                    triStream.Append(o);
                }
            }

            fixed4 frag (g2f i) : SV_Target {
                
                //get height
                
                float verticality = abs(i.localPos.y);

                // 2. Create the Ice Mask
                // We use smoothstep to avoid the 'if' and get a clean edge
                float iceMask = smoothstep(_IceThreshold, _IceThreshold + _IceSmoothness, verticality);

                // 3. Define Colors
                float3 oceanColor = float3(0.0, 0.1, 0.5); // Dark Blue
                float3 iceColor = float3(1.0, 1.0, 1.0);   // White

                // 4. Blend based on the mask
                float3 finalColor = lerp(oceanColor, iceColor, iceMask);

                return float4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}
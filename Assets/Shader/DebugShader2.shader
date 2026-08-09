Shader "Custom/Debug2"
{
    Properties
    {
        _DotColor ("Vertex Color", Color) = (1, 1, 1, 1)
        _VertexSize ("Vertex Size", Range(0, 0.5)) = 0.1
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
            struct v2g { float4 pos : SV_POSITION; float3 localPos : TEXCOORD0; };
            struct g2f { 
                float4 pos : SV_POSITION; 
                float3 localPos : TEXCOORD0; 
                float3 bary : TEXCOORD1; 
            };

            float4 _DotColor;
            float _VertexSize;

            v2g vert (appdata v) {
                v2g o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g i[3], inout TriangleStream<g2f> triStream) {
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
                float3 baseColor = i.localPos + 0.5;

                // 1. Calculate how close we are to ANY vertex
                // We check how close each barycentric component is to 1.0
                float3 d = fwidth(i.bary);
                float3 corners = smoothstep(1.0 - d * _VertexSize * 50, 1.0, i.bary);
                
                // 2. If any component is near 1, this pixel is part of a "dot"
                float dotMask = max(max(corners.x, corners.y), corners.z);

                // 3. Blend the background color with the dot color
                float3 finalColor = lerp(baseColor, _DotColor.rgb, dotMask);

                return float4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}
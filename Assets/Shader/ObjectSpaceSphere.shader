Shader "Custom/DebugLocalPos"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };

            v2f vert (appdata v) {
                v2f o;
                // Convert vertex from object space to screen space
                o.pos = UnityObjectToClipPos(v.vertex);
                // Pass object space position
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                //adjust offset
                float3 color = i.localPos + 0.5;
                return float4(color, 1.0);
            }
            ENDCG
        }
    }
}
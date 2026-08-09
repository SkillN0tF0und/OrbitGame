Shader "PlanetGeneration/BiomeUnlit"
{
    Properties
    {
        // No texture properties needed, we are strictly using vertex colors
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR; // Reads the mesh.colors array
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR; // Passes color to the fragment stage
            };

            v2f vert (appdata v)
            {
                v2f o;
                // Convert the local 3D vertex position to 2D screen space
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // Pass the biome color exactly as is
                o.color = v.color;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Return the interpolated color for this pixel
                return i.color;
            }
            ENDCG
        }
    }
}
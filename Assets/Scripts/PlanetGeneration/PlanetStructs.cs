using System.Runtime.InteropServices;
using UnityEngine;

namespace PlanetGeneration
{
    public struct ChunkGeometry
    {
        //the three corners of each face of the base icosahedron
        public Vector3 V1, V2, V3;
        public ChunkGeometry(Vector3 v1, Vector3 v2, Vector3 v3) { V1 = v1; V2 = v2; V3 = v3; }
    }

    public struct MeshSettings
    {
        public int Resolution;
        public ShapeGenerator ShapeGenerator;
        public MeshSettings(int res, ShapeGenerator generator) { Resolution = res; ShapeGenerator = generator; }
    }

    // 128 BYTES. LayoutKind.Sequential to keep variables in exact order for use in HLSL shaders.
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct PlanetBiome
    {
        // terrain variables, used in shapeGenerator ComputeSahder - 32 Bytes
        public readonly float startThreshold; // not used in shader currently
        public readonly float baseHeightOffset;
        public readonly float amplitude;
        public readonly float frequency;
        
        public readonly int fractalType;
        public readonly int octaves;
        public readonly float lacunarity;
        public readonly float gain;

        // Colors - 48 Bytes
        public readonly Color groundColor;
        public readonly Color cliffColor;
        public readonly Color noiseColor;

        //Visual Noise Settings - 16 Bytes
        public readonly int visualNoiseType;
        public readonly float visualFrequency;
        public readonly float noiseThreshold;
        public readonly float stretchX;

        //PBR Data - 16 Bytes
        public readonly float stretchY;
        public readonly float stretchZ;
        public readonly float metallic;
        public readonly float smoothness;

        // Bump Map - 4 Bytes + 12 bytes padding to get to 128 bytes and prevent "GPU stride misalignment"
        public readonly float bumpStrength;
        public readonly float pad1;
        public readonly float pad2;
        public readonly float pad3;

        public static int Size => 128;

        public PlanetBiome(
            float startThreshold, float baseHeightOffset, float amplitude, float frequency,
            int fractalType, int octaves, float lacunarity, float gain,
            Color groundColor, Color cliffColor, Color noiseColor,
            int visualNoiseType, float visualFrequency, float noiseThreshold, float stretchX,
            float stretchY, float stretchZ, float metallic, float smoothness,
            float bumpStrength)
        {
            this.startThreshold = startThreshold;
            this.baseHeightOffset = baseHeightOffset;
            this.amplitude = amplitude;
            this.frequency = frequency;
            this.fractalType = fractalType;
            this.octaves = octaves;
            this.lacunarity = lacunarity;
            this.gain = gain;
            this.groundColor = groundColor;
            this.cliffColor = cliffColor;
            this.noiseColor = noiseColor;
            this.visualNoiseType = visualNoiseType;
            this.visualFrequency = visualFrequency;
            this.noiseThreshold = noiseThreshold;
            this.stretchX = stretchX;
            this.stretchY = stretchY;
            this.stretchZ = stretchZ;
            this.metallic = metallic;
            this.smoothness = smoothness;
            this.bumpStrength = bumpStrength;
            this.pad1 = 0f;
            this.pad2 = 0f;
            this.pad3 = 0f;
        }
    }

    
    // seed point on surface for biomes voronoi pattern
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GPUBiomePoint
    {
        public readonly Vector3 position;
        public readonly int biomeIndex;
        public static int Stride => 16;
        public GPUBiomePoint(Vector3 pos, int index) { position = pos; biomeIndex = index; }
    }
    
    
    //returned by the meshgenerator compute shader
    [StructLayout(LayoutKind.Sequential)]
    public struct GPUVertexData
    {
        public Vector3 position;
        public float positionPadding;
        public Vector3 biomeIndices;
        public float indicesPadding;
        public Vector3 biomeWeights;
        public float weightsPadding;
        public static int Stride => 48;
    }
}
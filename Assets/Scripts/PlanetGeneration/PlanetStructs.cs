using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PlanetGeneration
{
    public struct ChunkGeometry//three corners of triangle chunk
    {
        
        public Vector3 V1, V2, V3;
        
        public ChunkGeometry(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            V1 = v1; V2 = v2; V3 = v3;
        }
    }
    
    public struct MeshSettings
    {
        public int Resolution;
        public ShapeGenerator ShapeGenerator;

        public MeshSettings(int res, ShapeGenerator generator)
        {
            Resolution = res;
            ShapeGenerator = generator;
        }
    }

    public struct VertexData
    {
        public Vector3 Position;
        public Color BiomeColor; // Pushed to shader to blend textures/colors
        // You can add uv, normals, etc., here later
    }

    public struct BiomePoint
    {
        public Vector3 Position; // Normalized direction on the sphere
        public int BiomeIndex;   // Which biome this specific cell belongs to
    }

    [System.Serializable]
    public struct NoiseLayer
    {
        public bool enabled;
        public float frequency;
        public float strength;
        public float baseRoughness; // Optional: offset multiplier inside the noise space
    }

    /// <summary>
    /// Flattens complex Biome data into primitive types for GPU memory transfer.
    /// Memory alignment must exactly match the HLSL struct.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GPUBiome
    {
        public readonly float startThreshold;
        public readonly float baseHeightOffset;
        public readonly float amplitude;
        public readonly float frequency;
        public readonly int fractalType;
        public readonly int octaves;
        public readonly float lacunarity;
        public readonly float gain;
        public readonly Color biomeColor;

        public static int Stride => 48;

        public GPUBiome(BiomeData data)
        {
            startThreshold = data.startThreshold;
            baseHeightOffset = data.baseHeightOffset;
            amplitude = data.amplitude;
            frequency = data.frequency;
            fractalType = (int)data.fractalType;
            octaves = data.octaves;
            lacunarity = data.lacunarity;
            gain = data.gain;
            biomeColor = data.biomeColor;
        }
    }

    /// <summary>
    /// Represents a spherical Voronoi center point on the GPU.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GPUBiomePoint
    {
        public readonly Vector3 position;
        public readonly int biomeIndex;

        // 3 floats (Vector3) + 1 int = 16 bytes
        public static int Stride => 16;

        public GPUBiomePoint(Vector3 pos, int index)
        {
            position = pos;
            biomeIndex = index;
        }
    }

    /// <summary>
    /// The final payload returned from the GPU for each vertex.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GPUVertexData
    {
        public Vector3 position;
        public float padding;
        public Color color;

        // 3 floats (Vector3) + 5 floats (Color) = 32 bytes
        public static int Stride => 32;
    }
}
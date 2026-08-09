using System;
using UnityEngine;

namespace PlanetGeneration
{
    [Serializable]
    public class BiomeData
    {
        public string biomeName = "New Biome";

        [Range(-1f, 1f)]
        public float startThreshold;

        [Header("Terrain Elevation")]
        public float baseHeightOffset = 0f;
        public float amplitude = 10f;

        [Header("Terrain Noise Shape")]
        public float frequency = 0.5f;
        public FastNoiseLite.FractalType fractalType = FastNoiseLite.FractalType.FBm;

        [Range(1, 8)]
        public int octaves = 4;
        public float lacunarity = 2.0f;
        public float gain = 0.5f;

        [Header("Appearance")]
        public Color biomeColor = Color.white;
    }


    [CreateAssetMenu(menuName = "Planet/Settings")]
    public class PlanetSettings : ScriptableObject
    {
        public delegate void SettingsUpdated();
        public event SettingsUpdated OnSettingsUpdated;

        [Header("Global Settings")]
        public float radius = 100f;
        public Material material;

        [Header("LOD Settings")]
        public int chunkResolution = 16;
        [Range(1, 15)] public int maxRecursionDepth = 6;
        public float[] lodDistances = new float[] { 100, 250, 500, 1000, 2000 };

        [Header("Debug Settings")]
        public bool disableLOD = false;
        [Tooltip("Forces the entire planet to generate at this recursion level when LOD is disabled.")]
        [Range(0, 7)] public int fixedLODLevel = 0;

        [Header("Physics Settings")]
        public float collisionDistance = 100f;

        [Header("Biome Map Generation")]
        public float biomeNoiseFrequency = 0.005f;
        [Range(0.001f, 0.5f)] public float biomeBlendDistance = 0.05f;

        [Tooltip("How many total Voronoi cells wrap the planet.")]
        public int biomeNoisePoints = 100;

        [Tooltip("0 = Perfect Grid. 1 = Highly organic/randomized placement.")]
        [Range(0f, 1f)] public float biomePointJitter = 0.6f;

        [Header("Biome Map Distortion (Domain Warp)")]
        public bool enableWarp = true;
        public float warpAmplitude = 30f;
        public float warpFrequency = 0.5f;

        [Header("Biomes (Sort Ascending by Threshold)")]
        public BiomeData[] biomes;

        private void OnValidate()
        {
            OnSettingsUpdated?.Invoke();
        }
    }
}
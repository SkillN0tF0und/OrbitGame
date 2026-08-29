using System;
using UnityEngine;

namespace PlanetGeneration
{
    [System.Serializable]
    public struct BiomePlacement
    {
        public BiomeData biome;
        //determines on which noise value each biome exists.
        [Range(-1f, 1f)] public float startThreshold;
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
        [Range(0f, 1f)]
        public float backFaceCullThreshold; // removed this for now
        [Range(1, 15)] public int maxRecursionDepth = 6;
        public float[] lodDistances = new float[] { 100, 250, 500, 1000, 2000 };
        [Header("Debug Settings")]
        public bool disableLOD = false;
        [Tooltip("Forces the entire planet to generate at this recursion level when LOD is disabled.")]
        [Range(0, 7)] public int fixedLODLevel = 0;
        
        public int planetSeed = 1337;

        

        [Header("Biome Map Generation")]
        public float biomeNoiseFrequency = 0.005f;
        [Range(0.001f, 0.5f)] public float biomeBlendDistance = 0.05f;

        // how many cells the planet surface gets split into based on the nois
        public int biomeNoisePoints = 100;
        [Range(0f, 1f)] public float biomePointJitter = 0.6f;

        [Header("Biome Map Distortion (Domain Warp)")]
        public bool enableWarp = true;
        public float warpAmplitude = 30f;
        public float warpFrequency = 0.5f;

        [Header("Global Planet Visuals")]
        [Range(1f, 20f)] public float colorBlendSharpness = 5f;
        [Range(0f, 1f)] public float globalSteepnessThreshold = 0.5f;

        [Header("Biomes (Sort Ascending by Threshold)")]
        public BiomePlacement[] biomePlacements;

        [Header("Ocean Configuration")]
        public Material oceanMaterial;
        public Color oceanColorShallow = new Color(0.1f, 0.6f, 0.8f, 0.8f);
        public Color oceanColorDeep = new Color(0.01f, 0.1f, 0.4f, 0.95f);
        public float oceanDepthMultiplier = 0.5f;
        public float oceanAlphaMultiplier = 1.0f;

        [Header("Ocean Waves")]
        public Texture2D waveNormalA;
        public Texture2D waveNormalB;
        [Range(0f, 1f)] public float waveStrength = 0.15f;
        public float waveScale = 0.05f;
        public float waveSpeed = 0.5f;
        [Range(0f, 1f)] public float oceanSmoothness = 0.95f;

        private void OnValidate()
        {
            OnSettingsUpdated?.Invoke();
        }
    }
}
using System;
using UnityEngine;

namespace PlanetGeneration
{
    [System.Serializable]
    public struct BiomePlacement
    {
        public BiomeData biome;
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
        public float backFaceCullThreshold;
        [Range(1, 15)] public int maxRecursionDepth = 6;
        public float[] lodDistances = new float[] { 100, 250, 500, 1000, 2000 };

        [Header("Generation Settings")]
        public int planetSeed = 1337;

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

        [Header("Global Planet Visuals")]
        [Range(1f, 20f)] public float colorBlendSharpness = 5f;
        [Range(0f, 1f)] public float globalSteepnessThreshold = 0.5f;

        [Header("Biomes (Sort Ascending by Threshold)")]
        public BiomePlacement[] biomePlacements;

        private void OnValidate()
        {
            OnSettingsUpdated?.Invoke();
        }
    }
}
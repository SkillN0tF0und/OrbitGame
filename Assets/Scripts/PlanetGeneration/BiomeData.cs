using System;
using UnityEngine;

namespace PlanetGeneration
{
    [CreateAssetMenu(fileName = "New Biome", menuName = "Planet Generation/Biome Data")]
    public class BiomeData : ScriptableObject
    {
        public event Action OnBiomeUpdated;

        [Header("Terrain Shape")]
        public float baseHeightOffset = 0f;
        public float amplitude = 1f;
        public float frequency = 1f;
        
        public FastNoiseLite.FractalType fractalType = FastNoiseLite.FractalType.None;
        [Range(1, 6)] public int octaves = 3;
        public float lacunarity = 2f;
        public float gain = 0.5f;

        [Header("Surface Colors")]
        public Color groundColor = Color.green;
        public Color cliffColor = Color.gray;
        public Color noiseColor = new Color(0.8f, 0.8f, 0.1f);

        [Header("Surface Details")]
        public FastNoiseLite.NoiseType visualNoiseType = FastNoiseLite.NoiseType.OpenSimplex2;
        public float visualFrequency = 5f;
        [Range(-1f, 1f)] public float noiseThreshold = 0.5f;

        [Header("PBR")]
        [Range(0f, 1f)] public float metallic = 0.0f;
        [Range(0f, 1f)] public float smoothness = 0.5f;
        [Range(0f, 5f)] public float bumpStrength = 1.0f;

        [Header("surface visual stretch")]
        public Vector3 stretchDirection = new Vector3(0, 0, 0);

        private void OnValidate()
        {
            OnBiomeUpdated?.Invoke();
        }
        
        
        //convert data to struct usable for shaders. 
        public PlanetBiome ToGPUBiome(float assignedThreshold)
        {
            return new PlanetBiome(
                baseHeightOffset, amplitude, frequency,
                (int)fractalType, octaves, lacunarity, gain,
                groundColor, cliffColor, noiseColor,
                (int)visualNoiseType, visualFrequency, noiseThreshold, stretchDirection.x,
                stretchDirection.y, stretchDirection.z, metallic, smoothness,
                bumpStrength
            );
        }
    }
}
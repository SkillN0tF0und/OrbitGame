using System;
using UnityEngine;

namespace PlanetGeneration
{
    public class ShapeGenerator
    {
        private readonly PlanetSettings _settings;
        private readonly FastNoiseLite _biomeMapNoise;
        private readonly FastNoiseLite _warpNoise;
        private readonly FastNoiseLite[] _terrainNoises;
        private readonly BiomePoint[] _biomePoints;

        public ShapeGenerator(PlanetSettings settings)
        {
            _settings = settings;

            _biomeMapNoise = InitializeMacroNoise();
            _warpNoise = InitializeWarpNoise();
            _terrainNoises = InitializeTerrainNoises();
            _biomePoints = GenerateBiomePoints();
        }

        public BiomePoint[] GetBiomePoints()
        {
            return _biomePoints;
        }

        public VertexData CalculateVertexData(Vector3 unitVector)
        {
            Vector3 samplePos = unitVector * _settings.radius;
            Vector3 warpedDir = GetWarpedDirection(unitVector, samplePos);

            FindClosestBiomes(warpedDir, out int indexA, out int indexB, out float edgeDiff);
            float blendWeight = CalculateBlendWeight(edgeDiff);

            float heightA = GetBiomeElevation(indexA, samplePos);
            float heightB = (blendWeight > 0f) ? GetBiomeElevation(indexB, samplePos) : heightA;

            float finalElevation = Mathf.Lerp(heightA, heightB, blendWeight);
            Color finalColor = Color.Lerp(_settings.biomes[indexA].biomeColor, _settings.biomes[indexB].biomeColor, blendWeight);

            return new VertexData
            {
                Position = unitVector * (_settings.radius + finalElevation),
                BiomeColor = finalColor
            };
        }

        #region Initialization Helpers

        private FastNoiseLite InitializeMacroNoise()
        {
            var noise = new FastNoiseLite();
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            noise.SetFrequency(_settings.biomeNoiseFrequency);
            return noise;
        }

        private FastNoiseLite InitializeWarpNoise()
        {
            var noise = new FastNoiseLite();
            noise.SetDomainWarpType(FastNoiseLite.DomainWarpType.OpenSimplex2);
            noise.SetDomainWarpAmp(_settings.warpAmplitude);
            noise.SetFrequency(_settings.warpFrequency);
            return noise;
        }

        private FastNoiseLite[] InitializeTerrainNoises()
        {
            if (_settings.biomes == null) return Array.Empty<FastNoiseLite>();

            var noises = new FastNoiseLite[_settings.biomes.Length];
            for (int b = 0; b < _settings.biomes.Length; b++)
            {
                BiomeData biome = _settings.biomes[b];
                var noise = new FastNoiseLite();

                noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
                noise.SetFractalType(biome.fractalType);
                noise.SetFrequency(biome.frequency);
                noise.SetFractalOctaves(biome.octaves);
                noise.SetFractalLacunarity(biome.lacunarity);
                noise.SetFractalGain(biome.gain);

                noises[b] = noise;
            }
            return noises;
        }

        #endregion

        #region Point Generation Helpers

        private BiomePoint[] GenerateBiomePoints()
        {
            if (_settings.biomes == null || _settings.biomes.Length == 0) return Array.Empty<BiomePoint>();

            int numPoints = _settings.biomeNoisePoints;
            BiomePoint[] points = new BiomePoint[numPoints];
            float phi = Mathf.PI * (3f - Mathf.Sqrt(5f)); // Golden angle

            // Fixed seed prevents the organic points from wildly reshuffling every time a slider is tweaked
            UnityEngine.Random.InitState(1337);

            for (int i = 0; i < numPoints; i++)
            {
                // Calculate perfect grid distribution
                Vector3 fibPos = CalculateFibonacciSpherePoint(i, numPoints, phi);

                // Add jitter to destroy the grid layout and create organic shapes
                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * _settings.biomePointJitter;
                Vector3 finalPos = (fibPos + randomOffset).normalized;

                points[i] = new BiomePoint
                {
                    Position = finalPos,
                    BiomeIndex = DetermineBiomeForPosition(finalPos)
                };
            }
            return points;
        }

        private Vector3 CalculateFibonacciSpherePoint(int index, int numPoints, float phi)
        {
            float y = 1f - (index / (float)(numPoints - 1)) * 2f;
            float radius = Mathf.Sqrt(1f - y * y);
            float theta = phi * index;
            return new Vector3(Mathf.Cos(theta) * radius, y, Mathf.Sin(theta) * radius);
        }

        private int DetermineBiomeForPosition(Vector3 pos)
        {
            Vector3 samplePos = pos * _settings.radius;
            float noiseVal = _biomeMapNoise.GetNoise(samplePos.x, samplePos.y, samplePos.z);

            int assignedBiome = 0;
            for (int b = 0; b < _settings.biomes.Length; b++)
            {
                if (noiseVal >= _settings.biomes[b].startThreshold)
                    assignedBiome = b;
                else
                    break;
            }

            // Safety clamp to ensure valid array indices
            return Mathf.Clamp(assignedBiome, 0, _settings.biomes.Length - 1);
        }

        #endregion

        #region Vertex Calculation Helpers (CPU Fallback)

        private Vector3 GetWarpedDirection(Vector3 unitVector, Vector3 samplePos)
        {
            if (!_settings.enableWarp) return unitVector;

            float wx = samplePos.x;
            float wy = samplePos.y;
            float wz = samplePos.z;

            _warpNoise.DomainWarp(ref wx, ref wy, ref wz);
            return new Vector3(wx, wy, wz).normalized;
        }

        private void FindClosestBiomes(Vector3 warpedDir, out int indexA, out int indexB, out float edgeDiff)
        {
            float d1Sqr = float.MaxValue;
            indexA = 0;

            // Pass 1: Find the absolute closest point
            for (int i = 0; i < _biomePoints.Length; i++)
            {
                float sqrDist = (warpedDir - _biomePoints[i].Position).sqrMagnitude;
                if (sqrDist < d1Sqr)
                {
                    d1Sqr = sqrDist;
                    indexA = _biomePoints[i].BiomeIndex;
                }
            }

            float d2Sqr = float.MaxValue;
            indexB = indexA;

            // Pass 2: Find the closest point belonging to a DIFFERENT biome
            for (int j = 0; j < _biomePoints.Length; j++)
            {
                if (_biomePoints[j].BiomeIndex == indexA) continue;

                float sqrDist = (warpedDir - _biomePoints[j].Position).sqrMagnitude;
                if (sqrDist < d2Sqr)
                {
                    d2Sqr = sqrDist;
                    indexB = _biomePoints[j].BiomeIndex;
                }
            }

            edgeDiff = (indexA != indexB && d2Sqr != float.MaxValue)
                ? Mathf.Sqrt(d2Sqr) - Mathf.Sqrt(d1Sqr)
                : float.MaxValue;
        }

        private float CalculateBlendWeight(float edgeDiff)
        {
            if (edgeDiff >= _settings.biomeBlendDistance) return 0f;

            float t = edgeDiff / _settings.biomeBlendDistance;
            return Mathf.SmoothStep(0f, 1f, 0.5f * (1f - t));
        }

        private float GetBiomeElevation(int biomeIndex, Vector3 samplePos)
        {
            // Safety clamp
            biomeIndex = Mathf.Clamp(biomeIndex, 0, _settings.biomes.Length - 1);

            BiomeData biome = _settings.biomes[biomeIndex];
            float noise = _terrainNoises[biomeIndex].GetNoise(samplePos.x, samplePos.y, samplePos.z);

            if (biome.fractalType == FastNoiseLite.FractalType.Ridged)
                noise = Mathf.Abs(noise);

            return biome.baseHeightOffset + (noise * biome.amplitude);
        }

        #endregion
    }
}
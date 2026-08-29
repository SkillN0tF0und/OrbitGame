using System;
using UnityEngine;

namespace PlanetGeneration
{
    public class ShapeGenerator
    {
        private readonly PlanetSettings _settings;
        private readonly FastNoiseLite _biomeMapNoise;
        private readonly GPUBiomePoint[] _gpuBiomePoints;

        public ShapeGenerator(PlanetSettings settings)
        {
            _settings = settings;
            _biomeMapNoise = InitializeMacroNoise();

            // Generate points on surface to use voronoi cells for biomes
            _gpuBiomePoints = GenerateBiomePoints();
        }

        public GPUBiomePoint[] GetBiomePoints()
        {
            return _gpuBiomePoints;
        }

        private FastNoiseLite InitializeMacroNoise()
        {
            var noise = new FastNoiseLite();
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            noise.SetFrequency(_settings.biomeNoiseFrequency);
            return noise;
        }

        private GPUBiomePoint[] GenerateBiomePoints()
        {
            if (_settings.biomePlacements == null || _settings.biomePlacements.Length == 0) return Array.Empty<GPUBiomePoint>();

            int numPoints = _settings.biomeNoisePoints;
            GPUBiomePoint[] points = new GPUBiomePoint[numPoints];
            float phi = Mathf.PI * (3f - Mathf.Sqrt(5f));
            
            UnityEngine.Random.InitState(_settings.planetSeed);

            for (int i = 0; i < numPoints; i++)
            {
                Vector3 fibPos = CalculateFibonacciSpherePoint(i, numPoints, phi);

                //slight offset to imitate noise
                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * _settings.biomePointJitter;
                Vector3 finalPos = (fibPos + randomOffset).normalized;

                points[i] = new GPUBiomePoint(finalPos, DetermineBiomeForPosition(finalPos));
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
            for (int b = 0; b < _settings.biomePlacements.Length; b++)
            {
                if (noiseVal >= _settings.biomePlacements[b].startThreshold) assignedBiome = b;
                else break;
            }

            return Mathf.Clamp(assignedBiome, 0, _settings.biomePlacements.Length - 1);
        }
    }
}
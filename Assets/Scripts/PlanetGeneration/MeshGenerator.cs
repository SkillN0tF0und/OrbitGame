using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

namespace PlanetGeneration
{
    public static class MeshGenerator
    {
        private const int ThreadGroupSize = 64;

        // Cache shader property IDs to eliminate string-hashing overhead
        private static readonly int InputDirectionsId = Shader.PropertyToID("_InputDirections");
        private static readonly int OutputVerticesId = Shader.PropertyToID("_OutputVertices");
        private static readonly int BiomesId = Shader.PropertyToID("_Biomes");
        private static readonly int BiomePointsId = Shader.PropertyToID("_BiomePoints");
        
        private static readonly int VertexCountId = Shader.PropertyToID("_VertexCount");
        private static readonly int BiomeCountId = Shader.PropertyToID("_BiomeCount");
        private static readonly int BiomePointCountId = Shader.PropertyToID("_BiomePointCount");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int BiomeBlendDistanceId = Shader.PropertyToID("_BiomeBlendDistance");
        
        private static readonly int EnableWarpId = Shader.PropertyToID("_EnableWarp");
        private static readonly int WarpAmplitudeId = Shader.PropertyToID("_WarpAmplitude");
        private static readonly int WarpFrequencyId = Shader.PropertyToID("_WarpFrequency");
        private static readonly int MacroSeedId = Shader.PropertyToID("_MacroSeed");

        public static void CreateChunkMeshAsync(
            ChunkGeometry geo,
            MeshSettings settings,
            ComputeShader computeShader,
            ComputeBuffer biomeBuffer,
            GPUBiomePoint[] gpuBiomePoints,
            PlanetSettings planetSettings,
            Action<Mesh> onMeshCompleted)
        {
            int numEdgeVerts = (3 * settings.Resolution) - 2;
            int vertexCount = (numEdgeVerts * (numEdgeVerts + 1)) / 2;
            int triangleCount = (numEdgeVerts - 1) * (numEdgeVerts - 1);

            GenerateGridTopology(geo, numEdgeVerts, vertexCount, triangleCount,
                out Vector3[] inputDirections, out int[] triangles);

            DispatchComputeAsync(
                vertexCount, inputDirections, triangles, computeShader,
                biomeBuffer, gpuBiomePoints, planetSettings, onMeshCompleted);
        }

        private static void GenerateGridTopology(
            ChunkGeometry geo, int numEdgeVerts, int vertexCount, int triangleCount,
            out Vector3[] directions, out int[] triangles)
        {
            directions = new Vector3[vertexCount];
            triangles = new int[3 * triangleCount];

            int v = 0;
            int t = 0;

            for (int y = 0; y < numEdgeVerts; y++)
            {
                int rowSize = numEdgeVerts - y;
                float heightProgress = numEdgeVerts > 1 ? (float)y / (numEdgeVerts - 1) : 0f;
                Vector3 rowStart = Vector3.Lerp(geo.V2, geo.V1, heightProgress);
                Vector3 rowEnd = Vector3.Lerp(geo.V3, geo.V1, heightProgress);

                for (int x = 0; x < rowSize; x++)
                {
                    float horizontalProgress = rowSize > 1 ? (float)x / (rowSize - 1) : 0f;
                    Vector3 gridPoint = Vector3.Lerp(rowStart, rowEnd, horizontalProgress);

                    directions[v] = gridPoint.normalized;

                    if (y < numEdgeVerts - 1 && x < rowSize - 1)
                    {
                        triangles[t++] = v;
                        triangles[t++] = v + 1;
                        triangles[t++] = v + rowSize;

                        if (x > 0)
                        {
                            triangles[t++] = v;
                            triangles[t++] = v + rowSize;
                            triangles[t++] = v + rowSize - 1;
                        }
                    }
                    v++;
                }
            }
        }

        private static void DispatchComputeAsync(
            int vertexCount, Vector3[] inputDirections, int[] triangles, ComputeShader computeShader,
            ComputeBuffer biomeBuffer, GPUBiomePoint[] gpuBiomePoints, PlanetSettings settings, Action<Mesh> onMeshCompleted)
        {
            ComputeBuffer inputDirBuffer = new ComputeBuffer(vertexCount, 12);
            ComputeBuffer outputVertBuffer = new ComputeBuffer(vertexCount, GPUVertexData.Stride);
            ComputeBuffer biomePointBuffer = new ComputeBuffer(gpuBiomePoints.Length, GPUBiomePoint.Stride);

            inputDirBuffer.SetData(inputDirections);
            biomePointBuffer.SetData(gpuBiomePoints);

            // ComputeShader kernels must still be fetched by string unless you hardcode the index (usually 0).
            int kernel = computeShader.FindKernel("GenerateVertices");

            // Use the cached integer IDs
            computeShader.SetBuffer(kernel, InputDirectionsId, inputDirBuffer);
            computeShader.SetBuffer(kernel, OutputVerticesId, outputVertBuffer);
            computeShader.SetBuffer(kernel, BiomesId, biomeBuffer);
            computeShader.SetBuffer(kernel, BiomePointsId, biomePointBuffer);

            computeShader.SetInt(VertexCountId, vertexCount);
            computeShader.SetInt(BiomeCountId, biomeBuffer.count);
            computeShader.SetInt(BiomePointCountId, gpuBiomePoints.Length);
            computeShader.SetFloat(RadiusId, settings.radius);
            computeShader.SetFloat(BiomeBlendDistanceId, settings.biomeBlendDistance);
            
            computeShader.SetInt(EnableWarpId, settings.enableWarp ? 1 : 0);
            computeShader.SetFloat(WarpAmplitudeId, settings.warpAmplitude);
            computeShader.SetFloat(WarpFrequencyId, settings.warpFrequency);
            computeShader.SetInt(MacroSeedId, settings.planetSeed);

            int threadGroups = Mathf.CeilToInt(vertexCount / (float)ThreadGroupSize);
            computeShader.Dispatch(kernel, threadGroups, 1, 1);

            AsyncGPUReadback.Request(outputVertBuffer, request =>
            {
                if (request.hasError)
                {
                    Debug.LogError("GPU Readback error: Failed to retrieve chunk data.");
                    ReleaseBuffers(inputDirBuffer, outputVertBuffer, biomePointBuffer);
                    return;
                }

                NativeArray<GPUVertexData> outputData = request.GetData<GPUVertexData>();
                Mesh mesh = AssembleMesh(vertexCount, triangles, outputData);

                ReleaseBuffers(inputDirBuffer, outputVertBuffer, biomePointBuffer);

                onMeshCompleted?.Invoke(mesh);
            });
        }

        private static void ReleaseBuffers(params ComputeBuffer[] buffers)
        {
            foreach (var buffer in buffers)
            {
                buffer?.Release();
            }
        }

        private static Mesh AssembleMesh(int vertexCount, int[] triangles, NativeArray<GPUVertexData> outputData)
        {
            Vector3[] finalVertices = new Vector3[vertexCount];
            Vector3[] finalNormals = new Vector3[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                var data = outputData[i];
                finalVertices[i] = data.position;
                finalNormals[i] = data.normal;
            }

            Mesh mesh = new Mesh { name = "ChunkMesh" };
            mesh.vertices = finalVertices;
            mesh.normals = finalNormals;
            mesh.triangles = triangles;

            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
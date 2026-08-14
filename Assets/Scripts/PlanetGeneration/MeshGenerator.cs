using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

namespace PlanetGeneration
{
    public static class MeshGenerator
    {
        private const int ThreadGroupSize = 64;

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

            int kernel = computeShader.FindKernel("GenerateVertices");

            computeShader.SetBuffer(kernel, "_InputDirections", inputDirBuffer);
            computeShader.SetBuffer(kernel, "_OutputVertices", outputVertBuffer);
            computeShader.SetBuffer(kernel, "_Biomes", biomeBuffer);
            computeShader.SetBuffer(kernel, "_BiomePoints", biomePointBuffer);

            computeShader.SetInt("_VertexCount", vertexCount);
            computeShader.SetInt("_BiomeCount", biomeBuffer.count);
            computeShader.SetInt("_BiomePointCount", gpuBiomePoints.Length);
            computeShader.SetFloat("_Radius", settings.radius);
            computeShader.SetFloat("_BiomeBlendDistance", settings.biomeBlendDistance);
            computeShader.SetInt("_EnableWarp", settings.enableWarp ? 1 : 0);
            computeShader.SetFloat("_WarpAmplitude", settings.warpAmplitude);
            computeShader.SetFloat("_WarpFrequency", settings.warpFrequency);
            computeShader.SetInt("_MacroSeed", settings.planetSeed);

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
            Vector3[] finalUV0 = new Vector3[vertexCount];
            Vector3[] finalUV1 = new Vector3[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                var data = outputData[i];
                finalVertices[i] = data.position;

                finalUV0[i] = data.biomeIndices;
                finalUV1[i] = data.biomeWeights;
            }

            Mesh mesh = new Mesh { name = "ChunkMesh" };
            mesh.vertices = finalVertices;
            mesh.triangles = triangles;

            mesh.SetUVs(0, finalUV0);
            mesh.SetUVs(1, finalUV1);

            // Unity calculates the normals using the newly displaced vertices
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
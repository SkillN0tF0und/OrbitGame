using UnityEngine;

namespace PlanetGeneration
{
    public static class MeshGenerator
    {
        private const int ThreadGroupSize = 64;

        /// <summary>
        /// Orchestrates the CPU topology generation, GPU vertex displacement, and final mesh assembly.
        /// </summary>
        public static Mesh CreateChunkMesh(
            ChunkGeometry geo,
            MeshSettings settings,
            ComputeShader computeShader,
            GPUBiome[] gpuBiomes,
            GPUBiomePoint[] gpuBiomePoints,
            PlanetSettings planetSettings)
        {
            int numEdgeVerts = (3 * settings.Resolution) - 2;
            int vertexCount = (numEdgeVerts * (numEdgeVerts + 1)) / 2;
            int triangleCount = (numEdgeVerts - 1) * (numEdgeVerts - 1);

            // 1. Calculate base layout and indices on the CPU
            GenerateGridTopology(geo, numEdgeVerts, vertexCount, triangleCount,
                out Vector3[] inputDirections, out int[] triangles);

            // 2. Offload mathematical displacement to the GPU
            GPUVertexData[] outputData = ProcessVerticesOnGPU(
                vertexCount, inputDirections, computeShader, gpuBiomes, gpuBiomePoints, planetSettings);

            // 3. Construct the Unity Mesh
            return AssembleMesh(vertexCount, triangles, outputData);
        }

        /// <summary>
        /// Generates the normalized sphere directions and the triangle index array.
        /// </summary>
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

                    // Build topology winding (Triangle mapping)
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

        /// <summary>
        /// Dispatches the Compute Shader and manages buffer memory.
        /// </summary>
        private static GPUVertexData[] ProcessVerticesOnGPU(
            int vertexCount, Vector3[] inputDirections, ComputeShader computeShader,
            GPUBiome[] gpuBiomes, GPUBiomePoint[] gpuBiomePoints, PlanetSettings settings)
        {
            // 'using var' guarantees these buffers are disposed/released safely at the end of the method scope
            using var inputDirBuffer = new ComputeBuffer(vertexCount, 12);
            using var outputVertBuffer = new ComputeBuffer(vertexCount, GPUVertexData.Stride);
            using var biomeBuffer = new ComputeBuffer(gpuBiomes.Length, GPUBiome.Stride);
            using var biomePointBuffer = new ComputeBuffer(gpuBiomePoints.Length, GPUBiomePoint.Stride);

            inputDirBuffer.SetData(inputDirections);
            biomeBuffer.SetData(gpuBiomes);
            biomePointBuffer.SetData(gpuBiomePoints);

            int kernel = computeShader.FindKernel("GenerateVertices");

            // Bind buffers
            computeShader.SetBuffer(kernel, "_InputDirections", inputDirBuffer);
            computeShader.SetBuffer(kernel, "_OutputVertices", outputVertBuffer);
            computeShader.SetBuffer(kernel, "_Biomes", biomeBuffer);
            computeShader.SetBuffer(kernel, "_BiomePoints", biomePointBuffer);

            // Bind primitives
            computeShader.SetInt("_BiomeCount", gpuBiomes.Length);
            computeShader.SetInt("_BiomePointCount", gpuBiomePoints.Length);
            computeShader.SetFloat("_Radius", settings.radius);
            computeShader.SetFloat("_BiomeBlendDistance", settings.biomeBlendDistance);
            computeShader.SetInt("_EnableWarp", settings.enableWarp ? 1 : 0);
            computeShader.SetFloat("_WarpAmplitude", settings.warpAmplitude);
            computeShader.SetFloat("_WarpFrequency", settings.warpFrequency);
            computeShader.SetInt("_MacroSeed", 1337);

            // Dispatch
            int threadGroups = Mathf.CeilToInt(vertexCount / (float)ThreadGroupSize);
            computeShader.SetInt("_VertexCount", vertexCount);
            computeShader.Dispatch(kernel, threadGroups, 1, 1);

            // Synchronous readback (Will stall CPU; transition to AsyncGPUReadback for production)
            GPUVertexData[] outputData = new GPUVertexData[vertexCount];
            outputVertBuffer.GetData(outputData);

            return outputData;
        }

        /// <summary>
        /// Extracts GPU payload back into Unity-native arrays and builds the Mesh object.
        /// </summary>
        private static Mesh AssembleMesh(int vertexCount, int[] triangles, GPUVertexData[] outputData)
        {
            Vector3[] finalVertices = new Vector3[vertexCount];
            Color[] finalColors = new Color[vertexCount];
            Vector3[] finalNormals = new Vector3[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                finalVertices[i] = outputData[i].position;
                finalColors[i] = outputData[i].color;

                // Temporary normal calculation (Points straight up from center)
                finalNormals[i] = outputData[i].position.normalized;
            }

            Mesh mesh = new Mesh { name = "ChunkMesh" };
            mesh.vertices = finalVertices;
            mesh.triangles = triangles;
            mesh.colors = finalColors;
            mesh.normals = finalNormals;

            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
using System.Buffers;
using UnityEngine;

namespace PlanetGeneration
{
    public static class OldMeshGenerator
    {
        #region structs
        private struct GridAxes
        {
            public Vector3 Origin, AxisX, AxisY;
            public float StepX, StepY;
        }

        private struct MeshBuffer
        {
            public readonly int EdgeCount;
            public readonly GridAxes Axes;
            public readonly Vector3[] Vertices;
            public readonly int[] Triangles;
            public readonly Color[] Colors;
            public readonly ShapeGenerator ShapeGen;
            public readonly int VerticesPerEdge;

            public MeshBuffer(int edgeCount, GridAxes axes, Vector3[] vertices, int[] triangles, Color[] colors, ShapeGenerator shapeGen, int verticesPerEdge)
            {
                EdgeCount = edgeCount;
                Axes = axes;
                Vertices = vertices;
                Triangles = triangles;
                Colors = colors;
                ShapeGen = shapeGen;
                VerticesPerEdge = verticesPerEdge;
            }
        }
        #endregion

        public static Mesh CreateChunkMesh(ChunkGeometry geo, MeshSettings settings)
        {
            MeshBuffer buffer = InitializeBuffers(geo, settings);
            GenerateGeometry(buffer, geo);
            return CompileMesh(buffer);
        }

        private static GridAxes CalculateGridAxes(ChunkGeometry geo, int edgeCount)
        {
            Vector3 axisX = (geo.V3 - geo.V2).normalized;
            Vector3 faceNormal = (geo.V1 + geo.V2 + geo.V3).normalized;
            Vector3 axisY = Vector3.Cross(faceNormal, axisX).normalized;

            return new GridAxes
            {
                Origin = geo.V2,
                AxisX = axisX,
                AxisY = axisY,
                StepX = Vector3.Distance(geo.V2, geo.V3) / (edgeCount - 1),
                StepY = Vector3.Distance(geo.V1, (geo.V2 + geo.V3) * 0.5f) / (edgeCount - 1)
            };
        }

        private static void GenerateGeometry(MeshBuffer buffers, ChunkGeometry geo)
        {
            int v = 0;
            int t = 0;
            int maxIndex = buffers.VerticesPerEdge - 1;

            for (int y = 0; y < buffers.VerticesPerEdge; y++)
            {
                int rowSize = buffers.VerticesPerEdge - y;

                float heightProgress = maxIndex > 0 ? (float)y / maxIndex : 0f;
                Vector3 rowStart = Vector3.Lerp(geo.V2, geo.V1, heightProgress);
                Vector3 rowEnd = Vector3.Lerp(geo.V3, geo.V1, heightProgress);

                for (int x = 0; x < rowSize; x++)
                {
                    float horizontalProgress = rowSize > 1 ? (float)x / (rowSize - 1) : 0f;
                    Vector3 gridPoint = Vector3.Lerp(rowStart, rowEnd, horizontalProgress);

                    VertexData vertexData = buffers.ShapeGen.CalculateVertexData(gridPoint.normalized);

                    buffers.Vertices[v] = vertexData.Position;
                    buffers.Colors[v] = vertexData.BiomeColor;

                    if (y < buffers.VerticesPerEdge - 1 && x < rowSize - 1)
                    {
                        buffers.Triangles[t++] = v;
                        buffers.Triangles[t++] = v + 1;
                        buffers.Triangles[t++] = v + rowSize;

                        if (x > 0)
                        {
                            buffers.Triangles[t++] = v;
                            buffers.Triangles[t++] = v + rowSize;
                            buffers.Triangles[t++] = v + rowSize - 1;
                        }
                    }
                    v++;
                }
            }
        }

        private static MeshBuffer InitializeBuffers(ChunkGeometry geo, MeshSettings settings)
        {
            int numEdgeVerts = (3 * settings.Resolution) - 2;
            int vertexCount = (numEdgeVerts * (numEdgeVerts + 1)) / 2;
            int triangleCount = (numEdgeVerts - 1) * (numEdgeVerts - 1);

            Vector3[] vertices = new Vector3[vertexCount];
            int[] triangles = new int[3 * triangleCount];
            Color[] colors = new Color[vertexCount];

            GridAxes axes = CalculateGridAxes(geo, numEdgeVerts);

            int vertsPerEdge = settings.Resolution;

            return new MeshBuffer(numEdgeVerts, axes, vertices, triangles, colors, settings.ShapeGenerator, vertsPerEdge);
        }

        private static Mesh CompileMesh(MeshBuffer buffers)
        {
            Mesh mesh = new Mesh { name = "ChunkMesh" };

            mesh.vertices = buffers.Vertices;
            mesh.triangles = buffers.Triangles;
            mesh.colors = buffers.Colors;

            Vector3[] normals = new Vector3[buffers.Vertices.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = buffers.Vertices[i].normalized;
            }
            mesh.normals = normals;

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
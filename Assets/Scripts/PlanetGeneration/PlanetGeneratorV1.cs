/*using UnityEngine;
using UnityEngine.Rendering;

namespace PlanetGeneration
{
    [ExecuteAlways]
    public class PlanetGeneratorV1 : MonoBehaviour
    {
        [SerializeField] private PlanetSettings settings;
        private GeometryData _icosahedronData;
        
        private struct FaceAxes {
            public Vector3 Origin;
            public Vector3 AxisX;
            public Vector3 AxisY;
            public float StepX;
            public float StepY;
        }

        private void OnValidate()
        {
            _icosahedronData = new GeometryData();
            
            if (settings != null)
            {
                settings.OnSettingsUpdated -= HandleSettingsUpdate;
                settings.OnSettingsUpdated += HandleSettingsUpdate;
            }

            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => {
                if (this != null) GeneratePlanet();
            };
            #endif
        }

        private void GeneratePlanet()
        {
            if (settings == null) return;

            ClearOldFaces();

            int dynamicResolution = GetTargetResolution();

            foreach (GeometryData.Face face in _icosahedronData.Faces)
            {
                GenerateFace(face, dynamicResolution);
            }
        }

        private void GenerateFace(GeometryData.Face face, int resolution)
        {
            MeshFilter filter = SetupFaceComponents(out MeshRenderer renderer);
            
            Mesh mesh = CreateBaseMesh(resolution, out Vector3[] vertices, out int[] triangles);
            
            FaceAxes axes = CalculateFaceAxes(face, resolution);
            
            PopulateArrays(resolution, axes, vertices, triangles);
            
            FinalizeMesh(mesh, filter, vertices, triangles);
        }
            

        private MeshFilter SetupFaceComponents(out MeshRenderer renderer)
        {
            GameObject faceObj = new GameObject("Face_Chunk");
            faceObj.transform.SetParent(transform, false);
            faceObj.transform.localPosition = Vector3.zero;

            renderer = faceObj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = settings.material;
            
            return faceObj.AddComponent<MeshFilter>();
        }

        private Mesh CreateBaseMesh(int resolution, out Vector3[] vertices, out int[] triangles)
        {
            Mesh mesh = new Mesh { name = "FaceMesh" };
            mesh.indexFormat = IndexFormat.UInt32;

            vertices = new Vector3[CalcNumVerts(resolution)];
            triangles = new int[3 * CalcTotalTris(resolution)];
            
            return mesh;
        }

        private FaceAxes CalculateFaceAxes(GeometryData.Face face, int resolution)
        {
            Vector3 vTop = _icosahedronData.CornerVertices[face.V1];
            Vector3 vLeft = _icosahedronData.CornerVertices[face.V2];
            Vector3 vRight = _icosahedronData.CornerVertices[face.V3];

            int numEdgeVerts = CalcNumEdgeVerts(resolution);
            Vector3 axisX = (vRight - vLeft).normalized;
            Vector3 faceNormal = (vTop + vLeft + vRight).normalized;
            Vector3 axisY = Vector3.Cross(faceNormal, axisX).normalized;

            return new FaceAxes {
                Origin = vLeft,
                AxisX = axisX,
                AxisY = axisY,
                StepX = Vector3.Distance(vLeft, vRight) / (numEdgeVerts - 1),
                StepY = Vector3.Distance(vTop, (vLeft + vRight) * 0.5f) / (numEdgeVerts - 1)
            };
        }

        private void PopulateArrays(int resolution, FaceAxes axes, Vector3[] vertices, int[] triangles)
        {
            int numEdgeVerts = CalcNumEdgeVerts(resolution);
            int v = 0;
            int t = 0;

            for (int y = 0; y < numEdgeVerts; y++)
            {
                int rowSize = numEdgeVerts - y;
                for (int x = 0; x < rowSize; x++)
                {
                    Vector3 point = axes.Origin + (x * axes.AxisX * axes.StepX) + 
                                    (y * axes.AxisY * axes.StepY) + 
                                    (0.5f * axes.AxisX * axes.StepX * y);
                    
                    vertices[v] = settings.isRound ? point.normalized * settings.radius : point;
                    
                    if (y < numEdgeVerts - 1 && x < rowSize - 1)
                    {
                        t = BuildTriangleIndices(v, rowSize, t, triangles, x);
                    }
                    v++;
                }
            }
        }

        private void FinalizeMesh(Mesh mesh, MeshFilter filter, Vector3[] vertices, int[] triangles)
        {
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = vertices; 
            mesh.RecalculateBounds();
            filter.mesh = mesh;
        }
        

        private int BuildTriangleIndices(int v, int rowSize, int t, int[] tris, int x)
        {
            tris[t++] = v;
            tris[t++] = v + 1;
            tris[t++] = v + rowSize;

            if (x > 0)
            {
                tris[t++] = v;
                tris[t++] = v + rowSize;
                tris[t++] = v + rowSize - 1;
            }
            return t;
        }

        private void ClearOldFaces()
        {
            while (transform.childCount > 0)
                DestroyImmediate(transform.GetChild(0).gameObject);
        }

        private int GetTargetResolution()
        {
            int calculatedRes = Mathf.RoundToInt(settings.radius * settings.resolutionDensityFactor);

            return Mathf.Clamp(calculatedRes, 1, settings.maxResolutionCap);
        }

        #region Math Boilerplate
        private int CalcNumEdgeVerts(int size) => (3 * size) - 2;
        private int CalcNumVerts(int size) { int n = CalcNumEdgeVerts(size); return (n * (n + 1)) / 2; }
        private int CalcTotalTris(int size) { int n = CalcNumEdgeVerts(size); return (n - 1) * (n - 1); }
        #endregion

        private void HandleSettingsUpdate()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => {
                if (this != null && gameObject != null) GeneratePlanet();
            };
            #endif
        }

        private void OnDisable() => settings.OnSettingsUpdated -= HandleSettingsUpdate;
    }
}*/
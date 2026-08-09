using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
public class HexagonSphereGenerator : MonoBehaviour
{
    [Range(2, 16)]
    public int frequency = 1;
    public Material planetMaterial;
    public bool IsRound = true;
    //constants
    private static readonly float Phi = (1f + Mathf.Sqrt(5f)) / 2f;
    private static readonly float OneOverPhi = 1f / Phi;
    
    private readonly Vector3[] _cornerVertices = {
        new Vector3(-1,  Phi,  0).normalized, new Vector3( 1,  Phi,  0).normalized,
        new Vector3(-1, -Phi,  0).normalized, new Vector3( 1, -Phi,  0).normalized,
        new Vector3( 0, -1,  Phi).normalized, new Vector3( 0,  1,  Phi).normalized,
        new Vector3( 0, -1, -Phi).normalized, new Vector3( 0,  1, -Phi).normalized,
        new Vector3( Phi,  0, -1).normalized, new Vector3( Phi,  0,  1).normalized,
        new Vector3(-Phi,  0, -1).normalized, new Vector3(-Phi,  0,  1).normalized
    };
    
    private struct Face {
        public int v1, v2, v3;
        public Face(int a, int b, int c) { v1 = a; v2 = b; v3 = c; }
    }

    private readonly Face[] _faces = {
        new Face(0, 11, 5), new Face(0, 5, 1), new Face(0, 1, 7), new Face(0, 7, 10), new Face(0, 10, 11),
        new Face(1, 5, 9), new Face(5, 11, 4), new Face(11, 10, 2), new Face(10, 7, 6), new Face(7, 1, 8),
        new Face(3, 9, 4), new Face(3, 4, 2), new Face(3, 2, 6), new Face(3, 6, 8), new Face(3, 8, 9),
        new Face(4, 9, 5), new Face(2, 4, 11), new Face(6, 2, 10), new Face(8, 6, 7), new Face(9, 8, 1)
    };
    
    private void OnValidate() {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => {
                if (this != null) GeneratePlanet();
            };
        #endif
    }

    private void GeneratePlanet()
    {
        while (transform.childCount > 0) {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
        
        foreach (Face face in _faces)
        {
            GenerateFace(face);
        }
    }
    
    void GenerateFace(Face face) {
        GameObject faceObj = new GameObject("Face");
        faceObj.transform.SetParent(transform, false);

        faceObj.transform.localPosition = Vector3.zero;
        faceObj.transform.localRotation = Quaternion.identity;
        faceObj.transform.localScale = Vector3.one;
        
        MeshFilter meshFilter = faceObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = faceObj.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = planetMaterial;

        Mesh mesh = new Mesh();
        
        Vector3[] vertices = new Vector3[CalcNumVerts(frequency)];
        int[] triangles = new int[3 * CalcTotalTris(frequency)]; //stores 3 index of vertices per triangle
        //int[] hexagons = new int[CalcNumFullHexagons(frequency)];
        int numEdgeVerts = CalcNumEdgeVerts(frequency);
        
        float faceEdgeLength = Vector3.Distance(_cornerVertices[face.v2], _cornerVertices[face.v3]);
        float faceHeight = Vector3.Distance(_cornerVertices[face.v1], (_cornerVertices[face.v2] + _cornerVertices[face.v3]) * 0.5f);
        
        float triangleEdgeLength = faceEdgeLength / (numEdgeVerts - 1);
        float triangleHeight = faceHeight / (numEdgeVerts - 1);
        
        // get top edge direction//
        Vector3 faceNormal = (_cornerVertices[face.v1] + _cornerVertices[face.v2] + _cornerVertices[face.v3]).normalized;
        Vector3 axisX = (_cornerVertices[face.v3] - _cornerVertices[face.v2]).normalized;
        Vector3 axisY = Vector3.Cross(faceNormal, axisX).normalized;

        int v = 0;
        int t = 0;
        
        for (int y = 0; y < numEdgeVerts; y++) //top to bottom
        {   
            int rowSize = numEdgeVerts - y;
            
            for (int x = 0; x < rowSize; x++) // left to right
            {

                Vector3 point = _cornerVertices[face.v2] + (x * axisX * triangleEdgeLength) +
                                (y * axisY * triangleHeight) + 
                                (0.5f * axisX * triangleEdgeLength * y); 
                
                vertices[v] = point;
                vertices[v] = IsRound ? point.normalized : point;
                
                if (y < numEdgeVerts - 1 && x < rowSize - 1)
                {
                    triangles[t++] = v;
                    triangles[t++] = v + 1;
                    triangles[t++] = v + rowSize;
                    
                    if (x > 0) //always do tris in pairs. first triangle of each row is alone
                    {

                        triangles[t++] = v;
                        triangles[t++] = v + rowSize;
                        triangles[t++] = v + rowSize - 1;
                    }
                }
                v++;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = vertices;
        meshFilter.mesh = mesh;
        mesh.RecalculateBounds(); //idk
    }


    private int CalcNumEdgeVerts(int size)
    {
        return ((3 * size) - 2);
    }
    
    private int CalcNumVerts(int size)
    {
        int n = CalcNumEdgeVerts(size);
        return (n * (n + 1)) / 2;
    }
    
    private int CalcNumFullHexagons(int size)
    {
        return (((3 * size * size) - (3 * size) + 2) / 2);
    }

    private int CalcNumHalfHexagons(int size)
    {
        return (3 * (size - 1));
    }

    private int CalcTotalTris(int size)
    {
        int n = CalcNumEdgeVerts(size);
        return (n - 1) * (n - 1);
    } 
}
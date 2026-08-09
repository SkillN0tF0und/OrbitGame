using UnityEngine;

[ExecuteAlways]
public class SphereGenerator : MonoBehaviour
{
    //number of vertices along one edge of the base cube
    [Range(2, 256)]
    public int resolution = 20;
    
    public Material planetMaterial;
    
    private Vector3[] _directions =
    {
        Vector3.up,
        Vector3.down, 
        Vector3.left,
        Vector3.right,
        Vector3.forward,
        Vector3.back
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
        
        foreach (Vector3 dir in _directions)
        {
            GenerateFace(dir);
        }
    }
    
    void GenerateFace(Vector3 normal) {
        GameObject faceObj = new GameObject("PlanetFace");
        faceObj.transform.parent = transform;

        MeshFilter meshFilter = faceObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = faceObj.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = planetMaterial;

        Mesh mesh = new Mesh();
        
        Vector3[] vertices = new Vector3[resolution * resolution];
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];

        // Find the side axes based on the normal
        //vector along one side of face
        Vector3 axisA = new Vector3(normal.y, normal.z, normal.x);
        //vector perpendicular to axisA
        Vector3 axisB = Vector3.Cross(normal, axisA);

        int v = 0;
        int t = 0;

        for (int y = 0; y < resolution; y++) {
            for (int x = 0; x < resolution; x++) {
                // Calculate position on a 1x1 unit square
                Vector2 percent = new Vector2(x, y) / (resolution - 1);
                
                // Map to Cube face
                Vector3 pointOnCube = normal + (percent.x - 0.5f) * 2 * axisA + (percent.y - 0.5f) * 2 * axisB;
                
                // Spherify (Simple normalization for now)
                vertices[v] = pointOnCube.normalized;

                if (x < resolution - 1 && y < resolution - 1) {
                    
                    //vertices need to be listed clockwise so the normals face to the outside
                    
                    //current Vertex
                    triangles[t] = v;
                    //top left Vertex
                    triangles[t + 1] = v + resolution + 1;
                    //bottom right Vertex
                    triangles[t + 2] = v + resolution;
                    
                    //bottom left Vertex
                    triangles[t + 3] = v;
                    //top right Vertex
                    triangles[t + 4] = v + 1;
                    //bottom right Vertex
                    triangles[t + 5] = v + resolution + 1;
                    t += 6;
                }
                v++;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = vertices;
        meshFilter.mesh = mesh;
    }
        
    
}

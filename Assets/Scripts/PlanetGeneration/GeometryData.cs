using UnityEngine;

namespace PlanetGeneration
{
    public static class GeometryData
    {
        public struct Face {
            public readonly int V1, V2, V3;
            public Face(int a, int b, int c) { V1 = a; V2 = b; V3 = c; }
        }
        
        private static readonly float Phi = (1f + Mathf.Sqrt(5f)) / 2f;
            
        public static readonly Vector3[] CornerVertices = {
            new Vector3(-1,  Phi,  0).normalized, new Vector3( 1,  Phi,  0).normalized,
            new Vector3(-1, -Phi,  0).normalized, new Vector3( 1, -Phi,  0).normalized,
            new Vector3( 0, -1,  Phi).normalized, new Vector3( 0,  1,  Phi).normalized,
            new Vector3( 0, -1, -Phi).normalized, new Vector3( 0,  1, -Phi).normalized,
            new Vector3( Phi,  0, -1).normalized, new Vector3( Phi,  0,  1).normalized,
            new Vector3(-Phi,  0, -1).normalized, new Vector3(-Phi,  0,  1).normalized
        };
            

        public static readonly Face[] Faces = {
            new Face(0, 11, 5), new Face(0, 5, 1), new Face(0, 1, 7), new Face(0, 7, 10), new Face(0, 10, 11),
            new Face(1, 5, 9), new Face(5, 11, 4), new Face(11, 10, 2), new Face(10, 7, 6), new Face(7, 1, 8),
            new Face(3, 9, 4), new Face(3, 4, 2), new Face(3, 2, 6), new Face(3, 6, 8), new Face(3, 8, 9),
            new Face(4, 9, 5), new Face(2, 4, 11), new Face(6, 2, 10), new Face(8, 6, 7), new Face(9, 8, 1)
        };
            
    }
    
}

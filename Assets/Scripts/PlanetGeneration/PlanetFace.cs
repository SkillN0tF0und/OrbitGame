using UnityEngine;

namespace PlanetGeneration
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PlanetFace : MonoBehaviour
    {
        private ChunkGeometry _geometry;
        private int _level;
        private PlanetGenerator _manager;

        private PlanetFace[] _children;
        private bool _isSplit;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        public void Initialize(ChunkGeometry geometry, int level, PlanetGenerator manager)
        {
            _geometry = geometry;
            _level = level;
            _manager = manager;

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.sharedMaterial = _manager.Settings.material;

            GenerateMesh();
        }

        public void EvaluateLOD(Vector3 localPlayerPos)
        {
            int targetDepth = _manager.Settings.maxRecursionDepth;

            if (_manager.Settings.disableLOD)
            {
                targetDepth = _manager.Settings.fixedLODLevel;
            }
            else
            {
                float distance = GetDistanceToFace(localPlayerPos);

                if (_manager.Settings.lodDistances != null)
                {
                    for (int i = 0; i < _manager.Settings.lodDistances.Length; i++)
                    {
                        if (distance > _manager.Settings.lodDistances[i])
                        {
                            targetDepth--;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            targetDepth = Mathf.Max(0, targetDepth);

            if (_level < targetDepth)
            {
                if (!_isSplit) Split();

                foreach (var child in _children)
                {
                    child.EvaluateLOD(localPlayerPos);
                }
            }
            else
            {
                if (_isSplit) Merge();
            }
        }

        private void Split()
        {
            _isSplit = true;
            ToggleMesh(false);

            Vector3 a = Vector3.Slerp(_geometry.V1, _geometry.V2, 0.5f).normalized * _manager.Settings.radius;
            Vector3 b = Vector3.Slerp(_geometry.V2, _geometry.V3, 0.5f).normalized * _manager.Settings.radius;
            Vector3 c = Vector3.Slerp(_geometry.V3, _geometry.V1, 0.5f).normalized * _manager.Settings.radius;

            _children = new PlanetFace[4];

            _children[0] = CreateChild(new ChunkGeometry(_geometry.V1, a, c), "Child_Top");
            _children[1] = CreateChild(new ChunkGeometry(a, _geometry.V2, b), "Child_Left");
            _children[2] = CreateChild(new ChunkGeometry(c, b, _geometry.V3), "Child_Right");
            _children[3] = CreateChild(new ChunkGeometry(a, b, c), "Child_Center");
        }

        private PlanetFace CreateChild(ChunkGeometry geo, string debugName)
        {
            GameObject obj = new GameObject(debugName);
            obj.transform.SetParent(transform, false);

            PlanetFace child = obj.AddComponent<PlanetFace>();
            child.Initialize(geo, _level + 1, _manager);
            return child;
        }

        private void Merge()
        {
            _isSplit = false;

            if (_children != null)
            {
                foreach (var child in _children)
                {
                    if (child == null) continue;
                    child.Merge();
                    DestroyImmediate(child.gameObject);
                }
                _children = null;
            }
            ToggleMesh(true);
            UpdateCollision(true);
        }

        private void GenerateMesh()
        {
            MeshSettings meshSettings = new MeshSettings(_manager.Settings.chunkResolution, _manager.ShapeGenerator);

            // Pass the GPU requirements from the manager into the MeshGenerator
            _meshFilter.sharedMesh = MeshGenerator.CreateChunkMesh(
                _geometry,
                meshSettings,
                _manager.ComputeShader,
                _manager.GPUBiomes,
                _manager.GPUBiomePoints,
                _manager.Settings
            );
        }

        private void ToggleMesh(bool isVisible)
        {
            _meshRenderer.enabled = isVisible;
        }

        private float GetDistanceToFace(Vector3 playerPos)
        {
            float d1 = Vector3.Distance(playerPos, _geometry.V1);
            float d2 = Vector3.Distance(playerPos, _geometry.V2);
            float d3 = Vector3.Distance(playerPos, _geometry.V3);
            return Mathf.Min(d1, Mathf.Min(d2, d3));
        }

        private void OnDestroy()
        {
            CleanupMesh();
        }

        private void CleanupMesh()
        {
            if (_meshFilter != null && _meshFilter.sharedMesh != null)
            {
                if (Application.isPlaying) Destroy(_meshFilter.sharedMesh);
                else DestroyImmediate(_meshFilter.sharedMesh);
            }
        }

        private void UpdateCollision(bool isLeaf)
        {
            if (!isLeaf)
            {
                if (TryGetComponent<MeshCollider>(out var oldCollider))
                    DestroyImmediate(oldCollider);
                return;
            }

            float distToPlayer = GetDistanceToFace(_manager.PlayerTransform.position);

            if (distToPlayer < _manager.Settings.collisionDistance)
            {
                MeshCollider collider = GetComponent<MeshCollider>();
                if (collider == null) collider = gameObject.AddComponent<MeshCollider>();

                collider.sharedMesh = _meshFilter.sharedMesh;
            }
            else
            {
                if (TryGetComponent<MeshCollider>(out var oldCollider))
                    DestroyImmediate(oldCollider);
            }
        }
    }
}
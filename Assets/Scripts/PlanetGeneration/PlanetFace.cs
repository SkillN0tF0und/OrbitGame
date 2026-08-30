using UnityEngine;

namespace PlanetGeneration
{
    public enum FaceState { Generating, Completed }

    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PlanetFace : MonoBehaviour
    {
        private ChunkGeometry _geometry;
        private int _level;
        private PlanetGenerator _manager;
        private PlanetFace _parentFace;

        private PlanetFace[] _children;
        private bool _isSplit;
        private bool _isDestroyed;
        private int _activeChildrenCount;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        
        // MaterialPropertyBlock allows to override shader variables on a single renderer without instantiating new copy of Material
        private MaterialPropertyBlock _propBlock;

        public FaceState State { get; private set; }

        public void Initialize(ChunkGeometry geometry, int level, PlanetGenerator manager, PlanetFace parent = null)
        {
            _geometry = geometry;
            _level = level;
            _manager = manager;
            _parentFace = parent;
            _isDestroyed = false;
            State = FaceState.Generating;

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
    
            // Evaluate which material to apply
            _meshRenderer.sharedMaterial = _manager.Settings.showWireframe ? 
                _manager.Settings.wireframeMaterial : _manager.Settings.material;

            BindShaderVariables();
            GenerateMesh();
        }

        private void BindShaderVariables()
        {
            if (_manager == null || _manager.BiomeBuffer == null || _meshRenderer == null) return;
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            _meshRenderer.GetPropertyBlock(_propBlock);
            
            
            // push buffers to meshrenderers material for use  in shader
            _propBlock.SetBuffer("_BiomeSurfaceBuffer", _manager.BiomeBuffer);
            _propBlock.SetFloat("_ColorBlendSharpness", _manager.Settings.colorBlendSharpness);
            _propBlock.SetFloat("_GlobalSteepnessThreshold", _manager.Settings.globalSteepnessThreshold);
            _propBlock.SetBuffer("_BiomePoints", _manager.BiomePointBuffer);
            _propBlock.SetInt("_BiomePointCount", _manager.GPUBiomePoints.Length);
            _propBlock.SetFloat("_Radius", _manager.Settings.radius);
            _propBlock.SetFloat("_BiomeBlendDistance", _manager.Settings.biomeBlendDistance);
            _propBlock.SetInt("_EnableWarp", _manager.Settings.enableWarp ? 1 : 0);
            _propBlock.SetFloat("_WarpAmplitude", _manager.Settings.warpAmplitude);
            _propBlock.SetFloat("_WarpFrequency", _manager.Settings.warpFrequency);
            _propBlock.SetInt("_MacroSeed", _manager.Settings.planetSeed);

            _meshRenderer.SetPropertyBlock(_propBlock);
        }

        public void EvaluateLOD(Vector3 localPlayerPos)
        {
            if (State == FaceState.Generating) return;

            BindShaderVariables(); // for editor use
            
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
                        if (distance > _manager.Settings.lodDistances[i]) targetDepth--;
                        else break;
                    }
                }
            }

            targetDepth = Mathf.Max(0, targetDepth);

            if (_level < targetDepth)
            {
                if (!_isSplit) Split();

                foreach (var child in _children)
                {
                    if (child != null) child.EvaluateLOD(localPlayerPos);
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
            _activeChildrenCount = 0;
            
            //Slerp so it works for the sphere
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
            child.Initialize(geo, _level + 1, _manager, this);
            return child;
        }

        private void Merge()
        {
            _isSplit = false;
            _activeChildrenCount = 0;

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
        }

        private void GenerateMesh()
        {
            MeshSettings meshSettings = new MeshSettings(_manager.Settings.chunkResolution, _manager.ShapeGenerator);
            
            
            //start mesh generation
            MeshGenerator.CreateChunkMeshAsync(
                _geometry,
                meshSettings,
                _manager.planetCompute,
                _manager.BiomeBuffer,
                _manager.GPUBiomePoints,
                _manager.Settings,
                OnMeshCompleted
            );
        }

        private void OnMeshCompleted(Mesh generatedMesh)
        {
            if (_isDestroyed || this == null)
            {
                if (Application.isPlaying) Destroy(generatedMesh);
                else DestroyImmediate(generatedMesh);
                return;
            }

            _meshFilter.sharedMesh = generatedMesh;
            State = FaceState.Completed;

            BindShaderVariables();

            if (_parentFace != null)
            {
                _parentFace.OnChildCompleted();
            }
        }

        public void OnChildCompleted()
        {
            _activeChildrenCount++;

            if (_activeChildrenCount == 4)
            {
                ToggleMesh(false);
            }
        }

        private void ToggleMesh(bool isVisible)
        {
            if (_meshRenderer != null) _meshRenderer.enabled = isVisible;
        }

        private float GetDistanceToFace(Vector3 localPlayerPos)
        {
            float d1 = Vector3.Distance(localPlayerPos, _geometry.V1);
            float d2 = Vector3.Distance(localPlayerPos, _geometry.V2);
            float d3 = Vector3.Distance(localPlayerPos, _geometry.V3);
            return Mathf.Min(d1, Mathf.Min(d2, d3));
        }

        private void OnDestroy()
        {
            _isDestroyed = true;
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

       
        
    }
}
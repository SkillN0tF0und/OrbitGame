using UnityEngine;
using System.Collections.Generic;

namespace PlanetGeneration
{
    [ExecuteAlways]
    public class PlanetGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlanetSettings settings;
        [SerializeField] private Transform playerTransform;

        [Header("GPU Resources")]
        public ComputeShader planetCompute;

        private ShapeGenerator _shapeGenerator;
        private List<PlanetFace> _rootFaces = new List<PlanetFace>();

        private GameObject _oceanObject;
        private Material _oceanMaterialInstance;

        public PlanetSettings Settings => settings;
        public ShapeGenerator ShapeGenerator => _shapeGenerator;

        // GPU Buffers to transfer data to the gpu
        public ComputeBuffer BiomeBuffer { get; private set; }
        public ComputeBuffer BiomePointBuffer { get; private set; }
        public GPUBiomePoint[] GPUBiomePoints { get; private set; }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
            ClearRoots();
            ReleaseBuffers();
        }

        private void Start()
        {
            Startup();
        }

        private void Update()
        {
            UpdateOceanMaterial();

            if (playerTransform == null || _rootFaces.Count == 0) return;
            
            Vector3 localPlayerPos = transform.InverseTransformPoint(playerTransform.position);

            foreach (var face in _rootFaces)
            {
                if (face != null) face.EvaluateLOD(localPlayerPos);
            }
        }

        private void Startup()
        {
            if (settings == null) return;

            InitializeLogic();
            InitializeBuffers();
            GenerateRoots();
            InitializeOcean();
        }

        private void InitializeLogic()
        {
            _shapeGenerator = new ShapeGenerator(settings);
            GPUBiomePoints = _shapeGenerator.GetBiomePoints();
        }

        
        // convert data from scriptable Objects to ComputeBuffers
        private void InitializeBuffers()
        {
            ReleaseBuffers();
            
            //skip if there are no assigned biomes in PlanetSO
            if (settings.biomePlacements == null || settings.biomePlacements.Length == 0) return;

            PlanetBiome[] gpuBiomes = new PlanetBiome[settings.biomePlacements.Length];
            for (int i = 0; i < settings.biomePlacements.Length; i++)
            {
                var placement = settings.biomePlacements[i];
                if (placement.biome != null)
                {
                    gpuBiomes[i] = placement.biome.ToGPUBiome(placement.startThreshold);
                }
            }
            
            
            //allocate VRAM
            BiomeBuffer = new ComputeBuffer(gpuBiomes.Length, PlanetBiome.Size);
            BiomeBuffer.SetData(gpuBiomes);

            BiomePointBuffer = new ComputeBuffer(GPUBiomePoints.Length, GPUBiomePoint.Stride);
            BiomePointBuffer.SetData(GPUBiomePoints);
        }
        
        //create the root faces of the icosahedron
        private void GenerateRoots()
        {
            ClearRoots();

            foreach (var face in GeometryData.Faces)
            {
                _rootFaces.Add(CreateRootFace(face, GeometryData.CornerVertices));
            }
        }

        private PlanetFace CreateRootFace(GeometryData.Face face, Vector3[] corners)
        {
            GameObject obj = new GameObject($"Root_Face_{_rootFaces.Count}");
            obj.transform.SetParent(transform, false);

            Vector3 v1 = corners[face.V1].normalized * settings.radius;
            Vector3 v2 = corners[face.V2].normalized * settings.radius;
            Vector3 v3 = corners[face.V3].normalized * settings.radius;

            ChunkGeometry geo = new ChunkGeometry(v1, v2, v3);

            PlanetFace pf = obj.AddComponent<PlanetFace>();
            pf.Initialize(geo, 0, this);

            return pf;
        }

        private void InitializeOcean()
        {
            if (settings.oceanMaterial == null) return;

            if (_oceanObject == null)
            {
                _oceanObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _oceanObject.name = "Ocean";
                _oceanObject.transform.SetParent(transform, false);
                _oceanObject.transform.localPosition = Vector3.zero;

                DestroyImmediate(_oceanObject.GetComponent<Collider>());
            }

            _oceanObject.transform.localScale = Vector3.one * (settings.radius * 2f);

            if (_oceanMaterialInstance == null)
            {
                _oceanMaterialInstance = new Material(settings.oceanMaterial);
            }

            _oceanObject.GetComponent<MeshRenderer>().sharedMaterial = _oceanMaterialInstance;
            UpdateOceanMaterial();
        }
        
        
        //push variables to material
        private void UpdateOceanMaterial()
        {
            if (_oceanMaterialInstance == null || settings == null) return;

            _oceanMaterialInstance.SetColor("_ShallowColor", settings.oceanColorShallow);
            _oceanMaterialInstance.SetColor("_DeepColor", settings.oceanColorDeep);
            _oceanMaterialInstance.SetFloat("_DepthMultiplier", settings.oceanDepthMultiplier);
            _oceanMaterialInstance.SetFloat("_AlphaMultiplier", settings.oceanAlphaMultiplier);

            if (settings.waveNormalA != null) _oceanMaterialInstance.SetTexture("_WaveNormalA", settings.waveNormalA);
            if (settings.waveNormalB != null) _oceanMaterialInstance.SetTexture("_WaveNormalB", settings.waveNormalB);

            _oceanMaterialInstance.SetFloat("_WaveStrength", settings.waveStrength);
            _oceanMaterialInstance.SetFloat("_WaveScale", settings.waveScale);
            _oceanMaterialInstance.SetFloat("_WaveSpeed", settings.waveSpeed);
            _oceanMaterialInstance.SetFloat("_Smoothness", settings.oceanSmoothness);
        }

        private void ClearRoots()
        {
            while (transform.childCount > 0) DestroyImmediate(transform.GetChild(0).gameObject);
            _rootFaces.Clear();
            _oceanObject = null;
        }
        
        private void ReleaseBuffers()
        {
            BiomeBuffer?.Release();
            BiomeBuffer = null;
            BiomePointBuffer?.Release();
            BiomePointBuffer = null;
        }

        private void SubscribeToEvents()
        {
            if (settings == null) return;

            settings.OnSettingsUpdated -= HandleSettingsUpdated;
            settings.OnSettingsUpdated += HandleSettingsUpdated;

            if (settings.biomePlacements != null)
            {
                foreach (var placement in settings.biomePlacements)
                {
                    if (placement.biome != null)
                    {
                        placement.biome.OnBiomeUpdated -= HandleSettingsUpdated;
                        placement.biome.OnBiomeUpdated += HandleSettingsUpdated;
                    }
                }
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (settings == null) return;

            settings.OnSettingsUpdated -= HandleSettingsUpdated;

            if (settings.biomePlacements != null)
            {
                foreach (var placement in settings.biomePlacements)
                {
                    if (placement.biome != null)
                    {
                        placement.biome.OnBiomeUpdated -= HandleSettingsUpdated;
                    }
                }
            }
        }

        private void HandleSettingsUpdated()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => {
                if (this != null && gameObject != null)
                {
                    UnsubscribeFromEvents();
                    SubscribeToEvents();
                    Startup();
                }
            };
#else
            UnsubscribeFromEvents();
            SubscribeToEvents();
            Startup();
#endif
        }
    }
}
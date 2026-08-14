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

        // Public Accessors
        public PlanetSettings Settings => settings;
        public ShapeGenerator ShapeGenerator => _shapeGenerator;
        public Transform PlayerTransform => playerTransform;

        // GPU Buffers
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
            if (playerTransform == null || _rootFaces.Count == 0) return;

            // Calculate local player position once per frame to pass down the LOD tree
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
        }

        private void InitializeLogic()
        {
            _shapeGenerator = new ShapeGenerator(settings);
            GPUBiomePoints = _shapeGenerator.GetBiomePoints();
        }

        private void InitializeBuffers()
        {
            ReleaseBuffers();

            if (settings.biomePlacements == null || settings.biomePlacements.Length == 0) return;

            GPUBiome[] gpuBiomes = new GPUBiome[settings.biomePlacements.Length];
            for (int i = 0; i < settings.biomePlacements.Length; i++)
            {
                var placement = settings.biomePlacements[i];
                if (placement.biome != null)
                {
                    // Pass the threshold from the tuple into the builder
                    gpuBiomes[i] = placement.biome.ToGPUBiome(placement.startThreshold);
                }
            }

            BiomeBuffer = new ComputeBuffer(gpuBiomes.Length, GPUBiome.Size);
            BiomeBuffer.SetData(gpuBiomes);

            BiomePointBuffer = new ComputeBuffer(GPUBiomePoints.Length, GPUBiomePoint.Stride);
            BiomePointBuffer.SetData(GPUBiomePoints);
        }

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

        private void ClearRoots()
        {
            while (transform.childCount > 0) DestroyImmediate(transform.GetChild(0).gameObject);
            _rootFaces.Clear();
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
                        // Safely prevent double-subscriptions
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
                    // Refresh subscriptions in case the array of biomes was modified
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
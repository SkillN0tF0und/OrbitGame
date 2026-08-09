using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace PlanetGeneration
{
    [ExecuteAlways]
    public class PlanetGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlanetSettings settings;
        [SerializeField] private Transform playerTransform;

        [Header("Compute Shader")]
        [SerializeField] private ComputeShader computeShader;

        private ShapeGenerator _shapeGenerator;
        private List<PlanetFace> _rootFaces = new List<PlanetFace>();

        public PlanetSettings Settings => settings;
        public ShapeGenerator ShapeGenerator => _shapeGenerator;
        public Transform PlayerTransform => playerTransform;

        // GPU Data Properties
        public ComputeShader ComputeShader => computeShader;
        public GPUBiome[] GPUBiomes { get; private set; }
        public GPUBiomePoint[] GPUBiomePoints { get; private set; }

        private void OnEnable()
        {
            if (settings != null) settings.OnSettingsUpdated += HandleSettingsUpdated;
        }

        private void OnDisable()
        {
            if (settings != null) settings.OnSettingsUpdated -= HandleSettingsUpdated;
            ClearRoots();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => {
                if (this != null && gameObject != null) Startup();
            };
#endif
        }

        private void Start() => Startup();

        private void Update()
        {
            if (playerTransform == null || _rootFaces.Count == 0) return;

            // Convert player to local space, then pass THAT to the faces.
            Vector3 localPlayerPos = transform.InverseTransformPoint(playerTransform.position);

            foreach (var face in _rootFaces)
            {
                if (face != null)
                {
                    face.EvaluateLOD(localPlayerPos);
                }
            }
        }

        private void Startup()
        {
            // Halt if the shader is not assigned in the inspector
            if (settings == null || computeShader == null) return;

            _shapeGenerator = new ShapeGenerator(settings);

            InitializeGPUData();

            GenerateRoots();
        }

        private void InitializeGPUData()
        {
            // 1. Flatten Biome Settings
            int biomeCount = settings.biomes?.Length ?? 0;
            GPUBiomes = new GPUBiome[biomeCount];
            for (int i = 0; i < biomeCount; i++)
            {
                GPUBiomes[i] = new GPUBiome(settings.biomes[i]);
            }

            // 2. Flatten Biome Points
            BiomePoint[] cpuPoints = _shapeGenerator.GetBiomePoints();
            GPUBiomePoints = new GPUBiomePoint[cpuPoints.Length];
            for (int i = 0; i < cpuPoints.Length; i++)
            {
                GPUBiomePoints[i] = new GPUBiomePoint(cpuPoints[i].Position, cpuPoints[i].BiomeIndex);
            }
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
            while (transform.childCount > 0)
            {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }
            _rootFaces.Clear();
        }

        private void HandleSettingsUpdated()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => {
                if (this != null && gameObject != null) Startup();
            };
#else
            Startup();
#endif
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using OrbitGame.Core;

namespace OrbitGame.OrbitMechanics
{
    public class SimulationManager : MonoBehaviour
    {
        public static SimulationManager Instance { get; private set; }

        [Header("Time & Tick Settings")]
        public double TimeScale = 1.0;
        public float TicksPerSecond = 10f;

        [Header("Universe Origin")]
        [Tooltip("The body that remains locked at (0,0,0).")]
        public CelestialBody activeFocusBody;

        public double SimulationTime { get; private set; }
        public CelestialBody[] AllBodies { get; private set; }
        public Vector3d OriginOffset { get; private set; }

        private List<CelestialBody> rootBodies = new List<CelestialBody>();

        private float tickInterval;
        private float timeAccumulator = 0f;

        void Awake()
        {
            if (Instance == null) Instance = this;

            tickInterval = TicksPerSecond > 0f ? 1f / TicksPerSecond : 0.02f;

            // FindObjectsByType is the updated standard for Unity 2023+
            AllBodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
            BuildHierarchy();
        }

        private void BuildHierarchy()
        {
            foreach (var body in AllBodies) body.InitializeHierarchy();

            foreach (var body in AllBodies)
            {
                if (body.parentBody == null) rootBodies.Add(body);
            }
        }

        void Update()
        {
            float frameTime = Mathf.Min(Time.deltaTime, 0.25f);
            timeAccumulator += frameTime;

            while (timeAccumulator >= tickInterval)
            {
                timeAccumulator -= tickInterval;
                SimulationTime += tickInterval * TimeScale;

                foreach (var root in rootBodies) root.UpdateSimulationPosition(SimulationTime);
            }

            double renderTime = SimulationTime + (timeAccumulator * TimeScale);

            // 1. Calculate the Floating Origin offset
            if (activeFocusBody != null)
            {
                OriginOffset = activeFocusBody.CalculateExactStateAtTime(renderTime).absolutePosition;
            }
            else
            {
                OriginOffset = Vector3d.zero;
            }

            // 2. Apply positioning and rotation to all bodies
            foreach (var body in AllBodies)
            {
                // Position relative to origin
                Vector3d exactPos = body.CalculateExactStateAtTime(renderTime).absolutePosition;
                body.transform.position = (Vector3)(exactPos - OriginOffset);

                // Calculate Rotation
                if (body.bodySettings != null && body.bodySettings.rotationPeriod != 0)
                {
                    double rotationAngle = (renderTime / body.bodySettings.rotationPeriod) * 360d;

                    Quaternion tilt = Quaternion.Euler(body.bodySettings.axialTilt);
                    Quaternion spin = Quaternion.AngleAxis((float)rotationAngle, Vector3.up);

                    body.transform.rotation = tilt * spin;
                }

                // Push data to materials
                body.UpdateShaderVariables();
            }
        }

        public void SetFocusBody(CelestialBody body)
        {
            activeFocusBody = body;
        }
    }
}
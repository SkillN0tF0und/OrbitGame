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
    
        public double SimulationTime { get; private set; }
        public CelestialBody[] AllBodies { get; private set; }

        private List<CelestialBody> rootBodies = new List<CelestialBody>();
    
        private float tickInterval;
        private float timeAccumulator = 0f;

        void Awake() 
        {
            if (Instance == null) Instance = this;
            
            tickInterval = TicksPerSecond > 0f ? 1f / TicksPerSecond : 0.02f; 
            
            AllBodies = FindObjectsByType<CelestialBody>();
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
    
            foreach (var body in AllBodies) 
            {
                // Updated line to pull absolutePosition from the new BodyState
                Vector3d exactPos = body.CalculateExactStateAtTime(renderTime).absolutePosition;
                body.transform.position = (Vector3)exactPos;
            }
        }
    }
}
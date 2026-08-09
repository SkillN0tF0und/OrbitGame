using UnityEngine;
using OrbitGame.Core;

namespace OrbitGame.OrbitMechanics
{
    [RequireComponent(typeof(LineRenderer))]
    [ExecuteAlways]
    public class OrbitVisualizer : MonoBehaviour 
    {
        public CelestialBody body;
        
        [Header("Visualization Settings")]
        public int lineResolution = 100;
        public bool drawFullOrbit = true;
        public double trajectoryTime = 1000d;

        [Header("Line Renderer Settings")]
        public float lineStartWidth = 0.5f;
        public float lineEndWidth = 0.5f;
        
        private LineRenderer _lineRenderer;
        
        void OnEnable() 
        {
            _lineRenderer = GetComponent<LineRenderer>();
            if (body == null) body = GetComponent<CelestialBody>();
            
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.startWidth = lineStartWidth;
            _lineRenderer.endWidth = lineEndWidth;
        }
        
        void Update() 
        {
            if (!Application.isPlaying) DrawPredictedPath();
        }

        void LateUpdate() 
        {
            if (Application.isPlaying) DrawPredictedPath();
        }

        private void DrawPredictedPath() 
        {
            if (_lineRenderer == null) _lineRenderer = GetComponent<LineRenderer>();
            if (body == null) body = GetComponent<CelestialBody>();

            if (body == null || body.parentBody == null) 
            {
                if (_lineRenderer != null) _lineRenderer.positionCount = 0;
                return;
            }

            double period = OrbitalMathUtility.CalculateOrbitalPeriod(body.orbitElements.orbitDistance, body.parentBody.properties.mass);
            
            if (period <= 0) 
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            _lineRenderer.positionCount = lineResolution;
            
            double startTime = 0d;
            if (Application.isPlaying && SimulationManager.Instance != null) 
            {
                startTime = SimulationManager.Instance.SimulationTime;
            }

            double timeStep = drawFullOrbit ? (period / (lineResolution - 1)) : (trajectoryTime / (lineResolution - 1));
            if (drawFullOrbit) startTime = 0d;

            for (int i = 0; i < lineResolution; i++) 
            {
                double evalTime = startTime + (i * timeStep);
                
                BodyState parentState = body.parentBody.CalculateExactStateAtTime(evalTime);
                OrbitalState state = OrbitalMathUtility.CalculateOrbitalState(body.orbitElements, evalTime, body.parentBody.properties.mass);
                
                Vector3d localPos = state.localPosition;

                if (body.alignment == OrbitAlignment.ParentAligned) 
                {
                    localPos = parentState.localFrame.TransformDirection(localPos);
                }

                Vector3 worldPos = (Vector3)(parentState.absolutePosition + localPos);
                _lineRenderer.SetPosition(i, worldPos);
            }
        }
    }
}
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

            if (body == null || body.parentBody == null || body.bodySettings == null)
            {
                if (_lineRenderer != null) _lineRenderer.positionCount = 0;
                return;
            }

            _lineRenderer.positionCount = lineResolution;

            double currentTime = Application.isPlaying && SimulationManager.Instance != null
                ? SimulationManager.Instance.SimulationTime
                : 0d;

            BodyState parentState = body.parentBody.CalculateExactStateAtTime(currentTime);

            // Fetch the origin offset to draw lines correctly in the floating universe
            Vector3d originOffset = Application.isPlaying && SimulationManager.Instance != null
                ? SimulationManager.Instance.OriginOffset
                : Vector3d.zero;

            if (drawFullOrbit)
            {
                // GEOMETRIC METHOD: Perfectly even vertices around the ellipse, unaffected by time distortion
                double angleStep = (2.0d * Mathd.PI) / (lineResolution - 1);

                for (int i = 0; i < lineResolution; i++)
                {
                    double trueAnomaly = i * angleStep;

                    OrbitalState state = OrbitalMathUtility.CalculateOrbitalStateByAngle(body.bodySettings.orbit, trueAnomaly);
                    Vector3d localPos = state.localPosition;

                    if (body.bodySettings.alignment == OrbitAlignment.ParentAligned)
                        localPos = parentState.localFrame.TransformDirection(localPos);

                    Vector3d worldPos = (parentState.absolutePosition + localPos) - originOffset;
                    _lineRenderer.SetPosition(i, (Vector3)worldPos);
                }
            }
            else
            {
                // TIME PREDICTION METHOD: Calculates partial trajectory over a set duration
                double timeStep = trajectoryTime / (lineResolution - 1);

                for (int i = 0; i < lineResolution; i++)
                {
                    double evalTime = currentTime + (i * timeStep);

                    OrbitalState state = OrbitalMathUtility.CalculateOrbitalState(body.bodySettings.orbit, evalTime, body.parentBody.bodySettings.mass);
                    Vector3d localPos = state.localPosition;

                    if (body.bodySettings.alignment == OrbitAlignment.ParentAligned)
                        localPos = parentState.localFrame.TransformDirection(localPos);

                    Vector3d worldPos = (parentState.absolutePosition + localPos) - originOffset;
                    _lineRenderer.SetPosition(i, (Vector3)worldPos);
                }
            }
        }
    }
}
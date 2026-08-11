using System.Collections.Generic;
using UnityEngine;
using OrbitGame.Core;

namespace OrbitGame.OrbitMechanics
{
    [ExecuteAlways]
    public class CelestialBody : MonoBehaviour
    {
        public CelestialBody parentBody;

        [Header("Physics Settings")]
        public CelestialBodySettings bodySettings; // Decoupled physics data

        public Vector3d SimulationPosition { get; private set; }
        public Vector3d CurrentVelocity { get; private set; }

        [HideInInspector]
        public List<CelestialBody> orbitingBodies = new List<CelestialBody>();

        private MaterialPropertyBlock _shaderPropertyBlock;

        public void InitializeHierarchy()
        {
            if (parentBody != null && !parentBody.orbitingBodies.Contains(this))
            {
                parentBody.orbitingBodies.Add(this);
            }
            _shaderPropertyBlock = new MaterialPropertyBlock();
        }

        public void UpdateSimulationPosition(double currentTime)
        {
            SimulationPosition = CalculateExactStateAtTime(currentTime).absolutePosition;

            Vector3d futurePos = CalculateExactStateAtTime(currentTime + 0.01d).absolutePosition;
            CurrentVelocity = (futurePos - SimulationPosition) / 0.01d;

            foreach (var child in orbitingBodies)
            {
                child.UpdateSimulationPosition(currentTime);
            }
        }

        public BodyState CalculateExactStateAtTime(double time)
        {
            if (parentBody == null || bodySettings == null)
            {
                return new BodyState { absolutePosition = Vector3d.zero, localFrame = CoordinateFrame.Identity };
            }

            BodyState parentState = parentBody.CalculateExactStateAtTime(time);
            OrbitalState state = OrbitalMathUtility.CalculateOrbitalState(bodySettings.orbit, time, parentBody.bodySettings.mass);

            Vector3d alignedLocalPos = state.localPosition;
            Vector3d alignedNormal = OrbitalMathUtility.CalculateOrbitalNormal(bodySettings.orbit);

            if (bodySettings.alignment == OrbitAlignment.ParentAligned)
            {
                alignedLocalPos = parentState.localFrame.TransformDirection(alignedLocalPos);
                alignedNormal = parentState.localFrame.TransformDirection(alignedNormal);
            }

            Vector3d absolutePos = parentState.absolutePosition + alignedLocalPos;

            CoordinateFrame myFrame = new CoordinateFrame();
            if (alignedLocalPos.sqrMagnitude > 0.00001d)
            {
                myFrame.forward = (parentState.absolutePosition - absolutePos).normalized;
                myFrame.up = alignedNormal;
                myFrame.right = Vector3d.Cross(myFrame.up, myFrame.forward).normalized;
                myFrame.up = Vector3d.Cross(myFrame.forward, myFrame.right).normalized;
            }
            else
            {
                myFrame = parentState.localFrame;
            }

            return new BodyState { absolutePosition = absolutePos, localFrame = myFrame };
        }

        public void UpdateShaderVariables()
        {
            if (_shaderPropertyBlock == null) _shaderPropertyBlock = new MaterialPropertyBlock();

            var renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(_shaderPropertyBlock);

                _shaderPropertyBlock.SetVector("_PlanetVelocity", (Vector3)CurrentVelocity);
                _shaderPropertyBlock.SetVector("_PlanetAxis", transform.up);

                r.SetPropertyBlock(_shaderPropertyBlock);
            }
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                transform.position = (Vector3)CalculateExactStateAtTime(0d).absolutePosition;
            }
        }
    }
}
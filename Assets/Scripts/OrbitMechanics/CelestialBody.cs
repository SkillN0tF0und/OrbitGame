using System.Collections.Generic;
using UnityEngine;
using OrbitGame.Core;

namespace OrbitGame.OrbitMechanics
{
    [ExecuteAlways]
    public class CelestialBody : MonoBehaviour 
    {
        public CelestialBody parentBody; 
        public CelestialProperties properties;
        public OrbitalElements orbitElements;
        
        [Header("Orbital Behavior")]
        public OrbitAlignment alignment = OrbitAlignment.WorldSpace;
        
        public Vector3d SimulationPosition { get; private set; } 
        
        [HideInInspector] 
        public List<CelestialBody> orbitingBodies = new List<CelestialBody>();

        public void InitializeHierarchy() 
        {
            if (parentBody != null && !parentBody.orbitingBodies.Contains(this))
            {
                parentBody.orbitingBodies.Add(this);
            }
        }
        
        public void UpdateSimulationPosition(double currentTime) 
        {
            SimulationPosition = CalculateExactStateAtTime(currentTime).absolutePosition;
            
            foreach (var child in orbitingBodies) 
            {
                child.UpdateSimulationPosition(currentTime);
            }
        }

        public BodyState CalculateExactStateAtTime(double time) 
        {
            if (parentBody == null) 
            {
                return new BodyState { absolutePosition = Vector3d.zero, localFrame = CoordinateFrame.Identity };
            }

            BodyState parentState = parentBody.CalculateExactStateAtTime(time);
            OrbitalState state = OrbitalMathUtility.CalculateOrbitalState(orbitElements, time, parentBody.properties.mass);
            
            Vector3d alignedLocalPos = state.localPosition;
            Vector3d alignedNormal = OrbitalMathUtility.CalculateOrbitalNormal(orbitElements);

            if (alignment == OrbitAlignment.ParentAligned) 
            {
                alignedLocalPos = parentState.localFrame.TransformDirection(alignedLocalPos);
                alignedNormal = parentState.localFrame.TransformDirection(alignedNormal);
            }

            Vector3d absolutePos = parentState.absolutePosition + alignedLocalPos;

            CoordinateFrame myFrame = new CoordinateFrame();
            if (alignedLocalPos.sqrMagnitude > 0.00001d)
            {
                // The Z-axis (Forward) always points towards the parent body
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
        
        void Update() 
        {
            if (!Application.isPlaying) 
            {
                transform.position = (Vector3)CalculateExactStateAtTime(0d).absolutePosition;
            }
        }
    }
}
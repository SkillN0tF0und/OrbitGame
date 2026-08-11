using UnityEngine;

namespace OrbitGame.OrbitMechanics
{
    [CreateAssetMenu(fileName = "NewCelestialBodySettings", menuName = "Orbit System/Celestial Body Settings")]
    public class CelestialBodySettings : ScriptableObject
    {
        [Header("Physical Properties")]
        public double mass = 1000d;
        public double radius = 100d;

        [Header("Orbital Elements")]
        public OrbitalElements orbit;
        public OrbitAlignment alignment = OrbitAlignment.WorldSpace;

        [Header("Rotation Settings")]
        public double rotationPeriod = 10d;
        public Vector3 axialTilt = new Vector3(0, 23.5f, 0);
    }
}
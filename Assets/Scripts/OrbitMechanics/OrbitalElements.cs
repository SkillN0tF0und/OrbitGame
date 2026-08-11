using UnityEngine;

namespace OrbitGame.OrbitMechanics
{
    public enum OrbitAlignment
    {
        WorldSpace,
        ParentAligned
    }

    [System.Serializable]
    public struct OrbitalElements
    {
        [Range(50f, 10000f)] public double orbitDistance;
        [Range(0f, 1)] public double orbitStretch;
        [Range(0f, 2 * Mathf.PI)] public double orbitTilt;
        [Range(0f, 2 * Mathf.PI)] public double ellipseRotation;
        [Range(0f, 2 * Mathf.PI)] public double planeRotation;
        [Range(0f, 2 * Mathf.PI)] public double startOffset;
    }
}
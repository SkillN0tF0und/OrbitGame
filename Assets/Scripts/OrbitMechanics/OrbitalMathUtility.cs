using UnityEngine;
using OrbitGame.Core; 

namespace OrbitGame.OrbitMechanics
{
    public struct CoordinateFrame 
    {
        public Vector3d forward;
        public Vector3d up;
        public Vector3d right;

        public static CoordinateFrame Identity => new CoordinateFrame 
        {
            forward = new Vector3d(0d, 0d, 1d),
            up = new Vector3d(0d, 1d, 0d),
            right = new Vector3d(1d, 0d, 0d)
        };

        public Vector3d TransformDirection(Vector3d localDir)
        {
            return (right * localDir.x) + (up * localDir.y) + (forward * localDir.z);
        }
    }

    public struct BodyState 
    {
        public Vector3d absolutePosition;
        public CoordinateFrame localFrame;
    }

    public struct OrbitalState 
    {
        public Vector3d localPosition;
    }

    public static class OrbitalMathUtility 
    {
        public const double G = 100.0; 
        
        // Tunable simulation parameters
        public const double KeplerTolerance = 1e-3d;
        public const int KeplerMaxIterations = 10;

        public static OrbitalState CalculateOrbitalState(OrbitalElements elements, double time, double parentMass) 
        {
            double meanMotion = CalculateMeanMotion(elements.orbitDistance, parentMass);
            double meanAnomaly = elements.startOffset + (meanMotion * time);
            double eccentricAnomaly = SolveKeplersEquation(meanAnomaly, elements.orbitStretch);
            double trueAnomaly = CalculateTrueAnomaly(eccentricAnomaly, elements.orbitStretch);

            double radius = elements.orbitDistance * (1.0d - elements.orbitStretch * Mathd.Cos(eccentricAnomaly));
            
            // 1. Start flat
            Vector3d localPos = new Vector3d(radius * Mathd.Cos(trueAnomaly), 0d, radius * Mathd.Sin(trueAnomaly));
            
            // 2. Apply sequential orbital rotations using the newly upgraded Vector3d
            localPos = Vector3d.RotateAroundY(localPos, -elements.ellipseRotation);
            localPos = Vector3d.RotateAroundX(localPos, elements.orbitTilt);
            localPos = Vector3d.RotateAroundY(localPos, -elements.planeRotation);

            return new OrbitalState { localPosition = localPos };
        }

        public static Vector3d CalculateOrbitalNormal(OrbitalElements elements)
        {
            Vector3d up = new Vector3d(0d, 1d, 0d);
            
            // Rotate the 'Up' vector to match the orbital tilt
            up = Vector3d.RotateAroundY(up, -elements.ellipseRotation);
            up = Vector3d.RotateAroundX(up, elements.orbitTilt);
            up = Vector3d.RotateAroundY(up, -elements.planeRotation);
            
            return up;
        }

        private static double CalculateMeanMotion(double orbitDistance, double parentMass) 
        {
            if (orbitDistance <= 0.0001d) return 0d;
            double a3 = orbitDistance * orbitDistance * orbitDistance;
            return Mathd.Sqrt((G * parentMass) / a3);
        }

        private static double SolveKeplersEquation(double meanAnomaly, double orbitStretch) 
        {
            double E = meanAnomaly; 
            
            for (int i = 0; i < KeplerMaxIterations; i++) 
            {
                double deltaE = (E - orbitStretch * Mathd.Sin(E) - meanAnomaly) / (1.0d - orbitStretch * Mathd.Cos(E));
                E -= deltaE;
                
                // Uses the new tunable tolerance
                if (Mathd.Abs(deltaE) < KeplerTolerance) break; 
            }
            return E;
        }

        private static double CalculateTrueAnomaly(double eccentricAnomaly, double orbitStretch) 
        {
            double y = Mathd.Sqrt(1.0d + orbitStretch) * Mathd.Sin(eccentricAnomaly / 2.0d);
            double x = Mathd.Sqrt(1.0d - orbitStretch) * Mathd.Cos(eccentricAnomaly / 2.0d);
            return 2.0d * Mathd.Atan2(y, x);
        }
        
        public static double CalculateOrbitalPeriod(double orbitDistance, double parentMass) 
        {
            if (orbitDistance <= 0.0001d) return 0d;
            double a3 = orbitDistance * orbitDistance * orbitDistance;
            return 2.0d * Mathd.PI * Mathd.Sqrt(a3 / (G * parentMass));
        }
    }
}
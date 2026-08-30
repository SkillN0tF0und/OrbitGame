using UnityEngine;
using UnityEngine.UI;
using PlanetGeneration;

namespace DemoUI
{
    public class PlanetSettingsUI : MonoBehaviour
    {
        [Header("Target Settings")]
        public PlanetSettings settings;

        [Header("Global Settings")]
        public Toggle disableLODToggle;
        public Slider fixedLODLevelSlider;
        public Toggle showWireframeToggle;

        [Header("Biome Map Generation")]
        public Slider biomeNoiseFrequencySlider;
        public Slider biomeBlendDistanceSlider;
        public Slider biomeNoisePointsSlider;

        [Header("Biome Map Distortion")]
        public Slider warpAmplitudeSlider;
        public Slider warpFrequencySlider;

        [Header("Global Planet Visuals")]
        public Slider colorBlendSharpnessSlider;
        public Slider globalSteepnessThresholdSlider;

        public Toggle oceanToggle;
        
        private void Start()
        {
            if (settings == null)
            {
                Debug.LogError("PlanetSettings UI requires an assigned ScriptableObject.");
                return;
            }

            // Global
            SetupToggle(disableLODToggle, settings.disableLOD, v => settings.disableLOD = v);
            SetupSlider(fixedLODLevelSlider, settings.fixedLODLevel, v => settings.fixedLODLevel = (int)v);
            SetupToggle(showWireframeToggle, settings.showWireframe, v => settings.showWireframe = v); // Bound to settings
            
            // Biome Map
            SetupSlider(biomeNoiseFrequencySlider, settings.biomeNoiseFrequency, v => settings.biomeNoiseFrequency = v);
            SetupSlider(biomeBlendDistanceSlider, settings.biomeBlendDistance, v => settings.biomeBlendDistance = v);
            SetupSlider(biomeNoisePointsSlider, settings.biomeNoisePoints, v => settings.biomeNoisePoints = (int)v);

            // Warp
            SetupSlider(warpAmplitudeSlider, settings.warpAmplitude, v => settings.warpAmplitude = v);
            SetupSlider(warpFrequencySlider, settings.warpFrequency, v => settings.warpFrequency = v);

            // Visuals
            SetupSlider(colorBlendSharpnessSlider, settings.colorBlendSharpness, v => settings.colorBlendSharpness = v);
            SetupSlider(globalSteepnessThresholdSlider, settings.globalSteepnessThreshold, v => settings.globalSteepnessThreshold = v);
            SetupToggle(oceanToggle, settings.oceanEnabled, v => settings.oceanEnabled = v);
        }

        private void SetupSlider(Slider slider, float initialValue, System.Action<float> onValueChanged)
        {
            if (slider == null) return;
            slider.value = initialValue;
            slider.onValueChanged.AddListener(v => 
            {
                onValueChanged(v);
                settings.ApplyRuntimeChanges(); 
            });
        }

        private void SetupToggle(Toggle toggle, bool initialValue, System.Action<bool> onValueChanged)
        {
            if (toggle == null) return;
            toggle.isOn = initialValue;
            toggle.onValueChanged.AddListener(v => 
            {
                onValueChanged(v);
                settings.ApplyRuntimeChanges(); 
            });
        }
    }
}
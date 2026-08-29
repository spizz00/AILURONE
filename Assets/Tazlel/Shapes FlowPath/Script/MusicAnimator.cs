using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering.HighDefinition;

namespace Tazlel.ShapesPathFlow.Example
{

    public class MusicAnimator : MonoBehaviour
    {
        public float Bass;
        public float BaseMultiplier;
        public float rhythm;
        public float rhythmMultiplier;

        // [Header("LED Materials")]
        // public List<Material> LED;

        [Header("Volume Reference")] public Volume volume;

        [Header("Chromatic Aberration")] [Range(0f, 1f)]
        public float chromaticAberrationIntensity = 0f;

        [Header("Color Adjustments")] [Range(-180f, 180f)]
        public float hueShift = 0f;

        [Range(-100f, 100f)] public float saturation = 0f;

        [Header("Screen Space Lens Flare")] [Min(0f)]
        public float lensFlareIntensity = 0f;

        [Header("Exposure")] public float exposureLimitMax = 0f;
        public float exposureCompensation = 0f;

        // Cached effect references
        private ChromaticAberration _chromaticAberration;
        private ColorAdjustments _colorAdjustments;
        private ScreenSpaceLensFlare _lensFlare;
        private Exposure _exposure;

        // Original values to restore when exiting play mode
        private float _originalCAIntensity;
        private float _originalHueShift;
        private float _originalSaturation;
        private float _originalLensFlareIntensity;
        private float _originalExposureLimitMax;
        // private List<float> _originalEmissions = new List<float>();

        void Start()
        {
            if (volume == null)
            {
                Debug.LogError("[MusicAnimator] No Volume assigned!");
                enabled = false;
                return;
            }

            volume.profile.TryGet(out _chromaticAberration);
            volume.profile.TryGet(out _colorAdjustments);
            volume.profile.TryGet(out _lensFlare);
            volume.profile.TryGet(out _exposure);

            if (_chromaticAberration != null)
            {
                _originalCAIntensity = _chromaticAberration.intensity.value;
                chromaticAberrationIntensity = _originalCAIntensity;
            }
            else
            {
                Debug.LogWarning("[MusicAnimator] ChromaticAberration not found in Volume Profile.");
            }

            if (_colorAdjustments != null)
            {
                _originalHueShift = _colorAdjustments.hueShift.value;
                _originalSaturation = _colorAdjustments.saturation.value;
                hueShift = _originalHueShift;
                saturation = _originalSaturation;
            }
            else
            {
                Debug.LogWarning("[MusicAnimator] ColorAdjustments not found in Volume Profile.");
            }

            if (_lensFlare != null)
            {
                _originalLensFlareIntensity = _lensFlare.intensity.value;
                lensFlareIntensity = _originalLensFlareIntensity;
            }
            else
            {
                Debug.LogWarning("[MusicAnimator] ScreenSpaceLensFlare not found in Volume Profile.");
            }

            if (_exposure != null)
            {
                _originalExposureLimitMax = _exposure.limitMax.value;
                exposureLimitMax = _originalExposureLimitMax;
                exposureCompensation = _exposure.compensation.value;
            }
            else
            {
                Debug.LogWarning("[MusicAnimator] Exposure not found in Volume Profile.");
            }

            // // Cache original emission intensity for each LED material
            // _originalEmissions.Clear();
            // foreach (Material mat in LED)
            // {
            //     if (mat != null && mat.HasProperty("_EmissiveIntensity"))
            //         _originalEmissions.Add(mat.GetFloat("_EmissiveIntensity"));
            //     else
            //         _originalEmissions.Add(0f);
            // }
        }

        void Update()
        {
            if (_chromaticAberration != null)
                _chromaticAberration.intensity.value = _chromaticAberration.intensity.value;

            if (_colorAdjustments != null)
            {
                _colorAdjustments.hueShift.value = hueShift;
                _colorAdjustments.saturation.value = saturation;
            }

            if (_lensFlare != null)
                _lensFlare.intensity.value = lensFlareIntensity;

            if (_exposure != null)
            {
                _exposure.limitMax.value = exposureLimitMax;
                _exposure.compensation.value = exposureCompensation;
            }

            // // Drive LED emission from Bass
            // for (int i = 0; i < LED.Count; i++)
            // {
            //     if (LED[i] != null && i < _originalEmissions.Count)
            //         LED[i].SetFloat("_EmissiveIntensity", _originalEmissions[i] + Bass * BaseMultiplier);
            // }
        }

        void OnDestroy()
        {
            if (_chromaticAberration != null)
                _chromaticAberration.intensity.value = _originalCAIntensity;

            if (_colorAdjustments != null)
            {
                _colorAdjustments.hueShift.value = _originalHueShift;
                _colorAdjustments.saturation.value = _originalSaturation;
            }

            if (_lensFlare != null)
                _lensFlare.intensity.value = _originalLensFlareIntensity;

            if (_exposure != null)
                _exposure.limitMax.value = _originalExposureLimitMax;

            // // Restore original emission values
            // for (int i = 0; i < LED.Count; i++)
            // {
            //     if (LED[i] != null && i < _originalEmissions.Count)
            //         LED[i].SetFloat("_EmissiveIntensity", _originalEmissions[i]);
            // }
        }
    }
}
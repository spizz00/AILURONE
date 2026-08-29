#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    /// <summary>
    /// Permanently removes the portion of the approved portrait that sits
    /// behind the opaque Integrity backdrop. This prevents the hidden lower
    /// body from becoming visible while the parent HUD CanvasGroup fades.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class AILURONEPortraitBackdropClip : MonoBehaviour
    {
        private const string BackdropName =
            "Approved_IntegrityBackdrop";

        private const string GlitchOverlayName =
            "Runtime_PortraitGlitchOverlay";

        private const string ShaderName =
            "AILURONE/UI/PortraitBackdropClip";

        private static readonly int BackdropMaskId =
            Shader.PropertyToID("_BackdropMask");

        private static readonly int BackdropRectId =
            Shader.PropertyToID("_BackdropRect");

        private readonly Vector3[] _worldCorners =
            new Vector3[4];

        private Image _portrait;
        private Image _backdrop;
        private Image _glitchOverlay;
        private Canvas _canvas;
        private Material _originalMaterial;
        private Material _runtimeMaterial;

        private void OnEnable()
        {
            ResolveReferences();
            CreateRuntimeMaterial();
            UpdateClipProperties();
        }

        private void LateUpdate()
        {
            if (_runtimeMaterial == null)
            {
                ResolveReferences();
                CreateRuntimeMaterial();
            }

            UpdateClipProperties();
        }

        private void OnDisable()
        {
            if (_portrait != null
                && _portrait.material == _runtimeMaterial)
            {
                _portrait.material = _originalMaterial;
            }

            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        private void ResolveReferences()
        {
            if (_portrait == null)
            {
                _portrait = GetComponent<Image>();
            }

            if (_canvas == null && _portrait != null)
            {
                _canvas = _portrait.canvas;
            }

            Transform parent = transform.parent;

            if (_backdrop == null && parent != null)
            {
                Transform backdropTransform =
                    parent.Find(BackdropName);

                if (backdropTransform != null)
                {
                    _backdrop =
                        backdropTransform.GetComponent<Image>();
                }
            }

            if (_glitchOverlay == null && parent != null)
            {
                Transform overlayTransform =
                    parent.Find(GlitchOverlayName);

                if (overlayTransform != null)
                {
                    _glitchOverlay =
                        overlayTransform.GetComponent<Image>();
                }
            }
        }

        private void CreateRuntimeMaterial()
        {
            if (_portrait == null
                || _backdrop == null
                || _backdrop.sprite == null
                || _runtimeMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find(ShaderName);

            if (shader == null)
            {
                return;
            }

            _originalMaterial = _portrait.material;
            _runtimeMaterial = new Material(shader)
            {
                name = "AILURONE_PortraitBackdropClip_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };

            _runtimeMaterial.SetTexture(
                BackdropMaskId,
                _backdrop.sprite.texture);

            _portrait.material = _runtimeMaterial;
            _portrait.SetMaterialDirty();
        }

        private void UpdateClipProperties()
        {
            if (_backdrop == null
                || _backdrop.sprite == null
                || Screen.width <= 0
                || Screen.height <= 0)
            {
                return;
            }

            RectTransform backdropRect =
                _backdrop.rectTransform;

            backdropRect.GetWorldCorners(_worldCorners);

            Camera eventCamera = null;

            if (_canvas != null
                && _canvas.renderMode
                    != RenderMode.ScreenSpaceOverlay)
            {
                eventCamera = _canvas.worldCamera;
            }

            Vector2 minimum = new Vector2(
                float.PositiveInfinity,
                float.PositiveInfinity);

            Vector2 maximum = new Vector2(
                float.NegativeInfinity,
                float.NegativeInfinity);

            for (int index = 0; index < _worldCorners.Length; index++)
            {
                Vector2 screenPoint =
                    RectTransformUtility.WorldToScreenPoint(
                        eventCamera,
                        _worldCorners[index]);

                minimum = Vector2.Min(minimum, screenPoint);
                maximum = Vector2.Max(maximum, screenPoint);
            }

            Vector2 size = Vector2.Max(
                maximum - minimum,
                Vector2.one);

            Vector4 normalisedRect = new Vector4(
                minimum.x / Screen.width,
                minimum.y / Screen.height,
                size.x / Screen.width,
                size.y / Screen.height);

            ApplyClipProperties(
                _runtimeMaterial,
                normalisedRect);

            ResolveReferences();

            Material glitchMaterial = _glitchOverlay != null
                ? _glitchOverlay.material
                : null;

            ApplyClipProperties(
                glitchMaterial,
                normalisedRect);
        }

        private void ApplyClipProperties(
            Material material,
            Vector4 normalisedRect)
        {
            if (material == null
                || !material.HasProperty(BackdropMaskId)
                || !material.HasProperty(BackdropRectId))
            {
                return;
            }

            material.SetTexture(
                BackdropMaskId,
                _backdrop.sprite.texture);

            material.SetVector(
                BackdropRectId,
                normalisedRect);
        }
    }
}

#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// Small procedural charge cue for Flying Bot's twin predictive shot.
/// Two side nodes spread apart while the center brightens, making the attack
/// readable even before dedicated art/VFX is authored.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlyingBotInterceptTelegraphFX : MonoBehaviour
{
    private float _duration;
    private float _remaining;
    private bool _followUp;
    private bool _flashing;
    private float _flashRemaining;

    private Transform _core;
    private Transform _leftNode;
    private Transform _rightNode;
    private Material _material;

    public static FlyingBotInterceptTelegraphFX Spawn(
        Transform anchor,
        float duration,
        bool followUp = false
    )
    {
        if (anchor == null)
        {
            return null;
        }

        GameObject root = new GameObject(
            followUp
                ? "FlyingBot_InterceptFollowUpFX"
                : "FlyingBot_InterceptTelegraphFX"
        );
        root.transform.SetParent(anchor, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        FlyingBotInterceptTelegraphFX fx =
            root.AddComponent<FlyingBotInterceptTelegraphFX>();

        fx.Initialize(duration, followUp);
        return fx;
    }

    private void OnDestroy()
    {
        if (_material != null)
        {
            Destroy(_material);
        }
    }

    private void Initialize(float duration, bool followUp)
    {
        _duration = Mathf.Max(0.05f, duration);
        _remaining = _duration;
        _followUp = followUp;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader != null)
        {
            _material = new Material(shader);
            ApplyMaterialColor(
                _followUp
                    ? new Color(2.4f, 0.55f, 0.12f, 1f)
                    : new Color(0.45f, 1.5f, 2.4f, 1f)
            );
        }

        _core = CreateNode("Core", Vector3.zero, 0.12f);
        float initialNodeOffset = _followUp ? 0.14f : 0.035f;
        _leftNode = CreateNode("Left", Vector3.left * initialNodeOffset, 0.055f);
        _rightNode = CreateNode("Right", Vector3.right * initialNodeOffset, 0.055f);
    }

    private Transform CreateNode(
        string nodeName,
        Vector3 localPosition,
        float scale
    )
    {
        GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        node.name = nodeName;
        node.transform.SetParent(transform, false);
        node.transform.localPosition = localPosition;
        node.transform.localRotation = Quaternion.identity;
        node.transform.localScale = Vector3.one * scale;

        Collider collider = node.GetComponent<Collider>();

        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        Renderer renderer = node.GetComponent<Renderer>();

        if (renderer != null && _material != null)
        {
            renderer.sharedMaterial = _material;
        }

        return node.transform;
    }

    private void Update()
    {
        if (_flashing)
        {
            _flashRemaining -= Time.deltaTime;
            float flash01 = Mathf.Clamp01(_flashRemaining / 0.08f);
            transform.localScale = Vector3.one * Mathf.Lerp(1.75f, 0.15f, 1f - flash01);

            if (_flashRemaining <= 0f)
            {
                Destroy(gameObject);
            }

            return;
        }

        _remaining -= Time.deltaTime;
        float progress = 1f - Mathf.Clamp01(_remaining / _duration);
        float pulse = 1f + Mathf.Sin(Time.time * 26f) * 0.08f;

        if (_core != null)
        {
            _core.localScale = Vector3.one *
                Mathf.Lerp(0.10f, 0.19f, progress) * pulse;
        }

        float nodeOffset = _followUp
            ? Mathf.Lerp(0.14f, 0.025f, progress)
            : Mathf.Lerp(0.035f, 0.14f, progress);

        if (_leftNode != null)
        {
            _leftNode.localPosition = Vector3.left * nodeOffset;
        }

        if (_rightNode != null)
        {
            _rightNode.localPosition = Vector3.right * nodeOffset;
        }

        if (_remaining <= 0f)
        {
            // The owner normally calls CompleteAndFlash in this frame. Keep the
            // cue alive very briefly so script execution order cannot make it
            // disappear before the shot is spawned.
            _remaining = 0f;
        }
    }

    public void CompleteAndFlash()
    {
        if (_flashing)
        {
            return;
        }

        _flashing = true;
        _flashRemaining = 0.08f;
        ApplyMaterialColor(
            _followUp
                ? new Color(3.0f, 1.15f, 0.22f, 1f)
                : new Color(1.65f, 2.25f, 2.8f, 1f)
        );
    }

    public void CancelImmediate()
    {
        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyMaterialColor(Color color)
    {
        if (_material == null)
        {
            return;
        }

        if (_material.HasProperty("_BaseColor"))
        {
            _material.SetColor("_BaseColor", color);
        }

        if (_material.HasProperty("_Color"))
        {
            _material.SetColor("_Color", color);
        }
    }
}

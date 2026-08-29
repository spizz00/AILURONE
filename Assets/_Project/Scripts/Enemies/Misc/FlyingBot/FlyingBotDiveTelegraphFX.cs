#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// Lightweight world-space corridor for the Flying Bot dive attack.
/// The target may move during soft tracking, then becomes fixed on hard lock.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlyingBotDiveTelegraphFX : MonoBehaviour
{
    private Transform _anchor;
    private Vector3 _target;
    private bool _locked;
    private bool _launching;
    private float _launchRemaining;

    private LineRenderer _centerLine;
    private LineRenderer _leftLine;
    private LineRenderer _rightLine;
    private Transform _targetNode;
    private Material _material;

    public static FlyingBotDiveTelegraphFX Spawn(
        Transform anchor,
        Vector3 initialTarget
    )
    {
        if (anchor == null)
        {
            return null;
        }

        GameObject root = new GameObject("FlyingBot_DiveTelegraphFX");
        FlyingBotDiveTelegraphFX fx =
            root.AddComponent<FlyingBotDiveTelegraphFX>();
        fx.Initialize(anchor, initialTarget);
        return fx;
    }

    private void Initialize(Transform anchor, Vector3 initialTarget)
    {
        _anchor = anchor;
        _target = initialTarget;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader != null)
        {
            _material = new Material(shader);
        }

        _centerLine = CreateLine("Center", 0.045f);
        _leftLine = CreateLine("LeftBoundary", 0.025f);
        _rightLine = CreateLine("RightBoundary", 0.025f);

        GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        node.name = "DiveTarget";
        node.transform.SetParent(transform, false);
        node.transform.localScale = Vector3.one * 0.20f;
        _targetNode = node.transform;

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

        ApplyColor(new Color(1f, 0.48f, 0.06f, 0.62f));
        RefreshGeometry();
    }

    private LineRenderer CreateLine(string lineName, float width)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;

        if (_material != null)
        {
            line.sharedMaterial = _material;
        }

        return line;
    }

    private void LateUpdate()
    {
        if (_anchor == null)
        {
            Destroy(gameObject);
            return;
        }

        if (_launching)
        {
            _launchRemaining -= Time.deltaTime;
            float launch01 = Mathf.Clamp01(_launchRemaining / 0.10f);
            transform.localScale = Vector3.one * Mathf.Lerp(1.55f, 0.15f, 1f - launch01);

            if (_launchRemaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }

        RefreshGeometry();

        if (_targetNode != null)
        {
            float pulse = _locked
                ? 1f + Mathf.Sin(Time.time * 32f) * 0.22f
                : 1f + Mathf.Sin(Time.time * 15f) * 0.10f;
            _targetNode.localScale = Vector3.one *
                (_locked ? 0.28f : 0.20f) * pulse;
        }
    }

    private void RefreshGeometry()
    {
        Vector3 start = _anchor.position;
        Vector3 direction = _target - start;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = _anchor.forward;
        }

        direction.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, direction);

        if (side.sqrMagnitude <= 0.0001f)
        {
            side = _anchor.right;
        }

        side.Normalize();
        float startHalfWidth = _locked ? 0.16f : 0.11f;
        float targetHalfWidth = _locked ? 0.72f : 0.55f;

        SetLine(_centerLine, start, _target);
        SetLine(
            _leftLine,
            start - side * startHalfWidth,
            _target - side * targetHalfWidth
        );
        SetLine(
            _rightLine,
            start + side * startHalfWidth,
            _target + side * targetHalfWidth
        );

        if (_targetNode != null)
        {
            _targetNode.position = _target;
        }
    }

    private static void SetLine(
        LineRenderer line,
        Vector3 start,
        Vector3 end
    )
    {
        if (line == null)
        {
            return;
        }

        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    public void UpdateTarget(Vector3 target)
    {
        if (!_locked)
        {
            _target = target;
        }
    }

    public void LockTarget(Vector3 target)
    {
        _target = target;
        _locked = true;
        ApplyColor(new Color(3f, 0.72f, 0.08f, 0.95f));

        SetLineWidth(_centerLine, 0.075f);
        SetLineWidth(_leftLine, 0.045f);
        SetLineWidth(_rightLine, 0.045f);
    }

    public void LaunchFlash()
    {
        if (_launching)
        {
            return;
        }

        _launching = true;
        _launchRemaining = 0.10f;
        ApplyColor(new Color(3.2f, 1.7f, 0.35f, 1f));
    }

    public void CancelImmediate()
    {
        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    private static void SetLineWidth(LineRenderer line, float width)
    {
        if (line == null)
        {
            return;
        }

        line.startWidth = width;
        line.endWidth = width;
    }

    private void ApplyColor(Color color)
    {
        if (_material == null)
        {
            return;
        }

        if (_material.HasProperty("_Color"))
        {
            _material.SetColor("_Color", color);
        }

        if (_material.HasProperty("_BaseColor"))
        {
            _material.SetColor("_BaseColor", color);
        }
    }

    private void OnDestroy()
    {
        if (_material != null)
        {
            Destroy(_material);
        }
    }
}

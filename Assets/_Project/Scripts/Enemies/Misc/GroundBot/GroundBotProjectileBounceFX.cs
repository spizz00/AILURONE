#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// 第一次反弹时生成的短促白红环形闪光。
/// 运行时自建，不需要额外 Prefab 或美术资源。
/// </summary>
public sealed class GroundBotProjectileBounceFX : MonoBehaviour
{
    private const int SegmentCount = 48;

    private LineRenderer _line;
    private Light _flashLight;
    private Material _material;
    private float _elapsed;
    private float _duration = 0.28f;
    private float _startRadius = 0.08f;
    private float _endRadius = 1.0f;
    private float _startWidth = 0.085f;
    private float _startLightIntensity = 5.2f;
    private float _startLightRange = 2.2f;

    public static void Spawn(
        Vector3 position,
        Vector3 surfaceNormal
    )
    {
        GameObject effectObject =
            new GameObject("GroundBot_BounceRingFX");

        effectObject.transform.position = position;

        if (surfaceNormal.sqrMagnitude <= 0.0001f)
        {
            surfaceNormal = Vector3.up;
        }

        effectObject.transform.rotation =
            Quaternion.LookRotation(
                surfaceNormal.normalized,
                Mathf.Abs(Vector3.Dot(
                    surfaceNormal.normalized,
                    Vector3.up
                )) > 0.98f
                    ? Vector3.forward
                    : Vector3.up
            );

        effectObject.AddComponent<GroundBotProjectileBounceFX>();
    }

    private void Awake()
    {
        _line = gameObject.AddComponent<LineRenderer>();
        _line.useWorldSpace = false;
        _line.loop = true;
        _line.positionCount = SegmentCount;
        _line.numCornerVertices = 2;
        _line.numCapVertices = 2;
        _line.alignment = LineAlignment.TransformZ;
        _line.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.textureMode = LineTextureMode.Stretch;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader != null)
        {
            _material = new Material(shader)
            {
                name = "Runtime_GroundBotBounceRing"
            };

            if (_material.HasProperty("_Surface"))
            {
                _material.SetFloat("_Surface", 1f);
            }

            if (_material.HasProperty("_Blend"))
            {
                _material.SetFloat("_Blend", 0f);
            }

            if (_material.HasProperty("_SrcBlend"))
            {
                _material.SetFloat(
                    "_SrcBlend",
                    (float)UnityEngine.Rendering.BlendMode.SrcAlpha
                );
            }

            if (_material.HasProperty("_DstBlend"))
            {
                _material.SetFloat(
                    "_DstBlend",
                    (float)UnityEngine.Rendering.BlendMode.One
                );
            }

            if (_material.HasProperty("_ZWrite"))
            {
                _material.SetFloat("_ZWrite", 0f);
            }

            _material.renderQueue = 3000;
            _line.material = _material;
        }

        _flashLight = gameObject.AddComponent<Light>();
        _flashLight.type = LightType.Point;
        _flashLight.shadows = LightShadows.None;
        _flashLight.renderMode = LightRenderMode.Auto;
        _flashLight.color =
            new Color(1f, 0.18f, 0.06f, 1f);
        _flashLight.intensity = _startLightIntensity;
        _flashLight.range = _startLightRange;

        UpdateRing(0f);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        float normalizedTime =
            Mathf.Clamp01(
                _elapsed / Mathf.Max(0.01f, _duration)
            );

        UpdateRing(normalizedTime);

        if (normalizedTime >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void UpdateRing(float normalizedTime)
    {
        if (_line == null)
        {
            return;
        }

        float eased =
            1f - Mathf.Pow(1f - normalizedTime, 3f);

        float radius =
            Mathf.Lerp(_startRadius, _endRadius, eased);

        float alpha =
            Mathf.Pow(1f - normalizedTime, 1.35f);

        _line.startWidth =
            _startWidth * (1f - normalizedTime);
        _line.endWidth = _line.startWidth;

        Color brightWhiteRed =
            new Color(1f, 0.88f, 0.82f, alpha);

        _line.startColor = brightWhiteRed;
        _line.endColor = brightWhiteRed;

        if (_flashLight != null)
        {
            _flashLight.intensity =
                _startLightIntensity *
                Mathf.Pow(alpha, 1.5f);
            _flashLight.range =
                Mathf.Lerp(
                    _startLightRange,
                    _startLightRange * 0.55f,
                    normalizedTime
                );
        }

        for (int index = 0; index < SegmentCount; index++)
        {
            float angle =
                (index / (float)SegmentCount) *
                Mathf.PI * 2f;

            _line.SetPosition(
                index,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f
                )
            );
        }
    }

    private void OnDestroy()
    {
        if (_material != null)
        {
            Destroy(_material);
            _material = null;
        }
    }
}

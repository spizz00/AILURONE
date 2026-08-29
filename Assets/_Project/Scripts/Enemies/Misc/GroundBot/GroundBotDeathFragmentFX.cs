#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// 单个死亡块体的轻量运动控制器。
/// 只与 Environment 做有限反弹，不影响玩家或其他敌人。
/// </summary>
[DisallowMultipleComponent]
public sealed class GroundBotDeathFragmentFX : MonoBehaviour
{
    private Vector3 _velocity;
    private Vector3 _angularVelocity;
    private Vector3 _baseScale;
    private LayerMask _environmentMask;
    private float _lifetime;
    private float _elapsed;
    private int _remainingBounces = 2;

    public void Initialize(
        Vector3 velocity,
        Vector3 angularVelocity,
        float lifetime,
        LayerMask environmentMask
    )
    {
        _velocity = velocity;
        _angularVelocity = angularVelocity;
        _lifetime = Mathf.Max(0.2f, lifetime);
        _environmentMask = environmentMask;
        _baseScale = transform.localScale;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        _elapsed += deltaTime;

        _velocity +=
            Physics.gravity *
            (0.92f * deltaTime);

        Vector3 delta =
            _velocity * deltaTime;

        float radius =
            Mathf.Max(
                0.015f,
                Mathf.Max(
                    transform.lossyScale.x,
                    Mathf.Max(
                        transform.lossyScale.y,
                        transform.lossyScale.z
                    )
                ) * 0.36f
            );

        if (_remainingBounces > 0 &&
            delta.sqrMagnitude > 0.000001f &&
            Physics.SphereCast(
                transform.position,
                radius,
                delta.normalized,
                out RaycastHit hit,
                delta.magnitude + radius,
                _environmentMask,
                QueryTriggerInteraction.Ignore
            ))
        {
            transform.position =
                hit.point +
                hit.normal * radius;

            Vector3 reflected =
                Vector3.Reflect(
                    _velocity,
                    hit.normal
                );

            Vector3 normalVelocity =
                Vector3.Project(
                    reflected,
                    hit.normal
                );

            Vector3 tangentVelocity =
                reflected - normalVelocity;

            _velocity =
                normalVelocity * 0.38f +
                tangentVelocity * 0.58f;

            _angularVelocity *= 0.76f;
            _remainingBounces--;
        }
        else
        {
            transform.position += delta;
        }

        transform.rotation =
            Quaternion.Euler(
                _angularVelocity * deltaTime
            ) *
            transform.rotation;

        float normalized =
            Mathf.Clamp01(
                _elapsed / _lifetime
            );

        float shrinkT =
            SmoothStep01(
                Mathf.InverseLerp(
                    0.82f,
                    1f,
                    normalized
                )
            );

        transform.localScale =
            _baseScale *
            Mathf.Lerp(1f, 0f, shrinkT);

        if (_elapsed >= _lifetime)
        {
            Destroy(gameObject);
        }
    }

    private static float SmoothStep01(
        float value
    )
    {
        value = Mathf.Clamp01(value);
        return value * value *
               (3f - 2f * value);
    }
}

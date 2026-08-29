#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class EnemyKillZone : MonoBehaviour
{
    private void Awake()
    {
        Collider zoneCollider = GetComponent<Collider>();

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        EnemyTarget enemyTarget =
            other.GetComponentInParent<EnemyTarget>();

        if (enemyTarget == null || enemyTarget.IsDead)
        {
            return;
        }

        enemyTarget.DieFromEnvironment(
            other.ClosestPoint(transform.position)
        );
    }
}

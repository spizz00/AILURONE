using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[DisallowMultipleComponent]
public sealed class LevelEntryDeploymentTrigger : MonoBehaviour
{
    [SerializeField] private LevelEntrySequenceController sequence;
    [SerializeField] private bool requireDescending = true;
    [SerializeField] private float minimumDownwardSpeed = 0.15f;

    public void Configure(
        LevelEntrySequenceController entrySequence,
        bool descendingRequired)
    {
        sequence = entrySequence;
        requireDescending = descendingRequired;

        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryBeginDeployment(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryBeginDeployment(other);
    }

    private void TryBeginDeployment(Collider other)
    {
        if (sequence == null || sequence.DeploymentStarted)
        {
            return;
        }

        PlayerHealth playerHealth =
            other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
        {
            return;
        }

        if (requireDescending)
        {
            CharacterController controller =
                playerHealth.GetComponent<CharacterController>();

            if (controller != null &&
                controller.velocity.y > -minimumDownwardSpeed)
            {
                return;
            }
        }

        sequence.BeginDeployment();
    }
}

using UnityEngine;

namespace AILURONE.HUD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class AILURONEClearRoomMembershipVolume : MonoBehaviour
    {
        [SerializeField] private BoxCollider volumeCollider;

        public BoxCollider VolumeCollider
        {
            get
            {
                if (volumeCollider == null)
                {
                    volumeCollider = GetComponent<BoxCollider>();
                }
                return volumeCollider;
            }
        }

        public bool Contains(Vector3 worldPoint)
        {
            BoxCollider box = VolumeCollider;
            if (box == null || !gameObject.activeInHierarchy)
            {
                return false;
            }

            Vector3 localPoint =
                box.transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 halfSize = box.size * 0.5f;
            const float epsilon = 0.001f;
            return Mathf.Abs(localPoint.x) <= halfSize.x + epsilon &&
                Mathf.Abs(localPoint.y) <= halfSize.y + epsilon &&
                Mathf.Abs(localPoint.z) <= halfSize.z + epsilon;
        }

        private void Reset()
        {
            volumeCollider = GetComponent<BoxCollider>();
            ConfigureCollider();
        }

        private void OnValidate()
        {
            if (volumeCollider == null)
            {
                volumeCollider = GetComponent<BoxCollider>();
            }
            ConfigureCollider();
        }

        private void ConfigureCollider()
        {
            if (volumeCollider != null)
            {
                volumeCollider.isTrigger = true;
            }
            gameObject.layer = 2;
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider box = VolumeCollider;
            if (box == null)
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.color = new Color(0.20f, 0.86f, 0.94f, 0.18f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.35f, 0.95f, 1f, 0.90f);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}

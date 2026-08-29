using UnityEngine;

namespace AILURONE.HUD
{
    /// <summary>
    /// Read-only companion to CoreSocket. It mirrors the socket's existing
    /// trigger volume for UI purposes and never reads the E key or changes
    /// node/socket state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoreSocket))]
    public sealed class AILURONESocketInteractionPromptTrigger : MonoBehaviour
    {
        [SerializeField] private CoreSocket socket;
        [SerializeField] private AILURONESocketInteractionPrompt prompt;

        private bool _playerInside;

        public bool IsPlayerInside => _playerInside;

        public bool IsInteractionAvailable
        {
            get
            {
                if (socket == null)
                {
                    return false;
                }

                // CoreSocket activates visualCoreMesh on a successful link.
                // This lets the prompt observe completion without changing
                // CoreSocket or duplicating its private gameplay state.
                return socket.visualCoreMesh == null ||
                       !socket.visualCoreMesh.activeSelf;
            }
        }

        private void Awake()
        {
            if (socket == null)
            {
                socket = GetComponent<CoreSocket>();
            }

            ResolvePrompt();
        }

        private void Start()
        {
            // CoreSocket.Start() initializes the socket to empty.
            // No UI action is needed until the player actually enters.
        }

        private void LateUpdate()
        {
            if (!_playerInside)
            {
                return;
            }

            if (!IsInteractionAvailable)
            {
                if (prompt != null)
                {
                    prompt.Unregister(this);
                }

                _playerInside = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            _playerInside = true;
            ResolvePrompt();

            if (prompt != null &&
                IsInteractionAvailable)
            {
                prompt.Register(this);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            if (!_playerInside)
            {
                _playerInside = true;
            }

            ResolvePrompt();

            if (prompt != null &&
                IsInteractionAvailable)
            {
                prompt.Register(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            _playerInside = false;

            if (prompt != null)
            {
                prompt.Unregister(this);
            }
        }

        private void OnDisable()
        {
            _playerInside = false;

            if (prompt != null)
            {
                prompt.Unregister(this);
            }
        }

        private void ResolvePrompt()
        {
            if (prompt != null)
            {
                return;
            }

            prompt =
                Object.FindFirstObjectByType<
                    AILURONESocketInteractionPrompt>(
                        FindObjectsInactive.Include);
        }

        public void EditorBind(
            CoreSocket targetSocket,
            AILURONESocketInteractionPrompt targetPrompt)
        {
            socket = targetSocket;
            prompt = targetPrompt;
        }
    }
}

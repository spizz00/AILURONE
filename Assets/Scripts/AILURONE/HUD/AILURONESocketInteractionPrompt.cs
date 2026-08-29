using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AILURONE.HUD
{
    /// <summary>
    /// One lightweight screen-space prompt shared by all Level Rewrite Node sockets.
    /// Socket trigger components report enter/exit state; this class only presents UI.
    /// It does not alter CoreSocket gameplay or consume input.
    /// </summary>
    public sealed class AILURONESocketInteractionPrompt : MonoBehaviour
    {
        [SerializeField] private RectTransform promptRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text promptText;

        [Header("Presentation")]
        [SerializeField] private string prompt =
            "<color=#57D9E8>[E]</color>  LINK REWRITE NODES";

        [SerializeField] private Vector2 shownPosition =
            new Vector2(0f, -118f);

        [SerializeField] private Vector2 hiddenOffset =
            new Vector2(0f, -10f);

        [Min(1f)]
        [SerializeField] private float showResponse = 18f;

        [Min(1f)]
        [SerializeField] private float hideResponse = 24f;

        [Range(0.8f, 1f)]
        [SerializeField] private float hiddenScale = 0.96f;

        private readonly HashSet<AILURONESocketInteractionPromptTrigger>
            _activeTriggers =
                new HashSet<AILURONESocketInteractionPromptTrigger>();

        private float _visibility;
        private bool _desiredVisible;

        public bool DesiredVisible => _desiredVisible;
        public int ActiveSocketCount => _activeTriggers.Count;

        private void Awake()
        {
            if (promptRoot == null)
            {
                promptRoot = transform as RectTransform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (promptText == null)
            {
                promptText = GetComponentInChildren<TMP_Text>(true);
            }

            ApplyImmediateHiddenState();
        }

        private void OnEnable()
        {
            RefreshText();
        }

        private void Update()
        {
            RemoveInvalidTriggers();

            bool gameplayAllowed =
                AILURONEGameplayActionGate.AllowsGameplayActions;

            _desiredVisible =
                gameplayAllowed &&
                _activeTriggers.Count > 0;

            float deltaTime =
                Mathf.Max(0f, Time.unscaledDeltaTime);

            float response =
                _desiredVisible
                    ? showResponse
                    : hideResponse;

            float target =
                _desiredVisible
                    ? 1f
                    : 0f;

            float blend =
                1f -
                Mathf.Exp(
                    -Mathf.Max(1f, response) *
                    deltaTime);

            _visibility =
                Mathf.Lerp(
                    _visibility,
                    target,
                    blend);

            if (!_desiredVisible &&
                _visibility < 0.001f)
            {
                _visibility = 0f;
            }
            else if (_desiredVisible &&
                     _visibility > 0.999f)
            {
                _visibility = 1f;
            }

            Render();
        }

        public void Register(
            AILURONESocketInteractionPromptTrigger trigger)
        {
            if (trigger == null ||
                !trigger.IsInteractionAvailable)
            {
                return;
            }

            _activeTriggers.Add(trigger);
            RefreshText();
        }

        public void Unregister(
            AILURONESocketInteractionPromptTrigger trigger)
        {
            if (trigger == null)
            {
                return;
            }

            _activeTriggers.Remove(trigger);
        }

        private void RemoveInvalidTriggers()
        {
            if (_activeTriggers.Count == 0)
            {
                return;
            }

            _activeTriggers.RemoveWhere(
                trigger =>
                    trigger == null ||
                    !trigger.isActiveAndEnabled ||
                    !trigger.IsPlayerInside ||
                    !trigger.IsInteractionAvailable);
        }

        private void RefreshText()
        {
            if (promptText == null)
            {
                return;
            }

            promptText.text = prompt;
            promptText.richText = true;
        }

        private void Render()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha =
                    Mathf.Clamp01(_visibility);

                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (promptRoot != null)
            {
                promptRoot.anchoredPosition =
                    Vector2.Lerp(
                        shownPosition + hiddenOffset,
                        shownPosition,
                        _visibility);

                float scale =
                    Mathf.Lerp(
                        hiddenScale,
                        1f,
                        _visibility);

                promptRoot.localScale =
                    new Vector3(
                        scale,
                        scale,
                        1f);
            }
        }

        private void ApplyImmediateHiddenState()
        {
            _visibility = 0f;
            _desiredVisible = false;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (promptRoot != null)
            {
                promptRoot.anchoredPosition =
                    shownPosition + hiddenOffset;

                promptRoot.localScale =
                    new Vector3(
                        hiddenScale,
                        hiddenScale,
                        1f);
            }

            RefreshText();
        }

        private void OnValidate()
        {
            showResponse =
                Mathf.Max(1f, showResponse);

            hideResponse =
                Mathf.Max(1f, hideResponse);

            hiddenScale =
                Mathf.Clamp(
                    hiddenScale,
                    0.8f,
                    1f);

            RefreshText();
        }
    }
}

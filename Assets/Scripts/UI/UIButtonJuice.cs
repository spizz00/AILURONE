#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Adds responsive micro-interactions to buttons:
/// - Smooth scale bounce on hover/press
/// - Inverts colors on hover/press: White background + Black text
/// - Adds subtle outline border
/// - Audio SFX playback for Hover & Click events
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButtonJuice : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler,
    ISubmitHandler
{
    [Header("Feature Toggles")]
    [SerializeField] private bool enableJuice = true;

    [Header("Scale Animation")]
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float pressedScale = 0.95f;
    [SerializeField] private float animSpeed = 15f;

    [Header("Color Highlights")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private Color hoverImageColor = Color.white;
    [SerializeField] private Color hoverTextColor = Color.black;

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSFX;
    [SerializeField] private AudioClip clickSFX;

    private Vector3 _originalScale;
    private Vector3 _targetScale;
    private Button _button;

    private Color _normalImageColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    private Color _normalTextColor = Color.white;
    private bool _isHovered = false;
    private float _lastHoverSoundAt = -10f;
    private float _lastClickSoundAt = -10f;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _originalScale = transform.localScale;
        _targetScale = _originalScale;

        if (enableJuice)
        {
            if (buttonImage == null) buttonImage = GetComponent<Image>();
            if (buttonText == null) buttonText = GetComponentInChildren<TMP_Text>();

            if (buttonImage != null)
            {
                _normalImageColor = buttonImage.color;
                if (_normalImageColor.a < 0.05f)
                {
                    _normalImageColor = new Color(0.05f, 0.05f, 0.05f, 0.6f);
                    buttonImage.color = _normalImageColor;
                }
            }

            if (buttonText != null)
            {
                _normalTextColor = buttonText.color;
            }

            var outline = GetComponent<Outline>();
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 1f, 1f, 0.35f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }

        if (_button != null)
        {
            if (enableJuice)
            {
                _button.transition = Selectable.Transition.None;
            }
        }
    }

    private void Update()
    {
        if (enableJuice)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * animSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;

        _isHovered = true;
        
        if (enableJuice)
        {
            _targetScale = _originalScale * hoverScale;
            ApplyHoverColors();
        }
        
        PlayHoverSFX();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        
        if (enableJuice)
        {
            _targetScale = _originalScale;
            ApplyNormalColors();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            PlayClickSFX();
        }
        
        if (enableJuice)
        {
            _targetScale = _originalScale * pressedScale;
            ApplyHoverColors();
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (_button != null && !_button.interactable) return;

        if (enableJuice)
        {
            _targetScale = _originalScale * hoverScale;
            ApplyHoverColors();
        }

        PlayHoverSFX();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (_isHovered || !enableJuice)
        {
            return;
        }

        _targetScale = _originalScale;
        ApplyNormalColors();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        PlayClickSFX();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;
        
        if (enableJuice)
        {
            _targetScale = (eventData.hovered.Contains(gameObject)) ? _originalScale * hoverScale : _originalScale;
            if (!_isHovered) ApplyNormalColors();
        }
    }

    private void ApplyHoverColors()
    {
        if (buttonImage != null) buttonImage.color = hoverImageColor;
        if (buttonText != null) buttonText.color = hoverTextColor;
    }

    private void ApplyNormalColors()
    {
        if (buttonImage != null) buttonImage.color = _normalImageColor;
        if (buttonText != null) buttonText.color = _normalTextColor;
    }

    private void PlayHoverSFX()
    {
        if (Time.unscaledTime - _lastHoverSoundAt < 0.075f)
        {
            return;
        }

        _lastHoverSoundAt = Time.unscaledTime;
        PlaySFX(hoverSFX, 0.6f);
    }

    private void PlayClickSFX()
    {
        if (_button != null && !_button.interactable)
        {
            return;
        }

        if (Time.unscaledTime - _lastClickSoundAt < 0.075f)
        {
            return;
        }

        _lastClickSoundAt = Time.unscaledTime;
        PlaySFX(clickSFX, 1f);
    }

    private void PlaySFX(AudioClip clip, float volume)
    {
        AILURONEUIAudioFeedback.PlayGlobal(clip, volume);
    }
}

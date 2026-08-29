using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Consistent menu audio for every Selectable. Pointer-down is used for the
/// click cue so the sound starts before a button hides a panel or loads a scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class AILURONEUIAudioFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerDownHandler,
    ISelectHandler,
    ISubmitHandler
{
    private const float FeedbackDebounce = 0.075f;

    [SerializeField] private AudioClip hoverSFX;
    [SerializeField] private AudioClip clickSFX;
    [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.9f;

    private static AudioSource _sharedSource;
    private static AudioSource _pitchedSharedSource;

    private Selectable _selectable;
    private float _lastHoverAt = -10f;
    private float _lastClickAt = -10f;

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
    }

    public void Configure(AudioClip hoverClip, AudioClip clickClip)
    {
        hoverSFX = hoverClip;
        clickSFX = clickClip;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayHoverFeedback();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            PlayClickFeedback();
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        PlayHoverFeedback();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        PlayClickFeedback();
    }

    public void PlayHoverFeedback()
    {
        if (!CanPlay() || Time.unscaledTime - _lastHoverAt < FeedbackDebounce)
        {
            return;
        }

        _lastHoverAt = Time.unscaledTime;
        PlayGlobal(hoverSFX, hoverVolume);
    }

    public void PlayClickFeedback()
    {
        if (!CanPlay() || Time.unscaledTime - _lastClickAt < FeedbackDebounce)
        {
            return;
        }

        _lastClickAt = Time.unscaledTime;
        PlayGlobal(clickSFX, clickVolume);
    }

    public static void PlayGlobal(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource source = GetSharedSource();
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public static void PlayGlobal(
        AudioClip clip,
        float volume,
        float pitch)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource source = GetPitchedSharedSource();
        source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private bool CanPlay()
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        if (_selectable == null)
        {
            _selectable = GetComponent<Selectable>();
        }

        return _selectable == null || _selectable.IsInteractable();
    }

    private static AudioSource GetSharedSource()
    {
        if (_sharedSource != null)
        {
            return _sharedSource;
        }

        GameObject sourceObject = new GameObject("AILURONE_UI_SFX");
        Object.DontDestroyOnLoad(sourceObject);

        _sharedSource = sourceObject.AddComponent<AudioSource>();
        _sharedSource.playOnAwake = false;
        _sharedSource.spatialBlend = 0f;
        _sharedSource.ignoreListenerPause = true;

        AILURONEAudioCategory category =
            sourceObject.AddComponent<AILURONEAudioCategory>();
        category.category = AILURONEAudioCategory.Category.SoundEffects;
        return _sharedSource;
    }

    private static AudioSource GetPitchedSharedSource()
    {
        if (_pitchedSharedSource != null)
        {
            return _pitchedSharedSource;
        }

        GameObject sourceObject = new GameObject("AILURONE_UI_SFX_PITCHED");
        Object.DontDestroyOnLoad(sourceObject);

        _pitchedSharedSource = sourceObject.AddComponent<AudioSource>();
        _pitchedSharedSource.playOnAwake = false;
        _pitchedSharedSource.spatialBlend = 0f;
        _pitchedSharedSource.ignoreListenerPause = true;

        AILURONEAudioCategory category =
            sourceObject.AddComponent<AILURONEAudioCategory>();
        category.category = AILURONEAudioCategory.Category.SoundEffects;
        return _pitchedSharedSource;
    }
}

#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class CutsceneManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI storyText;
    [SerializeField] private Button skipButton;

    [Header("Cutscene Settings")]
    [SerializeField] private float typingSpeed = 0.02f;
    [SerializeField] private string nextSceneName = "Tutorial";
    [SerializeField] private float autoAdvanceDelay = 4f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] typingSounds;
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 0.25f;

    [Header("UI Pulse Settings")]
    [SerializeField] private float pulseSpeed = 2.5f;
    [SerializeField] private float minAlpha = 0.25f;
    [SerializeField] private float maxAlpha = 0.85f;

    [TextArea(3, 10)]
    public List<string> storySlides = new List<string>();

    private int currentSlideIndex = 0;
    private Coroutine typingCoroutine;
    private Coroutine autoAdvanceCoroutine;
    private bool isTyping = false;
    private CanvasGroup skipCanvasGroup;

    private void Start()
    {
        // Ensure cursor is visible for the Skip button
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (skipButton == null)
        {
            var btnGO = GameObject.Find("SkipButton");
            if (btnGO != null) skipButton = btnGO.GetComponent<Button>();
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(SkipCutscene);
            skipButton.onClick.AddListener(SkipCutscene);

            // Ensure skipButton has a raycast-able Graphic (fully transparent so no box shows, but clicks register)
            Image img = skipButton.GetComponent<Image>();
            if (img == null)
            {
                img = skipButton.gameObject.AddComponent<Image>();
            }
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
            skipButton.targetGraphic = img;

            skipCanvasGroup = skipButton.GetComponent<CanvasGroup>();
            if (skipCanvasGroup == null)
            {
                skipCanvasGroup = skipButton.gameObject.AddComponent<CanvasGroup>();
            }
            StartCoroutine(PulseSkipButtonRoutine());
        }

        if (storySlides.Count > 0)
        {
            StartSlide(currentSlideIndex);
        }
        else
        {
            SkipCutscene();
        }
    }

    private IEnumerator PulseSkipButtonRoutine()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
            if (skipCanvasGroup != null)
            {
                skipCanvasGroup.alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
            }
            yield return null;
        }
    }

    private void Update()
    {
        // Advance or skip typing when Left Mouse Button, Space, or Enter is pressed
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame ||
            Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
        {
            if (isTyping)
            {
                // Instantly finish typing the current slide
                if (typingCoroutine != null) StopCoroutine(typingCoroutine);
                storyText.text = storySlides[currentSlideIndex];
                isTyping = false;
            }
            else
            {
                AdvanceToNextSlide();
            }
        }
    }

    private void StartSlide(int index)
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        if (autoAdvanceCoroutine != null) StopCoroutine(autoAdvanceCoroutine);
        
        typingCoroutine = StartCoroutine(TypeText(storySlides[index]));
        autoAdvanceCoroutine = StartCoroutine(AutoAdvanceRoutine());
    }

    private IEnumerator AutoAdvanceRoutine()
    {
        // Wait until typing finishes (or player skips typing)
        yield return new WaitUntil(() => !isTyping);
        // Give player time to read full text before moving on
        yield return new WaitForSeconds(autoAdvanceDelay);
        AdvanceToNextSlide();
    }

    private void AdvanceToNextSlide()
    {
        currentSlideIndex++;
        if (currentSlideIndex < storySlides.Count)
        {
            StartSlide(currentSlideIndex);
        }
        else
        {
            SkipCutscene();
        }
    }

    private IEnumerator TypeText(string content)
    {
        isTyping = true;
        storyText.text = "";

        foreach (char c in content.ToCharArray())
        {
            storyText.text += c;
            
            if (!char.IsWhiteSpace(c) && audioSource != null && typingSounds != null && typingSounds.Length > 0)
            {
                AudioClip clip = typingSounds[Random.Range(0, typingSounds.Length)];
                if (clip != null) audioSource.PlayOneShot(clip, soundVolume);
            }

            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
    }

    public void SkipCutscene()
    {
        Debug.Log("[CutsceneManager] Loading next scene: " + nextSceneName);
        SceneManager.LoadScene(nextSceneName);
    }
}

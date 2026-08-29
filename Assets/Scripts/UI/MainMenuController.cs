#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AILURONE.Ranking;

/// <summary>
/// Handles Main Menu button actions: starting the game, opening the Settings/Credits
/// panels, and quitting. Attach to the main menu Canvas root.
/// Automatically binds button events if unassigned in Inspector to prevent broken UI state.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private const string TutorialSceneName = "IntroCutscene";

    [Header("Panels")]
    [SerializeField] private GameObject menuButtonsPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject panelLogo;

    [Header("Buttons (Optional Explicit References)")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Button creditsBackButton;

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip buttonClickSFX;

    private void Awake()
    {
        // Ensure proper environment state on entering Main Menu
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        AutoResolveReferences();
        BindAllButtonListeners();
    }

    private void Start()
    {
        // Ensure initial panel visibility state immediately
        SetPanelActive(menuButtonsPanel, true, immediate: true);
        SetPanelActive(panelLogo, true, immediate: true);
        SetPanelActive(settingsPanel, false, immediate: true);
        SetPanelActive(creditsPanel, false, immediate: true);
    }

    private void Update()
    {
        if (Keyboard.current == null ||
            !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            OnSettingsBackPressed();
        }
        else if (creditsPanel != null && creditsPanel.activeSelf)
        {
            OnCreditsBackPressed();
        }
    }

    private void AutoResolveReferences()
    {
        if (menuButtonsPanel == null) menuButtonsPanel = transform.Find("Panel_MenuButtons")?.gameObject;
        if (settingsPanel == null) settingsPanel = transform.Find("Panel_Settings")?.gameObject;
        if (creditsPanel == null) creditsPanel = transform.Find("Panel_Credits")?.gameObject;
        if (panelLogo == null) panelLogo = transform.Find("Panel_Logo")?.gameObject;

        if (playButton == null && menuButtonsPanel != null) playButton = menuButtonsPanel.transform.Find("Button_Play")?.GetComponent<Button>();
        if (settingsButton == null && menuButtonsPanel != null) settingsButton = menuButtonsPanel.transform.Find("Button_Settings")?.GetComponent<Button>();
        if (creditsButton == null && menuButtonsPanel != null) creditsButton = menuButtonsPanel.transform.Find("Button_Credits")?.GetComponent<Button>();
        if (exitButton == null && menuButtonsPanel != null) exitButton = menuButtonsPanel.transform.Find("Button_Exit")?.GetComponent<Button>();

        if (settingsBackButton == null && settingsPanel != null) settingsBackButton = settingsPanel.transform.Find("Button_SettingsBack")?.GetComponent<Button>();

        if (creditsBackButton == null && creditsPanel != null) creditsBackButton = creditsPanel.transform.Find("Button_CreditsBack")?.GetComponent<Button>();

        if (settingsPanel != null)
        {
            var mmBtn = settingsPanel.transform.Find("Button_MainMenu");
            if (mmBtn != null) mmBtn.gameObject.SetActive(false);
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void BindAllButtonListeners()
    {
        BindButton(playButton, OnPlayPressed);
        BindButton(settingsButton, OnSettingsPressed);
        BindButton(creditsButton, OnCreditsPressed);
        BindButton(exitButton, OnExitPressed);
        BindButton(settingsBackButton, OnSettingsBackPressed);
        BindButton(creditsBackButton, OnCreditsBackPressed);
    }

    private void BindButton(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn != null && action != null)
        {
            btn.onClick.RemoveListener(action);
            btn.onClick.AddListener(action);
        }
    }

    private void PlayClickSFX()
    {
        if (audioSource != null && buttonClickSFX != null)
        {
            audioSource.PlayOneShot(buttonClickSFX);
        }
    }

    /// <summary>Loads the Tutorial scene to begin the game.</summary>
    public void OnPlayPressed()
    {
        PlayClickSFX();
        AILURONEUsernamePrompt.Show(LoadOpeningAfterIdentity);
    }

    private void LoadOpeningAfterIdentity()
    {
        Debug.Log($"[MainMenu] Loading scene: {TutorialSceneName}");
        SceneManager.LoadScene(TutorialSceneName);
    }

    /// <summary>Opens the Settings panel and hides the main button stack and logo.</summary>
    public void OnSettingsPressed()
    {
        PlayClickSFX();
        SetPanelActive(settingsPanel, true);
        SetPanelActive(menuButtonsPanel, false);
        SetPanelActive(panelLogo, false);
    }

    /// <summary>Closes the Settings panel and shows the main button stack and logo again.</summary>
    public void OnSettingsBackPressed()
    {
        PlayClickSFX();
        SetPanelActive(settingsPanel, false);
        SetPanelActive(menuButtonsPanel, true);
        SetPanelActive(panelLogo, true);
    }

    /// <summary>Opens the Credits panel and hides the main button stack and logo.</summary>
    public void OnCreditsPressed()
    {
        PlayClickSFX();
        SetPanelActive(creditsPanel, true);
        SetPanelActive(menuButtonsPanel, false);
        SetPanelActive(panelLogo, false);
    }

    /// <summary>Closes the Credits panel and shows the main button stack and logo again.</summary>
    public void OnCreditsBackPressed()
    {
        PlayClickSFX();
        SetPanelActive(creditsPanel, false);
        SetPanelActive(menuButtonsPanel, true);
        SetPanelActive(panelLogo, true);
    }

    /// <summary>Quits the application, or stops Play Mode when running in the Editor.</summary>
    public void OnExitPressed()
    {
        PlayClickSFX();
        Debug.Log("[MainMenu] Quitting application...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void SetPanelActive(GameObject panel, bool isActive, bool immediate = false)
    {
        if (panel != null)
        {
            var fader = panel.GetComponent<UIPanelFader>();
            if (fader != null)
            {
                fader.SetVisible(isActive, immediate);
            }
            else
            {
                panel.SetActive(isActive);
            }
        }
    }
}


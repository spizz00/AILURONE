using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gameplay pause shell. ESC opens a pause home page; Settings uses the same
/// Panel_Settings prefab as the main menu. Restart and Main Menu require a
/// confirmation, and resume restores the exact time scale that existed before
/// pausing (including Overclock).
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    private enum PausePage
    {
        Hidden,
        Home,
        Settings,
        ConfirmRestart,
        ConfirmMainMenu
    }

    [Header("Blur")]
    [SerializeField] private float blurFadeDuration = 0.24f;
    [SerializeField] private float blurMaxRadius = 1.8f;

    [Header("Transition")]
    [SerializeField] private float pauseOpenDuration = 0.26f;
    [SerializeField] private float pauseCloseDuration = 0.16f;
    [SerializeField] private float homeSlideDistance = 58f;

    private GameObject _pauseHome;
    private GameObject _settingsPanel;
    private GameObject _confirmationPanel;
    private TMP_Text _confirmationMessage;
    private Button _settingsBackButton;
    private Button _confirmButton;

    private Volume _pauseVolume;
    private Coroutine _blurCoroutine;
    private Coroutine _shellTransitionCoroutine;
    private Coroutine _pageTransitionCoroutine;
    private CanvasGroup _rootGroup;
    private CanvasGroup _homeGroup;
    private RectTransform _homeRect;
    private Vector2 _homeVisiblePosition;
    private Image _backdrop;
    private Color _backdropVisibleColor;
    private PausePage _page = PausePage.Hidden;
    private PausePage _pageBeforeTransition = PausePage.Hidden;
    private bool _transitioning;
    private float _timeScaleBeforePause = 1f;
    private float _fixedDeltaBeforePause = 0.02f;
    private CursorLockMode _cursorLockBeforePause;
    private bool _cursorVisibleBeforePause;
    private bool _starterCursorLockedBeforePause = true;
    private bool _starterCursorLookBeforePause = true;
    private StarterAssets.StarterAssetsInputs _starterInputs;

    [System.Obsolete("Legacy editor installer compatibility only.")]
    public GameObject settingsPanel
    {
        get => _settingsPanel;
        set => _settingsPanel = value;
    }

    public bool IsPaused => _page != PausePage.Hidden;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
        SetupPauseBlurVolume();
        ShowOnly(PausePage.Hidden, true);
    }

    private void Update()
    {
        if (Keyboard.current == null ||
            !Keyboard.current.escapeKey.wasPressedThisFrame ||
            _transitioning)
        {
            return;
        }

        switch (_page)
        {
            case PausePage.Hidden:
                PauseGame();
                break;
            case PausePage.Home:
                ResumeGame();
                break;
            case PausePage.Settings:
            case PausePage.ConfirmRestart:
            case PausePage.ConfirmMainMenu:
                TransitionToPage(PausePage.Home);
                break;
        }
    }

    private void OnDestroy()
    {
        if (IsPaused)
        {
            RestoreGameplayState();
        }
    }

    public void TogglePause()
    {
        if (IsPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (IsPaused || GameManager.Instance == null ||
            GameManager.Instance.IsLevelEnded)
        {
            return;
        }

        _timeScaleBeforePause = Time.timeScale;
        _fixedDeltaBeforePause = Time.fixedDeltaTime;
        _cursorLockBeforePause = Cursor.lockState;
        _cursorVisibleBeforePause = Cursor.visible;

        _starterInputs = FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
        if (_starterInputs != null)
        {
            _starterCursorLockedBeforePause = _starterInputs.cursorLocked;
            _starterCursorLookBeforePause = _starterInputs.cursorInputForLook;
            _starterInputs.cursorLocked = false;
            _starterInputs.cursorInputForLook = false;
        }

        GameManager.Instance.isGamePaused = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ShowOnly(PausePage.Home, true);
        BeginShellTransition(true);
    }

    public void ResumeGame()
    {
        if (!IsPaused)
        {
            return;
        }

        BeginShellTransition(false);
    }

    public void ShowSettings()
    {
        if (!IsPaused)
        {
            return;
        }

        TransitionToPage(PausePage.Settings);
        if (_settingsBackButton != null)
        {
            _settingsBackButton.onClick.RemoveAllListeners();
            _settingsBackButton.onClick.AddListener(BackToPauseHome);
        }
    }

    public void BackToPauseHome()
    {
        TransitionToPage(PausePage.Home);
    }

    public void ShowRestartConfirmation()
    {
        ConfigureConfirmation(
            "RESTART CURRENT LEVEL?\nUNSAVED PROGRESS WILL BE LOST.",
            RestartCurrentLevel,
            PausePage.ConfirmRestart);
    }

    public void ShowMainMenuConfirmation()
    {
        ConfigureConfirmation(
            "RETURN TO MAIN MENU?\nUNSAVED PROGRESS WILL BE LOST.",
            ReturnToMainMenu,
            PausePage.ConfirmMainMenu);
    }

    public void CancelConfirmation()
    {
        ShowOnly(PausePage.Home);
    }

    public void RestartCurrentLevel()
    {
        PrepareForSceneChange();
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    public void ReturnToMainMenu()
    {
        PrepareForSceneChange();
        SceneManager.LoadScene("MainMenu");
    }

    public void OnMainMenuPressed()
    {
        ShowMainMenuConfirmation();
    }

    private void ResolveReferences()
    {
        _pauseHome = FindChildObject("PauseHome");
        _settingsPanel = FindChildObject("Panel_Settings");
        _confirmationPanel = FindChildObject("ConfirmationPanel");
        _confirmationMessage = FindChild<TMP_Text>("Text_ConfirmationMessage");
        _settingsBackButton = FindChild<Button>("Button_SettingsBack");
        _confirmButton = FindChild<Button>("Button_Confirm");
        _rootGroup = GetComponent<CanvasGroup>();
        _homeGroup = _pauseHome != null
            ? _pauseHome.GetComponent<CanvasGroup>()
            : null;
        _homeRect = _pauseHome != null
            ? _pauseHome.transform as RectTransform
            : null;
        if (_homeRect != null)
        {
            _homeVisiblePosition = _homeRect.anchoredPosition;
        }

        _backdrop = FindChild<Image>("PauseBackdrop");
        if (_backdrop != null)
        {
            _backdropVisibleColor = _backdrop.color;
        }
    }

    private void BindButtons()
    {
        BindButton("Button_Resume", ResumeGame);
        BindButton("Button_OpenSettings", ShowSettings);
        BindButton("Button_Restart", ShowRestartConfirmation);
        BindButton("Button_ReturnMainMenu", ShowMainMenuConfirmation);
        BindButton("Button_Cancel", CancelConfirmation);
    }

    private void BindButton(string name, UnityEngine.Events.UnityAction action)
    {
        Button button = FindChild<Button>(name);
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void ConfigureConfirmation(
        string message,
        UnityEngine.Events.UnityAction confirmedAction,
        PausePage page)
    {
        if (_confirmationMessage != null)
        {
            _confirmationMessage.text = message;
        }

        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.AddListener(confirmedAction);
        }

        TransitionToPage(page);
    }

    private void ShowOnly(PausePage page, bool immediate = false)
    {
        _page = page;

        SetPanelVisible(_pauseHome, page == PausePage.Home, immediate);
        SetPanelVisible(_settingsPanel, page == PausePage.Settings, immediate);
        SetPanelVisible(
            _confirmationPanel,
            page == PausePage.ConfirmRestart || page == PausePage.ConfirmMainMenu,
            immediate);

        if (_rootGroup != null &&
            (immediate || page == PausePage.Hidden))
        {
            bool visible = page != PausePage.Hidden;
            _rootGroup.alpha = visible ? 1f : 0f;
            _rootGroup.interactable = visible;
            _rootGroup.blocksRaycasts = visible;
        }

        if (page == PausePage.Home && EventSystem.current != null)
        {
            Button resume = FindChild<Button>("Button_Resume");
            if (resume != null)
            {
                EventSystem.current.SetSelectedGameObject(resume.gameObject);
            }
        }
    }

    private void TransitionToPage(PausePage targetPage)
    {
        if (_page == targetPage)
        {
            return;
        }

        if (_pageTransitionCoroutine != null)
        {
            StopCoroutine(_pageTransitionCoroutine);
        }

        _pageTransitionCoroutine = StartCoroutine(
            PageTransitionRoutine(targetPage));
    }

    private IEnumerator PageTransitionRoutine(PausePage targetPage)
    {
        _pageBeforeTransition = _page;
        _transitioning = true;

        GameObject outgoing = GetPageObject(_pageBeforeTransition);
        CanvasGroup outgoingGroup = outgoing != null
            ? outgoing.GetComponent<CanvasGroup>()
            : null;
        float elapsed = 0f;
        const float fadeOutDuration = 0.08f;

        if (outgoingGroup != null)
        {
            outgoingGroup.interactable = false;
            outgoingGroup.blocksRaycasts = false;
            float startAlpha = outgoingGroup.alpha;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                outgoingGroup.alpha = Mathf.Lerp(
                    startAlpha,
                    0f,
                    EaseInCubic(Mathf.Clamp01(elapsed / fadeOutDuration)));
                yield return null;
            }
        }

        ShowOnly(targetPage, true);
        GameObject incoming = GetPageObject(targetPage);
        CanvasGroup incomingGroup = incoming != null
            ? incoming.GetComponent<CanvasGroup>()
            : null;
        UIPanelFader incomingFader = incoming != null
            ? incoming.GetComponent<UIPanelFader>()
            : null;

        if (incomingFader != null)
        {
            incomingFader.SetVisible(true);
        }
        else if (incomingGroup != null)
        {
            incomingGroup.alpha = 1f;
            incomingGroup.interactable = true;
            incomingGroup.blocksRaycasts = true;
        }

        yield return new WaitForSecondsRealtime(0.18f);
        _transitioning = false;
        _pageTransitionCoroutine = null;
    }

    private GameObject GetPageObject(PausePage page)
    {
        switch (page)
        {
            case PausePage.Home:
                return _pauseHome;
            case PausePage.Settings:
                return _settingsPanel;
            case PausePage.ConfirmRestart:
            case PausePage.ConfirmMainMenu:
                return _confirmationPanel;
            default:
                return null;
        }
    }

    private static void SetPanelVisible(
        GameObject panel,
        bool visible,
        bool immediate)
    {
        if (panel == null)
        {
            return;
        }

        if (!visible && !panel.activeSelf)
        {
            return;
        }

        UIPanelFader fader = panel.GetComponent<UIPanelFader>();
        if (!visible)
        {
            if (fader != null)
            {
                fader.SetVisible(false, true);
            }
            else
            {
                panel.SetActive(false);
            }
        }
        else if (fader != null)
        {
            fader.SetVisible(visible, immediate);
        }
        else
        {
            panel.SetActive(visible);
        }
    }

    private void RestoreGameplayState()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.isGamePaused = false;
        }

        Time.timeScale = Mathf.Max(0.0001f, _timeScaleBeforePause);
        Time.fixedDeltaTime = Mathf.Max(0.0001f, _fixedDeltaBeforePause);

        Cursor.lockState = _cursorLockBeforePause;
        Cursor.visible = _cursorVisibleBeforePause;

        if (_starterInputs != null)
        {
            _starterInputs.cursorLocked = _starterCursorLockedBeforePause;
            _starterInputs.cursorInputForLook = _starterCursorLookBeforePause;
        }

        _page = PausePage.Hidden;
    }

    private IEnumerator EnforceGameplayCursorRoutine()
    {
        if (_cursorLockBeforePause != CursorLockMode.Locked)
        {
            yield break;
        }

        for (int i = 0; i < 8; i++)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (_starterInputs != null)
            {
                _starterInputs.cursorLocked = _starterCursorLockedBeforePause;
                _starterInputs.cursorInputForLook = _starterCursorLookBeforePause;
            }
            yield return null;
        }
    }

    private void PrepareForSceneChange()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.isGamePaused = false;
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _page = PausePage.Hidden;
    }

    private void SetupPauseBlurVolume()
    {
        GameObject volumeObject = new GameObject("PauseMenu_BlurVolume");
        volumeObject.transform.SetParent(transform, false);

        _pauseVolume = volumeObject.AddComponent<Volume>();
        _pauseVolume.isGlobal = true;
        _pauseVolume.priority = 1000f;
        _pauseVolume.weight = 0f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "PauseBlurProfile_Runtime";
        _pauseVolume.profile = profile;

        DepthOfField depthOfField = profile.Add<DepthOfField>(true);
        depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
        depthOfField.gaussianStart.Override(0f);
        depthOfField.gaussianEnd.Override(8f);
        depthOfField.gaussianMaxRadius.Override(blurMaxRadius);
    }

    private void FadeBlur(bool show)
    {
        if (_blurCoroutine != null)
        {
            StopCoroutine(_blurCoroutine);
        }

        _blurCoroutine = StartCoroutine(FadeBlurRoutine(show));
    }

    private IEnumerator FadeBlurRoutine(bool show)
    {
        if (_pauseVolume == null)
        {
            yield break;
        }

        float startWeight = _pauseVolume.weight;
        float targetWeight = show ? 1f : 0f;
        float duration = Mathf.Max(0.01f, blurFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = show
                ? EaseOutCubic(normalized)
                : EaseInCubic(normalized);
            _pauseVolume.weight = Mathf.Lerp(
                startWeight,
                targetWeight,
                eased);
            yield return null;
        }

        _pauseVolume.weight = targetWeight;
        _blurCoroutine = null;
    }

    private void BeginShellTransition(bool opening)
    {
        if (_shellTransitionCoroutine != null)
        {
            StopCoroutine(_shellTransitionCoroutine);
        }

        FadeBlur(opening);
        _shellTransitionCoroutine = StartCoroutine(
            ShellTransitionRoutine(opening));
    }

    private IEnumerator ShellTransitionRoutine(bool opening)
    {
        _transitioning = true;

        if (_rootGroup == null || _homeGroup == null || _homeRect == null)
        {
            if (!opening)
            {
                ShowOnly(PausePage.Hidden, true);
                RestoreGameplayState();
                StartCoroutine(EnforceGameplayCursorRoutine());
            }

            _transitioning = false;
            _shellTransitionCoroutine = null;
            yield break;
        }

        float duration = Mathf.Max(
            0.01f,
            opening ? pauseOpenDuration : pauseCloseDuration);
        float rootStart = _rootGroup.alpha;
        float homeStart = _homeGroup.alpha;
        Vector2 positionStart = _homeRect.anchoredPosition;
        Color backdropStart = _backdrop != null
            ? _backdrop.color
            : Color.clear;
        float elapsed = 0f;

        _rootGroup.interactable = false;
        _rootGroup.blocksRaycasts = opening;
        if (opening)
        {
            _rootGroup.alpha = 0f;
            _homeGroup.alpha = 0f;
            _homeRect.anchoredPosition =
                _homeVisiblePosition + Vector2.left * homeSlideDistance;
            rootStart = 0f;
            homeStart = 0f;
            positionStart = _homeRect.anchoredPosition;
            if (_backdrop != null)
            {
                Color hiddenBackdrop = _backdropVisibleColor;
                hiddenBackdrop.a = 0f;
                _backdrop.color = hiddenBackdrop;
                backdropStart = hiddenBackdrop;
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = opening
                ? EaseOutCubic(normalized)
                : EaseInCubic(normalized);
            float target = opening ? 1f : 0f;

            _rootGroup.alpha = Mathf.Lerp(rootStart, target, eased);
            _homeGroup.alpha = Mathf.Lerp(homeStart, target, eased);
            _homeRect.anchoredPosition = Vector2.Lerp(
                positionStart,
                opening
                    ? _homeVisiblePosition
                    : _homeVisiblePosition + Vector2.left * 30f,
                eased);

            if (_backdrop != null)
            {
                Color targetBackdrop = opening
                    ? _backdropVisibleColor
                    : new Color(
                        _backdropVisibleColor.r,
                        _backdropVisibleColor.g,
                        _backdropVisibleColor.b,
                        0f);
                _backdrop.color = Color.Lerp(
                    backdropStart,
                    targetBackdrop,
                    eased);
            }

            yield return null;
        }

        _rootGroup.alpha = opening ? 1f : 0f;
        _homeGroup.alpha = opening ? 1f : 0f;
        _homeRect.anchoredPosition = _homeVisiblePosition;
        _rootGroup.interactable = opening;
        _rootGroup.blocksRaycasts = opening;

        if (!opening)
        {
            ShowOnly(PausePage.Hidden, true);
            RestoreGameplayState();
            StartCoroutine(EnforceGameplayCursorRoutine());
        }

        _transitioning = false;
        _shellTransitionCoroutine = null;
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - Mathf.Clamp01(value);
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInCubic(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * value;
    }

    private GameObject FindChildObject(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == childName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private T FindChild<T>(string childName) where T : Component
    {
        GameObject child = FindChildObject(childName);
        return child != null ? child.GetComponent<T>() : null;
    }
}

/// <summary>
/// Installs the pause prefab into any scene containing a GameManager. This keeps
/// Tutorial and Level working after future scene merges without serializing a
/// second copy of the menu into each scene.
/// </summary>
public static class AILURONEPauseMenuBootstrap
{
    private const string PausePrefabPath =
        "AILURONE/UI/AILURONE_PauseMenu";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
        {
            return;
        }

        AILURONEPauseMenuBootstrapRunner.Create(scene, PausePrefabPath);
    }
}

public sealed class AILURONEPauseMenuBootstrapRunner : MonoBehaviour
{
    private string _resourcePath;
    private string _targetSceneName;

    public static void Create(Scene targetScene, string resourcePath)
    {
        GameObject runnerObject = new GameObject("AILURONE_PauseMenuBootstrap");
        AILURONEPauseMenuBootstrapRunner runner =
            runnerObject.AddComponent<AILURONEPauseMenuBootstrapRunner>();
        runner._targetSceneName = targetScene.name;
        runner._resourcePath = resourcePath;
        Object.DontDestroyOnLoad(runnerObject);
    }

    private IEnumerator Start()
    {
        float timeoutAt = Time.realtimeSinceStartup + 5f;
        while (GameManager.Instance == null &&
               Time.realtimeSinceStartup < timeoutAt)
        {
            yield return null;
        }

        if (GameManager.Instance == null ||
            SceneManager.GetActiveScene().name != _targetSceneName ||
            FindAnyObjectByType<PauseMenuController>() != null)
        {
            Destroy(gameObject);
            yield break;
        }

        DisableLegacySettingsPanels();
        EnsureEventSystem();

        GameObject prefab = Resources.Load<GameObject>(_resourcePath);
        if (prefab == null)
        {
            Debug.LogError(
                $"[PauseMenu] Missing Resources prefab: {_resourcePath}");
            Destroy(gameObject);
            yield break;
        }

        GameObject instance = Instantiate(prefab);
        instance.name = "AILURONE_PauseMenu_Runtime";
        SceneManager.MoveGameObjectToScene(
            instance,
            SceneManager.GetActiveScene());
        Destroy(gameObject);
    }

    private static void DisableLegacySettingsPanels()
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in transforms)
            {
                if (child.name == "Panel_Settings")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject(
            "EventSystem_Runtime",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(
            eventSystemObject,
            SceneManager.GetActiveScene());
    }
}

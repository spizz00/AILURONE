using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Five-tab settings navigation with a short unscaled-time cross-fade.
/// References are resolved by the stable names emitted by the editor installer,
/// so the same prefab works in the main menu and pause menu.
/// </summary>
public class ControlStyleSettingsMenu : MonoBehaviour
{
    private enum SettingsPage
    {
        Gameplay,
        Controls,
        Audio,
        Display,
        Interface
    }

    [Header("Description")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Transition")]
    [SerializeField] private float pageFadeDuration = 0.12f;

    private static readonly Color ActiveTabColor =
        new Color(0.94f, 0.94f, 0.94f, 1f);
    private static readonly Color ActiveTabTextColor =
        new Color(0.035f, 0.035f, 0.035f, 1f);
    private static readonly Color InactiveTabColor =
        new Color(0f, 0f, 0f, 0f);
    private static readonly Color InactiveTabTextColor =
        new Color(0.48f, 0.48f, 0.48f, 1f);

    private readonly Dictionary<SettingsPage, Button> _tabs =
        new Dictionary<SettingsPage, Button>();
    private readonly Dictionary<SettingsPage, CanvasGroup> _pages =
        new Dictionary<SettingsPage, CanvasGroup>();

    private SettingsPage _activePage;
    private bool _hasActivePage;
    private Coroutine _pageTransition;

    [System.Obsolete("Legacy two-page editor installer compatibility only.")]
    public void Configure(
        Button controlsButton,
        Button audioButton,
        CanvasGroup controlsCanvasGroup,
        CanvasGroup audioCanvasGroup,
        TextMeshProUGUI description)
    {
        _tabs[SettingsPage.Controls] = controlsButton;
        _tabs[SettingsPage.Audio] = audioButton;
        _pages[SettingsPage.Controls] = controlsCanvasGroup;
        _pages[SettingsPage.Audio] = audioCanvasGroup;
        descriptionText = description;
    }

    private void Awake()
    {
        ResolveReferences();
        BindTabs();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindTabs();
        ShowPage(SettingsPage.Gameplay, true);
    }

    private void OnDisable()
    {
        if (_pageTransition != null)
        {
            StopCoroutine(_pageTransition);
            _pageTransition = null;
        }
    }

    public void ShowDescription(string description)
    {
        if (descriptionText != null)
        {
            descriptionText.text = description;
        }
    }

    private void ResolveReferences()
    {
        _tabs[SettingsPage.Gameplay] = FindChild<Button>("Tab_Gameplay");
        _tabs[SettingsPage.Controls] = FindChild<Button>("Tab_Controls");
        _tabs[SettingsPage.Audio] = FindChild<Button>("Tab_Audio");
        _tabs[SettingsPage.Display] = FindChild<Button>("Tab_Display");
        _tabs[SettingsPage.Interface] = FindChild<Button>("Tab_Interface");

        _pages[SettingsPage.Gameplay] = FindChild<CanvasGroup>("Page_Gameplay");
        _pages[SettingsPage.Controls] = FindChild<CanvasGroup>("Page_Controls");
        _pages[SettingsPage.Audio] = FindChild<CanvasGroup>("Page_Audio");
        _pages[SettingsPage.Display] = FindChild<CanvasGroup>("Page_Display");
        _pages[SettingsPage.Interface] = FindChild<CanvasGroup>("Page_Interface");

        if (descriptionText == null)
        {
            descriptionText = FindChild<TextMeshProUGUI>("Text_Description");
        }
    }

    private void BindTabs()
    {
        BindTab(SettingsPage.Gameplay);
        BindTab(SettingsPage.Controls);
        BindTab(SettingsPage.Audio);
        BindTab(SettingsPage.Display);
        BindTab(SettingsPage.Interface);
    }

    private void BindTab(SettingsPage page)
    {
        Button button = GetTab(page);
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ShowPage(page, false));
    }

    private void ShowPage(SettingsPage page, bool immediate)
    {
        CanvasGroup nextPage = GetPage(page);
        CanvasGroup previousPage = _hasActivePage
            ? GetPage(_activePage)
            : null;

        if (nextPage == null)
        {
            return;
        }

        _activePage = page;
        _hasActivePage = true;
        RefreshTabVisuals();
        ShowDescription(GetDefaultDescription(page));

        if (_pageTransition != null)
        {
            StopCoroutine(_pageTransition);
            _pageTransition = null;
        }

        if (immediate || previousPage == null || previousPage == nextPage)
        {
            foreach (KeyValuePair<SettingsPage, CanvasGroup> entry in _pages)
            {
                SetPageState(entry.Value, entry.Key == page);
            }

            SelectFirstControl(nextPage);
            return;
        }

        _pageTransition = StartCoroutine(
            CrossFadePages(previousPage, nextPage));
    }

    private IEnumerator CrossFadePages(
        CanvasGroup previousPage,
        CanvasGroup nextPage)
    {
        nextPage.gameObject.SetActive(true);
        nextPage.alpha = 0f;
        nextPage.interactable = false;
        nextPage.blocksRaycasts = false;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, pageFadeDuration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = t * t * (3f - 2f * t);

            previousPage.alpha = 1f - eased;
            nextPage.alpha = eased;
            yield return null;
        }

        SetPageState(previousPage, false);
        SetPageState(nextPage, true);
        SelectFirstControl(nextPage);
        _pageTransition = null;
    }

    private void RefreshTabVisuals()
    {
        foreach (KeyValuePair<SettingsPage, Button> entry in _tabs)
        {
            SetTabVisual(entry.Value, entry.Key == _activePage);
        }
    }

    private static void SetTabVisual(Button button, bool active)
    {
        if (button == null)
        {
            return;
        }

        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.color = active ? ActiveTabColor : InactiveTabColor;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = active
                ? ActiveTabTextColor
                : InactiveTabTextColor;
        }
    }

    private static void SetPageState(CanvasGroup page, bool visible)
    {
        if (page == null)
        {
            return;
        }

        page.alpha = visible ? 1f : 0f;
        page.interactable = visible;
        page.blocksRaycasts = visible;
        page.gameObject.SetActive(visible);
    }

    private static void SelectFirstControl(CanvasGroup page)
    {
        if (page == null || EventSystem.current == null)
        {
            return;
        }

        Selectable firstControl = page.GetComponentInChildren<Selectable>(true);
        if (firstControl != null)
        {
            EventSystem.current.SetSelectedGameObject(firstControl.gameObject);
        }
    }

    private Button GetTab(SettingsPage page)
    {
        return _tabs.TryGetValue(page, out Button tab) ? tab : null;
    }

    private CanvasGroup GetPage(SettingsPage page)
    {
        return _pages.TryGetValue(page, out CanvasGroup pageGroup)
            ? pageGroup
            : null;
    }

    private T FindChild<T>(string childName) where T : Component
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == childName)
            {
                T component = child.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }
            }
        }

        return null;
    }

    private static string GetDefaultDescription(SettingsPage page)
    {
        switch (page)
        {
            case SettingsPage.Gameplay:
                return "Tune field of view and motion feedback without changing core movement.";
            case SettingsPage.Controls:
                return "Adjust mouse response and vertical-look direction.";
            case SettingsPage.Audio:
                return "Set global master, music, and sound-effect playback levels.";
            case SettingsPage.Display:
                return "Choose window mode, resolution, synchronization, and frame-rate cap.";
            case SettingsPage.Interface:
                return "Control HUD elements that affect aiming clarity.";
            default:
                return string.Empty;
        }
    }
}

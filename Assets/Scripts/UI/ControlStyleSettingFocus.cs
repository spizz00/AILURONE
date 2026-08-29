using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Adds the cyan focus marker and contextual description to one setting row.
/// </summary>
public class ControlStyleSettingFocus : MonoBehaviour,
    ISelectHandler,
    IDeselectHandler,
    IPointerEnterHandler
{
    [SerializeField] private ControlStyleSettingsMenu menu;
    [SerializeField] private Image rowBackground;
    [SerializeField] private Image focusBar;
    [SerializeField, TextArea] private string description;

    private static readonly Color RestingRowColor = new Color(0.04f, 0.04f, 0.04f, 0.35f);
    private static readonly Color FocusedRowColor = new Color(0.025f, 0.12f, 0.11f, 0.72f);

    public void Configure(
        ControlStyleSettingsMenu owner,
        Image background,
        Image accentBar,
        string contextualDescription)
    {
        menu = owner;
        rowBackground = background;
        focusBar = accentBar;
        description = contextualDescription;
        SetFocused(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        SetFocused(true);
        if (menu != null)
        {
            menu.ShowDescription(description);
        }
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetFocused(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }

        SetFocused(true);
        if (menu != null)
        {
            menu.ShowDescription(description);
        }
    }

    private void SetFocused(bool focused)
    {
        if (rowBackground != null)
        {
            rowBackground.color = focused
                ? FocusedRowColor
                : RestingRowColor;
        }

        if (focusBar != null)
        {
            focusBar.gameObject.SetActive(focused);
        }
    }
}

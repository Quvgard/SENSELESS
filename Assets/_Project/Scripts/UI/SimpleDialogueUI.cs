using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ѕростое диалоговое окно-попап дл€ заглушек.
/// ¬ешаетс€ на Canvas, к нему прив€зываем CanvasGroup и Text.
/// </summary>
public class SimpleDialogueUI : MonoBehaviour
{
    public static SimpleDialogueUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private Text bodyText;

    [Header("Timing")]
    [SerializeField] private float defaultAutoHideTime = 3f;

    private float _hideTimer = -1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (panel != null)
            HideImmediate();
    }

    private void Update()
    {
        if (_hideTimer > 0f)
        {
            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f)
                Hide();
        }
    }

    public void ShowMessage(string text, float autoHideTime = -1f)
    {
        if (panel == null || bodyText == null)
        {
            Debug.LogWarning("SimpleDialogueUI: не назначены panel или bodyText.");
            return;
        }

        bodyText.text = text;

        panel.alpha = 1f;
        panel.interactable = true;
        panel.blocksRaycasts = true;

        if (autoHideTime < 0f)
        {
            _hideTimer = defaultAutoHideTime > 0f ? defaultAutoHideTime : -1f;
        }
        else
        {
            _hideTimer = autoHideTime;
        }
    }

    public void Hide()
    {
        if (panel == null)
            return;

        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        _hideTimer = -1f;
    }

    private void HideImmediate()
    {
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        _hideTimer = -1f;
    }
}
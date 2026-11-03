using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TagDisplay : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;

    [Tooltip("Optional. Fill image used by the pie menu while an option is held, when this display is the center element. Leave empty on the option items.")]
    [SerializeField] public Image HoldFill;

    private LocalizedKey localizedKey;
    private List<TagData> multipleTags;

    [Header("Pie center appearance")]
    [SerializeField] Image background;
    [SerializeField] Image gradient1;
    [SerializeField] Image gradient2;
    [SerializeField] TMP_Text inputHintLabel;
    [SerializeField] TMP_Text answerLabel;

    Color backgroundBase = Color.white;
    Color gradient2Base = Color.white;
    bool cached;

    /// <summary>Fill image driven by the pie menu when this display is used as the center element.</summary>
    // public Image HoldFill => holdFill;

    private void Awake()
    {
        EnsureComponents();
    }

    private void Start()
    {
        RefreshMultipleTagsText();
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        if (multipleTags != null && multipleTags.Count > 1)
        {
            RefreshMultipleTagsText();
        }
        else if (localizedKey != null)
        {
            localizedKey.UpdateText();
        }

        if (answerLabel != null && answerLabel.gameObject.activeSelf && label != null)
        {
            answerLabel.text = label.text;
        }
    }

    private void EnsureComponents()
    {
        if (label == null)
        {
            label = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (localizedKey == null)
        {
            localizedKey = GetComponent<LocalizedKey>();
            if (localizedKey == null)
            {
                localizedKey = gameObject.AddComponent<LocalizedKey>();
            }
        }

        if (localizedKey != null && label != null)
        {
            localizedKey.textComponent = label;
        }
    }

    /// <summary>
    /// Sets a single tag data on the display, optionally passing format arguments (e.g. remaining tag count) for {0}, {1}, etc.
    /// </summary>
    public void SetData(TagData data, params object[] formatArgs)
    {
        if (data == null) return;

        EnsureComponents();
        multipleTags = null;

        if (localizedKey != null)
        {
            localizedKey.localizationKey = data.localizationKey;
            localizedKey.SetFormatArguments(formatArgs);
        }
    }

    /// <summary>
    /// Sets multiple tag datas on the display and joins their localized names with a line break.
    /// </summary>
    public void SetMultipleData(IReadOnlyList<TagData> datas)
    {
        if (datas == null || datas.Count == 0)
            return;

        if (datas.Count == 1)
        {
            SetData(datas[0]);
            return;
        }

        EnsureComponents();

        multipleTags = new List<TagData>(datas.Count);
        for (int i = 0; i < datas.Count; i++)
        {
            if (datas[i] != null)
            {
                multipleTags.Add(datas[i]);
            }
        }

        if (localizedKey != null)
        {
            localizedKey.localizationKey = string.Empty;
        }

        RefreshMultipleTagsText();
    }

    private void RefreshMultipleTagsText()
    {
        if (multipleTags == null || multipleTags.Count <= 1 || label == null || LocalizationManager.Instance == null)
            return;

        List<string> localizedNames = new List<string>(multipleTags.Count);
        for (int i = 0; i < multipleTags.Count; i++)
        {
            if (multipleTags[i] != null)
            {
                localizedNames.Add(LocalizationManager.Instance.GetLocalizedValue(multipleTags[i].localizationKey));
            }
        }

        label.text = string.Join("\n", localizedNames);

        if (answerLabel != null && answerLabel.gameObject.activeSelf)
        {
            answerLabel.text = label.text;
        }
    }

    void CacheColors()
    {
        if (cached) return;
        cached = true;
        if (background != null) backgroundBase = background.color;
        if (gradient2 != null) gradient2Base = gradient2.color;
    }

    public void ShowNeutral()
    {
        CacheColors();
        SetActive(gradient1, true);
        SetActive(background, false);
        SetActive(gradient2, false);

        if (label != null) label.gameObject.SetActive(true);
        if (inputHintLabel != null) inputHintLabel.gameObject.SetActive(true);
        if (answerLabel != null) answerLabel.gameObject.SetActive(false);

        if (background != null) background.color = backgroundBase;
        if (gradient2 != null) gradient2.color = gradient2Base;
    }

    public void ShowResult(Color? tint)
    {
        CacheColors();
        SetActive(gradient1, false);
        SetActive(background, true);
        SetActive(gradient2, true);

        if (answerLabel != null)
        {
            if (label != null) answerLabel.text = label.text;
            answerLabel.gameObject.SetActive(true);
        }

        if (label != null) label.gameObject.SetActive(false);
        if (inputHintLabel != null) inputHintLabel.gameObject.SetActive(false);

        if (background != null) background.color = Tinted(backgroundBase, tint);
        if (gradient2 != null) gradient2.color = Tinted(gradient2Base, tint);
    }

    static Color Tinted(Color baseColor, Color? tint)
    {
        if (!tint.HasValue) return baseColor;
        return new Color(tint.Value.r, tint.Value.g, tint.Value.b, baseColor.a);
    }

    static void SetActive(Graphic g, bool active)
    {
        if (g != null) g.gameObject.SetActive(active);
    }
}

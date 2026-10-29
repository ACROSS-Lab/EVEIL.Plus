using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TagDisplay : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;

    [Tooltip("Optional. Fill image used by the pie menu while an option is held, when this display is the center element. Leave empty on the option items.")]
    [SerializeField] Image holdFill;

    private LocalizedKey localizedKey;
    
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
    public Image HoldFill => holdFill;

    private void Awake()
    {
        EnsureComponents();
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

    public void SetData(TagData data)
    {
        if (data == null) return;

        EnsureComponents();

        if (localizedKey != null)
        {
            localizedKey.localizationKey = data.localizationKey;

            if (localizedKey.textComponent != null)
            {
                localizedKey.UpdateText();
            }
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

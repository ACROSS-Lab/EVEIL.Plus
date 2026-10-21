using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TagDisplay : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;

    [Tooltip("Optional. Fill image used by the pie menu while an option is held, when this display is the center element. Leave empty on the option items.")]
    [SerializeField] Image holdFill;

    private LocalizedKey localizedKey;

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
}

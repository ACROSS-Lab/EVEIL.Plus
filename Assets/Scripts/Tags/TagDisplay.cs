using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TagDisplay : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI label;

    private LocalizedKey localizedKey;

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

        if (iconImage == null)
        {
            iconImage = GetComponentInChildren<Image>(true);
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

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
        }

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
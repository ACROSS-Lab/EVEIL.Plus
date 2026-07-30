using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TagDisplay : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI label;

    LocalizedKey localizedKey;

    void Awake()
    {
        localizedKey = GetComponent<LocalizedKey>();
        if (localizedKey == null)
        {
            localizedKey = gameObject.AddComponent<LocalizedKey>();
        }
        localizedKey.textComponent = label;
    }

    public void SetData(TagData data)
    {
        if (data == null) return;

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
        }

        if (localizedKey != null)
        {
            localizedKey.localizationKey = data.localizationKey;
            localizedKey.UpdateText();
        }
    }
}
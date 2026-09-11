using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Attach to the scan UI prefab referenced by PointOfInterest.scanUIPrefab.
// Expects a world-space canvas with a filled Image for the progress bar,
// and optionally a text label for the percentage / "Analyzing source" caption.
public class ScanProgressDisplay : MonoBehaviour
{
    [SerializeField] Image fillImage;
    [SerializeField] TextMeshProUGUI percentageLabel;

    public void SetProgress(float normalizedValue)
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = normalizedValue;
        }

        if (percentageLabel != null)
        {
            percentageLabel.text = Mathf.RoundToInt(normalizedValue * 100f) + "%";
        }
    }
}

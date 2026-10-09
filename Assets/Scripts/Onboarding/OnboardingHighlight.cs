using UnityEngine;

public class OnboardingHighlight : MonoBehaviour
{
    [SerializeField] private GameObject highlightVisual;

    public void Show()
    {
        highlightVisual.SetActive(true);
    }

    public void Hide()
    {
        highlightVisual.SetActive(false);
    }
}
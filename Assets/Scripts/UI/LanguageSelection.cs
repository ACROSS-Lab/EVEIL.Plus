using UnityEngine;
using UnityEngine.UI;

public class LanguageSelection : MonoBehaviour
{
    [SerializeField] Button vietnameseButton;
    [SerializeField] Button englishButton;
    [SerializeField] Button frenchButton;

    void Start()
    {
        vietnameseButton.onClick.AddListener(() => SetLanguage("Vietnamese"));
        englishButton.onClick.AddListener(() => SetLanguage("English"));
        frenchButton.onClick.AddListener(() => SetLanguage("French"));
    }

    void SetLanguage(string language)
    {
        LocalizationManager.Instance.SetLanguage(language);
        SequenceDirector.Instance.PerformAction();
    }
}

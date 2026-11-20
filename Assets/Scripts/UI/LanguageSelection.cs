using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class LanguageSelection : MonoBehaviour
{
    [SerializeField] Button vietnameseButton;
    [SerializeField] Button englishButton;
    [SerializeField] Button frenchButton;

    public static bool isLanguageSelected = false;

    void Start()
    {
        if (isLanguageSelected)
        {
            SequenceDirector.Instance.PerformAction();
            return;
        }

        vietnameseButton.onClick.AddListener(() => SetLanguage("Vietnamese"));
        englishButton.onClick.AddListener(() => SetLanguage("English"));
        frenchButton.onClick.AddListener(() => SetLanguage("French"));
    }

    void SetLanguage(string language)
    {
        LocalizationManager.Instance.SetLanguage(language);
        isLanguageSelected = true;
        SequenceDirector.Instance.PerformAction();
    }
}

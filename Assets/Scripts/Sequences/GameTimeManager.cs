using System.Collections;
using TMPro;
using UnityEngine;

public class GameTimeManager : MonoBehaviour 
{
    [Header("Text Components")]
    [SerializeField] TextMeshProUGUI dayText;
    [SerializeField] TextMeshProUGUI monthText;
    [SerializeField] TextMeshProUGUI hourText;
    [SerializeField] TextMeshProUGUI heightText;

    [Header("Start Value")]
    [SerializeField] GameTime startSimulationTime = new GameTime()
    {
        month = 7,
        day = 23,
        hour = 0
    };
    [SerializeField] float startHeight;

    int lastRenderedDay = -1;
    int lastRenderedMonth = -1;
    int lastRenderedHour = -1;

    void Start()
    {
        UpdateUITexts(startSimulationTime, startHeight);
    }

    void UpdateUITexts(GameTime currentTime, float height)
    {
        if (currentTime.day != lastRenderedDay)
        {
            dayText.text = currentTime.day.ToString("00");
            lastRenderedDay = currentTime.day;
        }

        if (currentTime.month != lastRenderedMonth)
        {
            string key = GameTime.monthKeys[currentTime.month - 1];
            monthText.text = LocalizationManager.Instance.GetLocalizedValue(key);
            lastRenderedMonth = currentTime.month;
        }

        if (currentTime.hour != lastRenderedHour)
        {
            hourText.text = $"{currentTime.hour:D2}h";
            lastRenderedHour = currentTime.hour;
        }

        heightText.text = $"{height:0.00}m";
    }

    public void AdvanceTime(GameTime startTime, GameTime endTime, float startHeight, float endHeight, float duration)
    {
        StartCoroutine(LerpTimeRoutine(startTime, endTime, startHeight, endHeight, duration));
    }

    private IEnumerator LerpTimeRoutine(GameTime startTime, GameTime endTime, float startHeight, float endHeight, float duration)
    {
        GameTime currentTime;

        float startHours = startTime.ToTotalHours();
        float endHours = endTime.ToTotalHours();
        
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float currentTotalHours = Mathf.Lerp(startHours, endHours, t);
            currentTime = GameTime.FromTotalHours(currentTotalHours);

            float currentHeight = Mathf.Lerp(startHeight, endHeight, t);

            UpdateUITexts(currentTime, currentHeight);
            yield return null;
        }

        currentTime = GameTime.FromTotalHours(endHours);
        UpdateUITexts(currentTime, endHeight);
    }

}
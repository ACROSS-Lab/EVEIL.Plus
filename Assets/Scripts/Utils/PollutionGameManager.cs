using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class PollutionGameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI remainingText;

    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Events")]
    public UnityEvent onAllTagged;

    public UnityEvent onAllCorrect;

    private PointOfInterest[] points;

    void Awake()
    {
        points = FindObjectsByType<PointOfInterest>(FindObjectsSortMode.None);

        foreach (PointOfInterest point in points)
        {
            point.OnTagChanged += OnPointChanged;
        }

        UpdateUI();
    }

    void OnDestroy()
    {
        foreach (PointOfInterest point in points)
        {
            point.OnTagChanged -= OnPointChanged;
        }
    }

    void OnPointChanged(PointOfInterest point)
    {
        UpdateUI();

        if (RemainingCount() == 0)
        {
            onAllTagged?.Invoke();

            if (AllCorrect())
                onAllCorrect?.Invoke();
        }
    }

    void UpdateUI()
    {
        if (remainingText != null)
            remainingText.text = RemainingCount().ToString();

        if (scoreText != null)
            scoreText.text = $"{CorrectCount()} / {points.Length}";
    }

    public int RemainingCount()
    {
        int remaining = 0;

        foreach (PointOfInterest point in points)
        {
            if (!point.HasTag)
                remaining++;
        }

        return remaining;
    }

    public int CorrectCount()
    {
        int correct = 0;

        foreach (PointOfInterest point in points)
        {
            if (point.IsCorrect)
                correct++;
        }

        return correct;
    }

    public bool AllCorrect()
    {
        return CorrectCount() == points.Length;
    }
}
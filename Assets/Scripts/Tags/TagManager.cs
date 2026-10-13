using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TagManager : MonoBehaviour
{
    [SerializeField] PointOfInterest[] pointsOfInterest;

    [Header("Events")]
    public UnityEvent onCompleted;
    public UnityEvent<int, int> onProgressChanged;
    public UnityEvent onFirstCorrectValidation;
    public UnityEvent onIncorrectValidation;
    public UnityEvent onPointScanned;
    [Tooltip("Fired whenever any progress or interaction occurs (source scanned, tag validated, or button pressed).")]
    public UnityEvent onPlayerProgressMade;

    bool isCompleted = false;
    bool firstCorrectValidationOccurred = false;

    public int TotalCount => pointsOfInterest != null ? pointsOfInterest.Length : 0;
    public bool IsCompleted => isCompleted;
    public bool HasUnscannedSources => ScannedCount() < TotalCount;
    public bool AreAllSourcesScanned => TotalCount > 0 && ScannedCount() >= TotalCount;
    public bool HasIncorrectOrUnsolvedTags => RemainingCount() > 0;
    public bool AreAllTagsValidated => TotalCount > 0 && SolvedCount() >= TotalCount;

    void Awake()
    {
        if (pointsOfInterest == null || pointsOfInterest.Length == 0)
        {
            pointsOfInterest = FindObjectsByType<PointOfInterest>(FindObjectsSortMode.None);
        }

        foreach (PointOfInterest point in pointsOfInterest)
        {
            if (point != null)
            {
                point.OnScanCompleted += OnPointScanned;
                point.OnTagValidated += OnPointValidated;
                point.OnValidateButtonPressed += OnValidateButtonPressed;
            }
        }

    }

    void OnDestroy()
    {
        if (pointsOfInterest != null)
        {
            foreach (PointOfInterest point in pointsOfInterest)
            {
                if (point != null)
                {
                    point.OnScanCompleted -= OnPointScanned;
                    point.OnTagValidated -= OnPointValidated;
                    point.OnValidateButtonPressed -= OnValidateButtonPressed;
                }
            }
        }
    }

    void OnPointScanned(PointOfInterest point)
    {
        onPointScanned?.Invoke();
        onPlayerProgressMade?.Invoke();
    }

    void OnPointValidated(PointOfInterest point)
    {
        int remaining = RemainingCount();
        int solved = SolvedCount();

        onProgressChanged?.Invoke(solved, TotalCount);
        onPlayerProgressMade?.Invoke();

        if (remaining == 0 && !isCompleted)
        {
            isCompleted = true;
            onCompleted?.Invoke();
        }
    }

    void OnValidateButtonPressed(PointOfInterest point)
    {
        onPlayerProgressMade?.Invoke();

        if (point.IsCorrect && !firstCorrectValidationOccurred)
        {
            firstCorrectValidationOccurred = true;
            onFirstCorrectValidation?.Invoke();
        }
        else if (!point.IsCorrect)
        {
            onIncorrectValidation?.Invoke();
        }
    }

    public int ScannedCount()
    {
        if (pointsOfInterest == null) return 0;

        int count = 0;
        foreach (PointOfInterest point in pointsOfInterest)
        {
            if (point != null && point.IsScanned)
                count++;
        }
        return count;
    }

    public int UnscannedCount()
    {
        return TotalCount - ScannedCount();
    }

    public int SolvedCount()
    {
        if (pointsOfInterest == null) return 0;

        int count = 0;
        foreach (PointOfInterest point in pointsOfInterest)
        {
            if (point != null && point.IsTagValidated && point.IsCorrect)
                count++;
        }
        return count;
    }

    public int RemainingCount()
    {
        return TotalCount - SolvedCount();
    }
}

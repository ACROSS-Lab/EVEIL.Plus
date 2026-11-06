using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TagManager : MonoBehaviour
{
    [SerializeField] PointOfInterest[] pointsOfInterest;

    public int remainingSourcesToTag;

    [Header("Events")]
    public UnityEvent onCompleted;
    // public UnityEvent<int, int> onProgressChanged;
    public UnityEvent onFirstCorrectValidation;
    public UnityEvent onIncorrectValidation;
    public UnityEvent onPointScanned;
    [Tooltip("Fired whenever any progress or interaction occurs (source scanned, tag validated, or button pressed).")]
    public UnityEvent onPlayerProgressMade;
    public bool tagThresholdEnabled;
    public int remainingSourcesThreshold;
    [Tooltip("Fired whenever player has tagged enough sources to pass the remainingSourcesThreshold")]
    public UnityEvent onTagThresholdReached;
    [Tooltip("Fired the first time player finds the 1st valid tag of a source that requires 2+ validtags")]
    public UnityEvent onFirstPartialTagFound;
    
    bool isCompleted = false;
    bool firstCorrectValidationOccurred = false;
    bool tagThresholdReached = false;
    bool firstPartialTagFound = false;

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
                point.OnFirstPartialTagFound += OnFirstPartialTagFound;
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
                    point.OnFirstPartialTagFound -= OnFirstPartialTagFound;
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
        remainingSourcesToTag = RemainingCount();

        // int solved = SolvedCount();
        // onProgressChanged?.Invoke(solved, TotalCount);
        
        onPlayerProgressMade?.Invoke();

        if (remainingSourcesToTag == 0 && !isCompleted)
        {
            isCompleted = true;
            onCompleted?.Invoke();
        }

        if (tagThresholdEnabled && !tagThresholdReached && remainingSourcesToTag <= remainingSourcesThreshold)
        {
            tagThresholdReached = true;
            onTagThresholdReached?.Invoke();
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
        else if (!point.IsLastSelectionCorrect)
        {
            onIncorrectValidation?.Invoke();
        }
    }
    
    void OnFirstPartialTagFound(PointOfInterest point)
    {
        if (firstPartialTagFound)
            return;

        firstPartialTagFound = true;
        onFirstPartialTagFound?.Invoke();
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

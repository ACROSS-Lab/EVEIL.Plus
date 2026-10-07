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
    public UnityEvent onFirstIncorrectValidation;


    bool isCompleted = false;
    bool firstCorrectValidationOccurred = false;
    bool firstIncorrectValidationOccurred = false;

    public int TotalCount => pointsOfInterest != null ? pointsOfInterest.Length : 0;
    public bool IsCompleted => isCompleted;

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
                    point.OnTagValidated -= OnPointValidated;
                    point.OnValidateButtonPressed -= OnValidateButtonPressed;
                }
            }
        }
    }

    void OnPointValidated(PointOfInterest point)
    {
        int remaining = RemainingCount();
        int solved = SolvedCount();

        onProgressChanged?.Invoke(solved, TotalCount);

        if (remaining == 0 && !isCompleted)
        {
            isCompleted = true;
            onCompleted?.Invoke();
        }
    }

    void OnValidateButtonPressed(PointOfInterest point)
    {
        if (point.IsCorrect && !firstCorrectValidationOccurred)
        {
            firstCorrectValidationOccurred = true;
            onFirstCorrectValidation?.Invoke();
        }
        else if (!point.IsCorrect && !firstIncorrectValidationOccurred)
        {
            firstIncorrectValidationOccurred = true;
            onFirstIncorrectValidation?.Invoke();
        }
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

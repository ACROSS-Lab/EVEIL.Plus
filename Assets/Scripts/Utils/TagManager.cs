using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TagManager : MonoBehaviour
{
    [Header("Points of Interest")]
    [Tooltip("If left empty, all PointOfInterest components in the scene will be automatically detected.")]
    [SerializeField] PointOfInterest[] points;

    [Header("UI (Optional)")]
    [Tooltip("Displays remaining unvalidated sources. Passes remaining count as format argument {0}.")]
    [SerializeField] LocalizedKey remainingText;

    [Header("Events")]
    [Tooltip("Invoked when all points of interest have been correctly validated.")]
    public UnityEvent onCompleted;

    [Tooltip("Invoked whenever a tag is validated. Passes (solvedCount, totalCount).")]
    public UnityEvent<int, int> onProgressChanged;

    [Header("Idle Hints")]
    [SerializeField] bool hintsActivated = true;
    [SerializeField] float idleThreshold = 120f;
    [Tooltip("If the player is still idle after a hint, wait this long before hinting again.")]
    [SerializeField] float repeatInterval = 60f;

    [Tooltip("Movable objects to highlight using HoverLiftEffect")]
    [SerializeField] HoverLiftEffect[] movableObjectHighlights;
    [Tooltip("Movable objects to highlight using GrabInteractable")]
    [SerializeField] GrabInteractable[] grabbableBlockHighlights;
    [Tooltip("Additional objects to highlight (magnifying glass, tools)")]
    [SerializeField] HighlightPulse[] toolHighlights;
    [Tooltip("UI to highlight")]
    [SerializeField] HighlightPulse[] tagWindowHighlights;

    bool isCompleted = false;
    float idleTimer = 0f;

    public int TotalCount => points != null ? points.Length : 0;
    public bool IsCompleted => isCompleted;

    void Awake()
    {
        if (points == null || points.Length == 0)
        {
            points = FindObjectsByType<PointOfInterest>(FindObjectsSortMode.None);
        }

        foreach (PointOfInterest point in points)
        {
            if (point != null)
            {
                point.OnTagValidated += OnPointValidated;
            }
        }

        UpdateRemainingUI();
    }

    void Update()
    {
        if (isCompleted || !hintsActivated)
            return;

        idleTimer += Time.deltaTime;

        if (idleTimer >= idleThreshold)
        {
            TriggerNeedsTaggingHint();
            idleTimer = idleThreshold - repeatInterval;
        }
    }

    void OnDestroy()
    {
        if (points != null)
        {
            foreach (PointOfInterest point in points)
            {
                if (point != null)
                {
                    point.OnTagValidated -= OnPointValidated;
                }
            }
        }
    }

    void OnPointValidated(PointOfInterest point)
    {
        // Reset idle timer on player progress
        idleTimer = 0f;

        int remaining = RemainingCount();
        int solved = SolvedCount();

        UpdateRemainingUI();
        onProgressChanged?.Invoke(solved, TotalCount);

        if (remaining == 0 && !isCompleted)
        {
            isCompleted = true;
            onCompleted?.Invoke();
        }
    }

    public int SolvedCount()
    {
        if (points == null) return 0;

        int count = 0;
        foreach (PointOfInterest point in points)
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

    public void UpdateRemainingUI()
    {
        if (remainingText != null)
        {
            remainingText.SetFormatArguments(RemainingCount());
        }
    }

    void TriggerNeedsTaggingHint()
    {
        if (movableObjectHighlights != null)
        {
            foreach (var obj in movableObjectHighlights)
                if (obj != null) obj.PulseHighlight();
        }

        if (grabbableBlockHighlights != null)
        {
            foreach (var block in grabbableBlockHighlights)
                if (block != null) block.PulseHighlight();
        }

        if (toolHighlights != null)
        {
            foreach (var tool in toolHighlights)
                if (tool != null) tool.Pulse();
        }

        if (tagWindowHighlights != null)
        {
            foreach (var window in tagWindowHighlights)
                if (window != null) window.Pulse();
        }
    }
}

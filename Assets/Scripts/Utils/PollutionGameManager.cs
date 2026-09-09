using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public enum ProgressState
{
    NeedsTagging,
    ReadyToValidate,
    HasErrors,
    Completed
}

public class PollutionGameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private LocalizedKey remainingText;

    [Header("Validation")]
    [SerializeField] private GameObject validationButton;
    [SerializeField] private GameObject validationPanel;
    [SerializeField] private LocalizedKey errorText;

    [Header("Events")]
    public UnityEvent onAllTagged;
    public UnityEvent onCompleted;

    [Header("Idle Hints")]
    [SerializeField] private bool hintsActivated = true;
    [SerializeField] private float idleThreshold = 120f;
    [Tooltip("If the player is still idle after a hint, wait this long before hinting again.")]
    [SerializeField] private float repeatInterval = 60f;
    
    //TODO chose one method of grabing / highlighting
    [Tooltip("Movable objects to highlight using HoverLiftEffect")]
    [SerializeField] private HoverLiftEffect[] movableObjectHighlights;
    [Tooltip("Movable objects to highlight using GrabInteractable")]
    [SerializeField] private GrabInteractable[] grabbableBlockHighlights;
    [Tooltip("Additional objects to highlight (magnifying glass, tools)")]
    [SerializeField] private HighlightPulse[] toolHighlights;
    [Tooltip("UI to highlight")]
    [SerializeField] private HighlightPulse[] tagWindowHighlights;
    
    // [SerializeField] private Narrator narrator;
    // [SerializeField] private string needsTaggingHintKey;
    // [SerializeField] private int hintBodyState, hintEyesState, hintMouthState;
    // [SerializeField] private bool hintUsesOverlay = false;
    
    private PointOfInterest[] points;
    
    private bool isCompleted = false;
    private float idleTimer = 0f;

    private void Awake()
    {
        points = FindObjectsByType<PointOfInterest>(
            FindObjectsSortMode.None
        );

        foreach (PointOfInterest point in points)
        {
            point.OnTagChanged += OnPointChanged;
        }

        // Hide the validation panel and button at the beginning.
        if (validationPanel != null)
            validationPanel.SetActive(false);

        if (validationButton != null)
            validationButton.SetActive(false);

        UpdateRemainingUI();
    }
    
    private void Update()
    {
        if (isCompleted)
            return;
        
        if (GetProgressState() != ProgressState.NeedsTagging)
        {
            idleTimer = 0f;
            return;
        }
        
        // (deactivated for now bs not really well design yet)
        // if (narrator != null && narrator.IsTalking)
        //     return;
 
        if (!hintsActivated)
        {
            return;
        }
        
        idleTimer += Time.deltaTime;
 
        if (idleTimer >= idleThreshold)
        {
            TriggerNeedsTaggingHint();
            idleTimer = idleThreshold - repeatInterval;
        }
    }

    private void OnDestroy()
    {
        foreach (PointOfInterest point in points)
        {
            point.OnTagChanged -= OnPointChanged;
        }
    }

    private void OnPointChanged(PointOfInterest point)
    {
        UpdateRemainingUI();

        // The validation button only becomes available
        // once every source has been tagged at least once.
        if (RemainingCount() == 0)
        {
            if (validationButton != null)
                validationButton.SetActive(true);

            onAllTagged?.Invoke();
        }
    }

    private void UpdateRemainingUI()
    {
        if (remainingText != null)
        {
            remainingText.SetFormatArguments(
                RemainingCount()
            );
        }
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

    public int ErrorCount()
    {
        int errors = 0;

        foreach (PointOfInterest point in points)
        {
            if (!point.IsCorrect)
                errors++;
        }

        return errors;
    }
    
    public ProgressState GetProgressState()
    {
        if (isCompleted)
            return ProgressState.Completed;
 
        if (RemainingCount() > 0)
            return ProgressState.NeedsTagging;
 
        if (ErrorCount() > 0)
            return ProgressState.HasErrors;
 
        return ProgressState.ReadyToValidate;
    }

    public void ValidateAnswers()
    {
        // Do not allow validation before every source has been tagged.
        if (RemainingCount() > 0)
            return;

        int errors = ErrorCount();

        // Show the validation result only after the player
        // explicitly presses the validation button.
        if (validationPanel != null)
            validationPanel.SetActive(true);

        if (errorText != null)
        {
            errorText.SetFormatArguments(errors);
        }

        // All answers are correct.
        if (errors == 0)
        {
            onCompleted?.Invoke();
        }
    }

    public void CloseValidationPanel()
    {
        if (validationPanel != null)
            validationPanel.SetActive(false);
    }
    
    private void TriggerNeedsTaggingHint()
    {
        foreach (var obj in movableObjectHighlights)
            if (obj != null) obj.PulseHighlight();
        
        foreach (var block in grabbableBlockHighlights)
            if (block != null) block.PulseHighlight();
 
        foreach (var tool in toolHighlights)
            if (tool != null) tool.Pulse();
 
        foreach (var window in tagWindowHighlights)
            if (window != null) window.Pulse();
 
        // if (narrator != null && !string.IsNullOrEmpty(needsTaggingHintKey))
        // {
        //     StartCoroutine(PlayHintDialogue());
        // }
    }
 
    
    // private IEnumerator PlayHintDialogue()
    // {
    //     float talkingTime = narrator.StartTalking(
    //         needsTaggingHintKey,
    //         hintBodyState,
    //         hintEyesState,
    //         hintMouthState,
    //         hintUsesOverlay
    //     );
    //
    //     yield return new WaitForSeconds(talkingTime);
    //
    //     narrator.FinishDialogue(hintBodyState, hintMouthState);
    //     narrator.DisableDialogueBox();
    // }
}
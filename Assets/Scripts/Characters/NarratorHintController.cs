using NaughtyAttributes;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class NarratorHintController : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Reference to the level TagManager. Automatically located if left empty.")]
    [SerializeField] TagManager tagManager;

    [Tooltip("Optional reference to DetectorController to suppress/cancel scans when interacting with the Narrator.")]
    [SerializeField] DetectorController detectorController;

    [Header("Sequence Tracks")]
    [Tooltip("Track 1: Reminds the player to click the narrator for hints if inactive for a while.")]
    [SerializeField] SequenceTrack inactivityReminderTrack;

    [Tooltip("Track 2: Hint played when there are still unscanned sources.")]
    [SerializeField] SequenceTrack unscannedSourcesTrack;

    [Tooltip("Track 3: Hint played when all sources are scanned, but tags are wrong or unsolved.")]
    [SerializeField] SequenceTrack checkSourcesAgainTrack;

    [Header("Inactivity Reminder Settings")]
    [Tooltip("Whether the automatic reminder timer is active.")]
    [SerializeField] bool enableInactivityReminder = true;

    [Tooltip("Time in seconds without any scan or tag validation before the reminder track triggers.")]
    [SerializeField] float inactivityThreshold = 45f;

    [Header("Interaction Settings")]
    [Tooltip("Minimum time in seconds between manual hint clicks to prevent spamming.")]
    [SerializeField] float hintCooldown = 2f;

    [Header("Debug Status")]
    [ShowNonSerializedField] float currentInactivityTimer = 0f;
    [ShowNonSerializedField] float lastHintTime = -10f;

    Narrator narrator;
    XRBaseInteractable interactable;

    void Awake()
    {
        narrator = GetComponent<Narrator>();
        interactable = GetComponent<XRSimpleInteractable>();

        if (tagManager == null)
        {
            tagManager = FindFirstObjectByType<TagManager>();
        }

        if (detectorController == null)
        {
            detectorController = FindFirstObjectByType<DetectorController>();
        }
    }

    void OnEnable()
    {
        if (tagManager != null)
        {
            tagManager.onPlayerProgressMade.AddListener(ResetInactivityTimer);
        }

        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnInteractableSelected);
        }
    }

    void OnDisable()
    {
        if (tagManager != null)
        {
            tagManager.onPlayerProgressMade.RemoveListener(ResetInactivityTimer);
        }

        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnInteractableSelected);
        }
    }

    void Update()
    {
        UpdateInactivityTimer();
    }

    void UpdateInactivityTimer()
    {
        if (!enableInactivityReminder) return;

        if (tagManager != null && tagManager.IsCompleted) return;
        if (narrator != null && narrator.IsTalking) return;

        currentInactivityTimer += Time.deltaTime;

        if (currentInactivityTimer >= inactivityThreshold)
        {
            TriggerInactivityReminder();
        }
    }

    void TriggerInactivityReminder()
    {
        currentInactivityTimer = 0f;

        if (inactivityReminderTrack != null)
        {
            inactivityReminderTrack.TriggerAction();
        }
        else
        {
            Debug.LogWarning("[NarratorHintController] Inactivity reminder track is not assigned.");
        }
    }

    public void ResetInactivityTimer()
    {
        currentInactivityTimer = 0f;
    }

    public void SetInactivityReminderEnabled(bool enabled)
    {
        enableInactivityReminder = enabled;
        if (!enabled)
        {
            currentInactivityTimer = 0f;
        }
    }

    void OnInteractableSelected(SelectEnterEventArgs args)
    {
        RequestHint();
    }

    public void RequestHint()
    {
        if (detectorController != null)
        {
            detectorController.CancelScan(disrupted: false);
        }

        if (tagManager != null && tagManager.IsCompleted) return;

        if (Time.time < lastHintTime + hintCooldown) return;

        if (narrator != null && narrator.IsTalking) return;

        lastHintTime = Time.time;
        ResetInactivityTimer();

        if (tagManager == null)
        {
            Debug.LogWarning("[NarratorHintController] TagManager reference is missing.");
            return;
        }

        if (tagManager.HasUnscannedSources)
        {
            if (unscannedSourcesTrack != null)
            {
                unscannedSourcesTrack.TriggerAction();
            }
            else
            {
                Debug.LogWarning("[NarratorHintController] Unscanned sources track is not assigned.");
            }
        }
        else if (tagManager.HasIncorrectOrUnsolvedTags)
        {
            if (checkSourcesAgainTrack != null)
            {
                checkSourcesAgainTrack.TriggerAction();
            }
            else
            {
                Debug.LogWarning("[NarratorHintController] Check sources again track is not assigned.");
            }
        }
    }

    /// <summary>
    /// Alias for RequestHint for intuitive UnityEvent binding in the Inspector.
    /// </summary>
    public void OnNarratorClicked()
    {
        RequestHint();
    }
}

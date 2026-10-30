using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

public enum InterruptionDiscardMode
{
    [Tooltip("Keep the interrupted step and push it back to resume after this track.")]
    KeepCurrentStep = 0,
    [Tooltip("Discard only the step currently playing, keep whatever was queued after it.")]
    DiscardCurrentStepOnly = 1,
    [Tooltip("Discard the current step AND all subsequent steps in the queue (full override/branch).")]
    DiscardAllRemainingSteps = 2,
}

public enum StepSelectionMode
{
    [Tooltip("Add all steps in the list in order")]
    All = 0,
    [Tooltip("Pick one random step from the list")]
    RandomOne = 1,
}

public class SequenceTrack : MonoBehaviour
{
    public enum ExecutionMode
    {
        [Tooltip("Play immediately after current step finishes")]
        Next = 0,
        [Tooltip("Add to the end of the entire queue")]
        End = 1,
        [Tooltip("Interrupt the current step immediately and play these steps")]
        Immediate = 2,
    }

    [Header("Trigger")]
    [SerializeField] bool triggerOnStart = false;
    [HideIf("triggerOnStart")]
    [SerializeField] bool canBeRepeated = false;

    [Header("List of adding steps")]
    [Tooltip("How steps from this track are chosen when triggered.")]
    [SerializeField] StepSelectionMode selectionMode = StepSelectionMode.All;

    [ShowIf("isRandom")]
    [Tooltip("If true, avoids picking the exact same step consecutively when this track is triggered multiple times.")]
    [SerializeField] bool avoidConsecutiveRepeat = true;

    [SerializeField] List<SequenceStep> sequenceSteps;

    [SerializeField] ExecutionMode insertMode = ExecutionMode.Next;

    [ShowIf("isImmediate")]
    [Tooltip("How to handle the currently playing step and any subsequent queued steps when interrupting.")]
    [SerializeField] InterruptionDiscardMode discardMode = InterruptionDiscardMode.DiscardCurrentStepOnly;

    // Persists across all scene transitions during this play session
    static readonly HashSet<string> triggeredTracks = new HashSet<string>();

    int lastRandomIndex = -1;

    string trackId => $"{gameObject.scene.name}_{gameObject.name}";
    bool isRepeatable => !triggerOnStart && canBeRepeated;
    bool isImmediate => insertMode == ExecutionMode.Immediate;
    bool isRandom => selectionMode == StepSelectionMode.RandomOne;

    // Resets static tracking when entering Play Mode in the Unity Editor
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetTriggeredTracks() => triggeredTracks.Clear();

    void Start()
    {
        if (triggerOnStart)
        {
            TriggerAction();
        }
    }

    public void TriggerAction()
    {
        if (!isRepeatable && triggeredTracks.Contains(trackId)) return;

        if (SequenceDirector.Instance == null) return;

        switch (insertMode)
        {
            case ExecutionMode.Immediate:
                InterruptAndPlay();
                break;
            case ExecutionMode.Next:
                Insert();
                break;
            case ExecutionMode.End:
                Append();
                break;
        }

        triggeredTracks.Add(trackId);
    }

    List<SequenceStep> GetStepsToExecute()
    {
        if (sequenceSteps == null || sequenceSteps.Count == 0) return null;

        if (selectionMode == StepSelectionMode.RandomOne)
        {
            if (sequenceSteps.Count == 1)
            {
                return sequenceSteps[0] != null ? new List<SequenceStep> { sequenceSteps[0] } : null;
            }

            int index = Random.Range(0, sequenceSteps.Count);
            if (avoidConsecutiveRepeat && index == lastRandomIndex)
            {
                // Shift to a different index within [0, Count-1]
                index = (index + 1 + Random.Range(0, sequenceSteps.Count - 1)) % sequenceSteps.Count;
            }
            lastRandomIndex = index;

            SequenceStep chosenStep = sequenceSteps[index];
            return chosenStep != null ? new List<SequenceStep> { chosenStep } : null;
        }

        return sequenceSteps;
    }

    void InterruptAndPlay()
    {
        if (SequenceDirector.Instance != null)
        {
            var steps = GetStepsToExecute();
            if (steps != null && steps.Count > 0)
            {
                SequenceDirector.Instance.InterruptAndPlaySteps(steps, discardMode);
            }
        }
    }

    void Insert()
    {
        if (SequenceDirector.Instance != null)
        {
            var steps = GetStepsToExecute();
            if (steps != null && steps.Count > 0)
            {
                SequenceDirector.Instance.InsertNextSteps(steps);
            }
        }
    }

    void Append()
    {
        if (SequenceDirector.Instance != null)
        {
            var steps = GetStepsToExecute();
            if (steps != null && steps.Count > 0)
            {
                SequenceDirector.Instance.EnqueueSteps(steps);
            }
        }
    }
}

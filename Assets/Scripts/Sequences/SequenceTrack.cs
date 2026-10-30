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
    [SerializeField] List<SequenceStep> sequenceSteps;

    [SerializeField] ExecutionMode insertMode = ExecutionMode.Next;

    [ShowIf("isImmediate")]
    [Tooltip("How to handle the currently playing step and any subsequent queued steps when interrupting.")]
    [SerializeField] InterruptionDiscardMode discardMode = InterruptionDiscardMode.DiscardCurrentStepOnly;

    // Persists across all scene transitions during this play session
    static readonly HashSet<string> triggeredTracks = new HashSet<string>();

    string trackId => $"{gameObject.scene.name}_{gameObject.name}";
    bool isRepeatable => !triggerOnStart && canBeRepeated;
    bool isImmediate => insertMode == ExecutionMode.Immediate;

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

    void InterruptAndPlay()
    {
        if (SequenceDirector.Instance != null)
        {
            SequenceDirector.Instance.InterruptAndPlaySteps(sequenceSteps, discardMode);
        }
    }

    void Insert()
    {
        if (SequenceDirector.Instance != null)
        {
            SequenceDirector.Instance.InsertNextSteps(sequenceSteps);
        }
    }

    void Append()
    {
        if (SequenceDirector.Instance != null)
        {
            SequenceDirector.Instance.EnqueueSteps(sequenceSteps);
        }
    }
}

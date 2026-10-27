using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

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
    [Tooltip("If true, the current step being interrupted will be discarded. If false, it will resume after this track finishes.")]
    [SerializeField] bool discardCurrentStep = true;

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
            SequenceDirector.Instance.InterruptAndPlaySteps(sequenceSteps, discardCurrentStep);
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

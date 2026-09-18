using System.Collections.Generic;
using UnityEngine;

public class SequenceTrack : MonoBehaviour
{
    public enum ExecutionMode
    {
        [Tooltip("Play immediately after current step finishes")]
        Next,
        [Tooltip("Add to the end of the entire queue")]
        End,
    }

    [Header("List of adding steps")]
    [SerializeField] List<SequenceStep> sequenceSteps;

    [Header("Auto Trigger")]
    [SerializeField] bool triggerOnStart = false;
    [SerializeField] ExecutionMode insertMode = ExecutionMode.Next;

    bool isTriggered = false;

    void Start()
    {
        if (triggerOnStart)
        {
            TriggerAction();
        }
    }

    public void TriggerAction()
    {
        if (isTriggered == true) return;
        
        switch (insertMode)
        {
            case ExecutionMode.Next:
                Insert();
                break;
            case ExecutionMode.End:
                Append();
                break;
        }
    }

    void Insert()
    {
        if (SequenceDirector.Instance != null)
        {
            SequenceDirector.Instance.InsertNextSteps(sequenceSteps);
            isTriggered = true;
        }
    }

    void Append()
    {
        if (SequenceDirector.Instance != null)
        {
            SequenceDirector.Instance.EnqueueSteps(sequenceSteps);
            isTriggered = true;
        }
    }
}
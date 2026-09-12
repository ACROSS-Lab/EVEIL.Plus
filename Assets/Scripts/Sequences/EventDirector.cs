using System.Collections;
using TMPro;
using UnityEngine;

public class EventDirector : MonoBehaviour
{
    public SequenceEvent currentSequenceEvent { get; private set; }

    SequenceEvent[] sequenceEvents;
    
    void Awake()
    {
        sequenceEvents = GetComponentsInChildren<SequenceEvent>();
    }

    public void SetCurrentSequenceEvent(string stepId)
    {
        foreach (SequenceEvent sequenceEvent in sequenceEvents)
        {
            if (string.Equals(sequenceEvent.name, stepId))
            {        
                currentSequenceEvent = sequenceEvent;
                break;
            }
        }
    }

    public void TriggerEventsForStep()
    {
        currentSequenceEvent.TriggerEvents();
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class SequenceEvent : MonoBehaviour
{
    public List<EventsWithDelay> eventsWithDelay;
    [ShowIf("hasController")] [Dropdown("GetAnimationIndexes")] public int animationIndex;
    [ShowIf("hasController")] public float simulationDelayStart = 0;
    [ShowIf("hasController")] public GameTime startTime, endTime;
    [ShowIf("hasController")] public float startHeight, endHeight;


    public void TriggerEvents()
    {
        if (eventsWithDelay != null)
        {
            foreach (EventsWithDelay eventConfig in eventsWithDelay)
            {
                StartCoroutine(InvokeEventWithDelay(eventConfig.unityEvent, eventConfig.delay));
            }
        }
    }

    IEnumerator InvokeEventWithDelay(UnityEvent unityEvent, float delay)
    {
        yield return new WaitForSeconds(delay);
        unityEvent?.Invoke();
    }
}

[Serializable]
public class EventsWithDelay
{
    public float delay;
    public UnityEvent unityEvent;
}
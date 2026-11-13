using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

public class StartActionManager : MonoBehaviour
{
    [SerializeField] StartActions[] lobbyStartActions;

    void Start()
    {
        int progressCount = SequenceDirector.Instance.GetCompletedDioramaCount();
        foreach (var action in lobbyStartActions)
        {
            if (action.progressCount == progressCount)
            {
                action.onStartActions?.Invoke();
                break;
            }
        }
    }
}

[Serializable]
public class StartActions
{
    public int progressCount;
    public UnityEvent onStartActions;
}
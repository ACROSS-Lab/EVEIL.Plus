using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

public class LobbyManager : MonoBehaviour
{
    [SerializeField] LobbyStartActions[] lobbyStartActions;
    [ShowNonSerializedField] int currentProgressCount;

    void Start()
    {
        int progressCount = SequenceDirector.Instance.GetCompletedDioramaCount();
        currentProgressCount = progressCount;

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
public class LobbyStartActions
{
    public int progressCount;
    public UnityEvent onStartActions;
}
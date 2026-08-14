using UnityEngine;
using NaughtyAttributes;

[CreateAssetMenu(fileName = "Sequence Step", menuName = "Sequence Step")]
public class SequenceStep : ScriptableObject
{
    public bool hasSequenceEvents;

    [Header("Phase 1: Movement")]
    public bool hasNarratorMovement;
    [ShowIf("hasNarratorMovement")] public Vector3 targetNarratorPosition;
    [ShowIf("hasNarratorMovement")] public Vector3 offsetAtCenter;
    [ShowIf("hasNarratorMovement")] public bool hasNarratorRotation;
    [ShowIf("hasNarratorRotation")] public Vector3 targetNarratorRotation;
    [ShowIf("hasNarratorMovement")] public float targetNarratorScale;
    [ShowIf("hasNarratorMovement")] public float flyDuration;

    public bool hasPlayerMovement;
    [ShowIf("hasPlayerMovement")] public Vector3 playerTargetPosition;
    [ShowIf("hasPlayerMovement")] public bool hasPlayerRotation;
    [ShowIf("hasPlayerRotation")] public Vector3 playerTargetRotation;
    [ShowIf("hasPlayerMovement")] public bool hasSceneTransition;
    [ShowIf("hasSceneTransition")] public string sceneName;
    [ShowIf("hasSceneTransition")] public bool isGoingBackToMainScene;

    [Header("Phase 2: Presentation")]
    public bool hasDialogue;
    [ShowIf("hasDialogue")] public CharacterStatesConfig characterStates;
    [ShowIf("hasDialogue")] public float timeWaitBeforeTalking;
    [ShowIf("hasDialogue")] public string dialogueKey;
    [ShowIf("hasDialogue")] public bool isUsingOverlay;
    [ShowIf("hasDialogue")][Dropdown("GetEyesStates")][OnValueChanged("OnDropdownChanged")] public int eyesState;
    [ShowIf("hasDialogue")][Dropdown("GetBodyStates")][OnValueChanged("OnDropdownChanged")] public int bodyStartState, bodyEndState;
    [ShowIf("hasDialogue")][Dropdown("GetMouthStates")][OnValueChanged("OnDropdownChanged")] public int mouthStartState, mouthEndState;
    [ShowIf("hasDialogue")] public float timeWaitAfterTalking;

    [Header("Phase 3: Interaction")]
    public bool hasInteraction;
    [ShowIf("hasInteraction")] public bool hasInfiniteTimeout;
    [ShowIf("ShowWaitTimeout")] public float waitTimeout;
    [ShowIf("hasInteraction")] public bool hasSubStep;
    [ShowIf("hasSubStep")] public float timeToWaitBeforeSubStep;
    [ShowIf("hasSubStep")] public SequenceStep subStep;

    bool ShowWaitTimeout() => hasInteraction && !hasInfiniteTimeout;

    DropdownList<int> GetBodyStates() => BuildDropdown(characterStates != null ? characterStates.bodyStates : null);
    DropdownList<int> GetEyesStates() => BuildDropdown(characterStates != null ? characterStates.eyesStates : null);
    DropdownList<int> GetMouthStates() => BuildDropdown(characterStates != null ? characterStates.mouthStates : null);

    DropdownList<int> BuildDropdown(System.Collections.Generic.List<CharacterStatesConfig.StateEntry> states)
    {
        var list = new DropdownList<int>();
        if (states == null || states.Count == 0)
        {
            list.Add("None", -1);
            return list;
        }
        foreach (var s in states)
            list.Add(s.name, s.id);
        return list;
    }

    void OnDropdownChanged()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
#endif
    }
}
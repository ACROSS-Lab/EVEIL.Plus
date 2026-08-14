using UnityEngine;
using System.Collections.Generic;
using NaughtyAttributes;

[CreateAssetMenu(fileName = "Character States", menuName = "Character States")]
public class CharacterStatesConfig : ScriptableObject
{
    [System.Serializable]
    public class StateEntry
    {
        public string name;
        public int id;
    }

    public List<StateEntry> eyesStates = new List<StateEntry>();
    public List<StateEntry> bodyStates = new List<StateEntry>();
    public List<StateEntry> mouthStates = new List<StateEntry>();

    [Button("Assign Next Free IDs")]
    void AssignNextFreeIds()
    {
        AssignMissingIds(eyesStates);
        AssignMissingIds(bodyStates);
        AssignMissingIds(mouthStates);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
#endif
    }

    void AssignMissingIds(List<StateEntry> states)
    {
        var usedIds = new HashSet<int>();
        foreach (var s in states)
            if (s.id >= 0) usedIds.Add(s.id);

        int nextId = 0;
        foreach (var s in states)
        {
            if (s.id >= 0) continue;
            while (usedIds.Contains(nextId)) nextId++;
            s.id = nextId;
            usedIds.Add(nextId);
        }
    }
}
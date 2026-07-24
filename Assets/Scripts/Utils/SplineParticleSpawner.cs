using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

[System.Serializable]
public class ParticleSpawnGroup
{
    [Tooltip("Just a label to keep the list readable in the inspector")]
    public string Name = "Group";

    public GameObject Prefab;

    [Min(0)]
    public int Count = 10;

    [Tooltip("Leave empty to let this group spawn on any spline in the container, picked randomly and weighted by length. " +
             "Fill in specific indices (0, 1, 2...) to restrict this group to certain splines only.")]
    public List<int> AllowedSplineIndices = new List<int>();
}

public class SplineParticleSpawner : MonoBehaviour
{
    [SerializeField] private SplineContainer splineContainer;

    [SerializeField] private List<ParticleSpawnGroup> spawnGroups = new List<ParticleSpawnGroup>();

    [Tooltip("Optional parent for spawned instances, keeps the hierarchy tidy. If left empty, instances are parented to this object.")]
    [SerializeField] private Transform spawnParent;

    private void Start()
    {
        if (splineContainer == null || splineContainer.Splines == null || splineContainer.Splines.Count == 0)
        {
            Debug.LogWarning($"[SplineParticleSpawner] No spline assigned on '{name}', nothing will be spawned.", this);
            return;
        }

        foreach (var group in spawnGroups)
        {
            SpawnGroup(group);
        }
    }

    private void SpawnGroup(ParticleSpawnGroup group)
    {
        if (group.Prefab == null)
        {
            Debug.LogWarning($"[SplineParticleSpawner] Group '{group.Name}' has no prefab assigned, skipping.", this);
            return;
        }

        if (group.Count <= 0) return;

        var followOnPrefab = group.Prefab.GetComponent<FollowSpline>();
        if (followOnPrefab == null)
        {
            Debug.LogWarning($"[SplineParticleSpawner] Prefab '{group.Prefab.name}' in group '{group.Name}' has no FollowSpline component, skipping.", this);
            return;
        }

        var parent = spawnParent != null ? spawnParent : transform;

        for (var i = 0; i < group.Count; i++)
        {
            var instance = Instantiate(group.Prefab, parent);
            var follow = instance.GetComponent<FollowSpline>();

            var splineIndex = FollowSpline.PickWeightedRandomSplineIndex(splineContainer, group.AllowedSplineIndices);

            // Calling Initialize here means the particle is configured before its own Start() runs,
            // so it will pick up this specific spline instead of rolling its own random choice.
            follow.Initialize(splineContainer, splineIndex);
        }
    }
}

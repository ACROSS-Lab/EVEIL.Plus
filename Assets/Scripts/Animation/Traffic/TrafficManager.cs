using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TrafficManager : MonoBehaviour
{
    [Header("Traffic Paths")]
    [SerializeField] private TrafficPath[] trafficPaths;

    [Header("Vehicle Prefabs")]
    [SerializeField] private TrafficVehicle[] vehiclePrefabs;
    [Tooltip("Global cap. Vehicles are spread across all splines in round robin, so every spline gets vehicles.")]
    [Min(0)]
    [SerializeField] private int maxTotalVehicles = 30;
    [SerializeField] private bool avoidConsecutiveDuplicates = true;

    [Header("Debug")]
    [SerializeField] private bool logSpawnSummary = true;

    private readonly List<TrafficVehicle> spawnedVehicles = new List<TrafficVehicle>();
    private readonly List<int> validIndices = new List<int>();
    private int previousPrefabIndex = -1;

    private void Start()
    {
        SpawnTraffic();
    }

    public void SpawnTraffic()
    {
        ClearTraffic();
        previousPrefabIndex = -1;

        if (trafficPaths == null || trafficPaths.Length == 0)
        {
            Debug.LogWarning("TrafficManager: No TrafficPath assigned.", this);
            return;
        }

        if (!HasValidPrefabs())
        {
            Debug.LogWarning("TrafficManager: Assign at least one valid vehicle prefab.", this);
            return;
        }

        int maxRounds = 0;

        foreach (TrafficPath path in trafficPaths)
        {
            if (path == null)
                continue;

            maxRounds = Mathf.Max(maxRounds, path.VehicleCount);

            if (logSpawnSummary)
            {
                Debug.Log(
                    $"TrafficManager: Path '{path.name}' has {path.SplineCount} spline(s), " +
                    $"{path.VehicleCount} vehicle(s) per spline.",
                    path
                );
            }
        }

        int spawnedCount = 0;

        // Round robin: each round spawns one vehicle on every spline,
        // so the global cap can never starve the last splines.
        for (int round = 0; round < maxRounds; round++)
        {
            foreach (TrafficPath path in trafficPaths)
            {
                if (path == null || round >= path.VehicleCount)
                    continue;

                int splineCount = path.SplineCount;

                for (int splineIndex = 0; splineIndex < splineCount; splineIndex++)
                {
                    if (spawnedCount >= maxTotalVehicles)
                    {
                        LogSummary(spawnedCount);
                        return;
                    }

                    float length = path.GetLength(splineIndex);

                    if (length <= 0.001f)
                    {
                        if (round == 0)
                        {
                            Debug.LogWarning(
                                $"TrafficManager: Spline {splineIndex} of path '{path.name}' has no valid length.",
                                path
                            );
                        }

                        continue;
                    }

                    TrafficVehicle prefab = GetRandomPrefab();

                    if (prefab == null)
                        continue;

                    float startDistance = length * (round / (float)path.VehicleCount);

                    // Alternate directions to populate both lanes as evenly as possible.
                    int direction = (round % 2 == 0) ? 1 : -1;

                    TrafficVehicle vehicle = Instantiate(prefab, transform);
                    vehicle.name = $"{prefab.name}_Traffic_{spawnedCount:00}";
                    vehicle.Initialize(path, splineIndex, startDistance, direction);

                    spawnedVehicles.Add(vehicle);
                    spawnedCount++;
                }
            }
        }

        LogSummary(spawnedCount);
    }

    private void LogSummary(int spawnedCount)
    {
        if (logSpawnSummary)
            Debug.Log($"TrafficManager: {spawnedCount} vehicle(s) spawned.", this);
    }

    private void ClearTraffic()
    {
        for (int i = 0; i < spawnedVehicles.Count; i++)
        {
            if (spawnedVehicles[i] != null)
                Destroy(spawnedVehicles[i].gameObject);
        }

        spawnedVehicles.Clear();
    }

    private bool HasValidPrefabs()
    {
        if (vehiclePrefabs == null)
            return false;

        foreach (TrafficVehicle prefab in vehiclePrefabs)
        {
            if (prefab != null)
                return true;
        }

        return false;
    }

    private TrafficVehicle GetRandomPrefab()
    {
        validIndices.Clear();

        for (int i = 0; i < vehiclePrefabs.Length; i++)
        {
            if (vehiclePrefabs[i] != null)
                validIndices.Add(i);
        }

        if (validIndices.Count == 0)
            return null;

        if (avoidConsecutiveDuplicates && validIndices.Count > 1)
            validIndices.Remove(previousPrefabIndex);

        int selectedIndex = validIndices[Random.Range(0, validIndices.Count)];
        previousPrefabIndex = selectedIndex;

        return vehiclePrefabs[selectedIndex];
    }
}

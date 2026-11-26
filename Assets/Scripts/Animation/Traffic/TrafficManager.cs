using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class TrafficManager : MonoBehaviour
{
    [System.Serializable]
    public class VehicleEntry
    {
        public TrafficVehicle prefab;

        [Tooltip("Relative weight. Weights 1, 1, 2 give 25%, 25% and 50%. A weight of 0 disables the vehicle.")]
        [Min(0f)]
        public float weight = 1f;

        public bool IsValid => prefab != null && weight > 0f;
    }

    [Header("Traffic Paths")]
    [SerializeField] private TrafficPath[] trafficPaths;

    [Header("Vehicle Prefabs")]
    [SerializeField] private VehicleEntry[] vehicles;
    [Min(0)]
    [SerializeField] private int maxTotalVehicles = 30;

    [Header("Proportions")]
    [Tooltip("ON: the exact proportions are respected (shuffled list, rounded with the largest remainder method). " +
             "OFF: each vehicle is drawn randomly according to its weight, so proportions are only approximate.")]
    [SerializeField] private bool exactProportions = true;
    [Tooltip("Avoids spawning the same prefab twice in a row when several prefabs are available.")]
    [SerializeField] private bool avoidConsecutiveDuplicates = true;

    [Header("Physics")]
    [Tooltip("Layer given to every traffic vehicle. Collisions between vehicles of this layer are disabled. " +
             "Create it in Project Settings > Tags and Layers. Leave empty to skip.")]
    [SerializeField] private string trafficLayerName = "Traffic";

    [Header("Debug")]
    [SerializeField] private bool logSpawnSummary = true;

    private readonly List<TrafficVehicle> spawnedVehicles = new List<TrafficVehicle>();
    private readonly List<int> deck = new List<int>();
    private int deckPosition;
    private int previousEntryIndex = -1;
    private int[] spawnedPerEntry;
    private int trafficLayer = -1;

    private void Start()
    {
        SpawnTraffic();
    }

    private void SetupTrafficLayer()
    {
        trafficLayer = -1;

        if (string.IsNullOrEmpty(trafficLayerName))
            return;

        trafficLayer = LayerMask.NameToLayer(trafficLayerName);

        if (trafficLayer < 0)
        {
            Debug.LogWarning(
                $"TrafficManager: Layer '{trafficLayerName}' does not exist. " +
                "Create it in Project Settings > Tags and Layers to disable collisions between vehicles.",
                this
            );
            return;
        }

        // One call, no per vehicle cost: vehicles of this layer never collide together.
        Physics.IgnoreLayerCollision(trafficLayer, trafficLayer, true);
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        Transform[] children = target.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
            children[i].gameObject.layer = layer;
    }

    public void SpawnTraffic()
    {
        ClearTraffic();
        previousEntryIndex = -1;
        SetupTrafficLayer();

        if (trafficPaths == null || trafficPaths.Length == 0)
        {
            Debug.LogWarning("TrafficManager: No TrafficPath assigned.", this);
            return;
        }

        if (!HasValidEntries())
        {
            Debug.LogWarning("TrafficManager: Assign at least one vehicle prefab with a weight above 0.", this);
            return;
        }

        spawnedPerEntry = new int[vehicles.Length];

        int maxRounds = 0;
        int expectedCount = 0;

        foreach (TrafficPath path in trafficPaths)
        {
            if (path == null)
                continue;

            maxRounds = Mathf.Max(maxRounds, path.VehicleCount);
            expectedCount += path.SplineCount * path.VehicleCount;

            if (logSpawnSummary)
            {
                Debug.Log(
                    $"TrafficManager: Path '{path.name}' has {path.SplineCount} spline(s), " +
                    $"{path.VehicleCount} vehicle(s) per spline.",
                    path
                );
            }
        }

        expectedCount = Mathf.Min(expectedCount, maxTotalVehicles);

        if (exactProportions)
            BuildDeck(expectedCount);

        int spawnedCount = 0;
        int skippedCount = 0;

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
                        LogSummary(spawnedCount, skippedCount);
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

                    int entryIndex = GetNextEntryIndex();

                    if (entryIndex < 0)
                        continue;

                    TrafficVehicle prefab = vehicles[entryIndex].prefab;

                    float startDistance = length * (round / (float)path.VehicleCount);

                    // Alternate directions to populate both lanes as evenly as possible.
                    int direction = (round % 2 == 0) ? 1 : -1;

                    TrafficVehicle vehicle = Instantiate(prefab, transform);
                    vehicle.name = $"{prefab.name}_Traffic_{spawnedCount:00}";

                    if (trafficLayer >= 0)
                        SetLayerRecursively(vehicle.gameObject, trafficLayer);

                    vehicle.Initialize(path, splineIndex, startDistance, direction);

                    // Never spawn a vehicle on top of another one.
                    if (!path.IsSpaceFree(vehicle))
                    {
                        // Deactivating unregisters it immediately, Destroy is only applied at the end of the frame.
                        vehicle.gameObject.SetActive(false);
                        Destroy(vehicle.gameObject);
                        skippedCount++;
                        continue;
                    }

                    spawnedVehicles.Add(vehicle);
                    spawnedPerEntry[entryIndex]++;
                    spawnedCount++;
                }
            }
        }

        LogSummary(spawnedCount, skippedCount);
    }

    // Builds a shuffled list of entry indices that respects the weights as exactly as possible.
    private void BuildDeck(int total)
    {
        deck.Clear();
        deckPosition = 0;

        if (total <= 0)
            return;

        float totalWeight = 0f;

        for (int i = 0; i < vehicles.Length; i++)
        {
            if (vehicles[i].IsValid)
                totalWeight += vehicles[i].weight;
        }

        if (totalWeight <= 0f)
            return;

        int[] quotas = new int[vehicles.Length];
        float[] remainders = new float[vehicles.Length];
        int assigned = 0;

        for (int i = 0; i < vehicles.Length; i++)
        {
            if (!vehicles[i].IsValid)
                continue;

            float exact = total * vehicles[i].weight / totalWeight;
            quotas[i] = Mathf.FloorToInt(exact);
            remainders[i] = exact - quotas[i];
            assigned += quotas[i];
        }

        // Largest remainder method: distribute the vehicles left by the rounding.
        int remaining = total - assigned;

        while (remaining > 0)
        {
            int best = -1;

            for (int i = 0; i < vehicles.Length; i++)
            {
                if (!vehicles[i].IsValid)
                    continue;

                if (best < 0 || remainders[i] > remainders[best])
                    best = i;
            }

            if (best < 0)
                break;

            quotas[best]++;
            remainders[best] = -1f;
            remaining--;
        }

        for (int i = 0; i < quotas.Length; i++)
        {
            for (int n = 0; n < quotas[i]; n++)
                deck.Add(i);
        }

        // Fisher Yates shuffle.
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }
    }

    private int GetNextEntryIndex()
    {
        if (exactProportions && deckPosition < deck.Count)
        {
            // Swap with a later different entry to avoid two identical vehicles in a row.
            if (avoidConsecutiveDuplicates && deck[deckPosition] == previousEntryIndex)
            {
                for (int j = deckPosition + 1; j < deck.Count; j++)
                {
                    if (deck[j] != previousEntryIndex)
                    {
                        int temp = deck[deckPosition];
                        deck[deckPosition] = deck[j];
                        deck[j] = temp;
                        break;
                    }
                }
            }

            previousEntryIndex = deck[deckPosition];
            deckPosition++;
            return previousEntryIndex;
        }

        // Random draw according to the weights (also used if the deck is exhausted).
        int validCount = 0;

        for (int i = 0; i < vehicles.Length; i++)
        {
            if (vehicles[i].IsValid)
                validCount++;
        }

        if (validCount == 0)
            return -1;

        bool excludePrevious = avoidConsecutiveDuplicates && validCount > 1;
        float totalWeight = 0f;

        for (int i = 0; i < vehicles.Length; i++)
        {
            if (!vehicles[i].IsValid || (excludePrevious && i == previousEntryIndex))
                continue;

            totalWeight += vehicles[i].weight;
        }

        float roll = Random.value * totalWeight;
        int lastValid = -1;

        for (int i = 0; i < vehicles.Length; i++)
        {
            if (!vehicles[i].IsValid || (excludePrevious && i == previousEntryIndex))
                continue;

            lastValid = i;
            roll -= vehicles[i].weight;

            if (roll <= 0f)
            {
                previousEntryIndex = i;
                return i;
            }
        }

        previousEntryIndex = lastValid;
        return lastValid;
    }

    private void LogSummary(int spawnedCount, int skippedCount)
    {
        if (!logSpawnSummary)
            return;

        StringBuilder builder = new StringBuilder();
        builder.Append($"TrafficManager: {spawnedCount} vehicle(s) spawned, ");
        builder.Append($"{skippedCount} skipped because there was no free space.");

        if (spawnedPerEntry != null && spawnedCount > 0)
        {
            builder.Append(" Distribution: ");

            bool first = true;

            for (int i = 0; i < vehicles.Length; i++)
            {
                if (!vehicles[i].IsValid)
                    continue;

                if (!first)
                    builder.Append(", ");

                float percent = 100f * spawnedPerEntry[i] / spawnedCount;
                builder.Append($"{vehicles[i].prefab.name} {spawnedPerEntry[i]} ({percent:0}%)");
                first = false;
            }
        }

        Debug.Log(builder.ToString(), this);
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

    private bool HasValidEntries()
    {
        if (vehicles == null)
            return false;

        for (int i = 0; i < vehicles.Length; i++)
        {
            if (vehicles[i] != null && vehicles[i].IsValid)
                return true;
        }

        return false;
    }
}

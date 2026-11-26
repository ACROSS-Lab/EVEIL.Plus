using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

[DisallowMultipleComponent]
public class TrafficPath : MonoBehaviour
{
    // Runtime state of one virtual traffic light placed on a spline.
    private class RedLight
    {
        public float distance;
        public bool isRed;
        public float timer;
    }

    // Pre-sampled data of one spline of the container.
    private class SplineLane
    {
        public Vector3[] positions;
        public Vector3[] tangents;
        public Vector3[] ups;
        public float[] cumulativeDistances;
        public float length;

        // Index 0 = vehicles going backward (-1), index 1 = vehicles going forward (+1).
        public readonly List<TrafficVehicle>[] vehicles =
        {
            new List<TrafficVehicle>(),
            new List<TrafficVehicle>()
        };

        public RedLight[] lights;
    }

    [Header("Spline")]
    [Tooltip("All splines of this container are used as roads.")]
    [SerializeField] private SplineContainer splineContainer;
    [Min(8)]
    [SerializeField] private int sampleCount = 100;

    [Header("Traffic")]
    [Tooltip("Number of vehicles spawned on EACH spline of the container.")]
    [Min(0)]
    [SerializeField] private int vehicleCount = 6;
    [Min(0.01f)]
    [SerializeField] private float vehicleSpeed = 5f;

    [Header("Two-Way Lanes")]
    [Tooltip("Lateral distance from the spline centre to each lane.")]
    [Min(0f)]
    [SerializeField] private float laneOffset = 1.5f;

    [Header("Vehicle Following")]
    [Tooltip("Minimum bumper to bumper distance maintained from the vehicle ahead.")]
    [Min(0f)]
    [SerializeField] private float minimumFollowingDistance = 2f;
    [Tooltip("Maximum deceleration used to calculate a safe following speed.")]
    [Min(0.1f)]
    [SerializeField] private float braking = 6f;

    [Header("Periodic Slowdowns")]
    [Range(0f, 1f)]
    [SerializeField] private float slowdownProbability = 0.35f;
    [Min(0f)]
    [SerializeField] private float slowdownSpeed = 1.5f;
    [Min(0.1f)]
    [SerializeField] private float slowdownDuration = 2f;
    [Min(0.1f)]
    [SerializeField] private float slowdownInterval = 8f;

    [Header("Red Lights")]
    [SerializeField] private bool enableRedLights = true;
    [Tooltip("Number of virtual traffic lights placed randomly on each spline. Each light affects both directions.")]
    [Min(0)]
    [SerializeField] private int redLightsPerSpline = 2;
    [Tooltip("Chance that a light turns red when its green phase ends.")]
    [Range(0f, 1f)]
    [SerializeField] private float redLightProbability = 0.7f;
    [Tooltip("Random red phase duration (min, max) in seconds.")]
    [SerializeField] private Vector2 redDurationRange = new Vector2(4f, 10f);
    [Tooltip("Random green phase duration (min, max) in seconds.")]
    [SerializeField] private Vector2 greenDurationRange = new Vector2(8f, 20f);
    [Tooltip("Distance kept between the front bumper of a stopped vehicle and the stop line.")]
    [Min(0f)]
    [SerializeField] private float stopLineMargin = 1f;

    [Header("Crossing Avoidance (vehicles of different splines)")]
    [SerializeField] private bool enableCrossingAvoidance = true;
    [Tooltip("Distance ahead (bumper to bumper) at which a vehicle of another spline is taken into account.")]
    [Min(0f)]
    [SerializeField] private float crossingLookAhead = 10f;
    [Tooltip("Extra lateral clearance added to the vehicle widths.")]
    [Min(0f)]
    [SerializeField] private float crossingMargin = 0.5f;
    [Tooltip("Vehicles with a height difference above this value are ignored (bridges).")]
    [Min(0f)]
    [SerializeField] private float crossingHeightTolerance = 2f;
    [Tooltip("Seconds a vehicle may stay blocked by crossing traffic before it ignores it (deadlock breaker).")]
    [Min(0.5f)]
    [SerializeField] private float crossingDeadlockTimeout = 5f;
    [Tooltip("Seconds during which crossing checks are skipped after a deadlock timeout.")]
    [Min(0.5f)]
    [SerializeField] private float crossingDeadlockIgnoreDuration = 3f;

    private SplineLane[] lanes;
    private readonly List<TrafficVehicle> allVehicles = new List<TrafficVehicle>();

    public int VehicleCount => vehicleCount;
    public float VehicleSpeed => vehicleSpeed;
    public float LaneOffset => laneOffset;
    public float MinimumFollowingDistance => minimumFollowingDistance;
    public float Braking => braking;
    public float SlowdownProbability => slowdownProbability;
    public float SlowdownSpeed => slowdownSpeed;
    public float SlowdownDuration => slowdownDuration;
    public float SlowdownInterval => slowdownInterval;
    public float StopLineMargin => stopLineMargin;
    public float CrossingDeadlockTimeout => crossingDeadlockTimeout;
    public float CrossingDeadlockIgnoreDuration => crossingDeadlockIgnoreDuration;

    // Builds the path on demand if Awake has not run yet (for example on an inactive object).
    public int SplineCount
    {
        get
        {
            EnsureBuilt();
            return lanes != null ? lanes.Length : 0;
        }
    }

    private void Awake()
    {
        EnsureBuilt();
    }

    private void EnsureBuilt()
    {
        if (lanes == null)
            BuildPath();
    }

    private void Update()
    {
        if (!enableRedLights || lanes == null)
            return;

        float deltaTime = Time.deltaTime;

        for (int l = 0; l < lanes.Length; l++)
        {
            RedLight[] lights = lanes[l].lights;

            if (lights == null)
                continue;

            for (int i = 0; i < lights.Length; i++)
                UpdateLight(lights[i], deltaTime);
        }
    }

    private void UpdateLight(RedLight light, float deltaTime)
    {
        light.timer -= deltaTime;

        if (light.timer > 0f)
            return;

        if (light.isRed)
        {
            // Back to green, vehicles restart.
            light.isRed = false;
            light.timer = RandomDuration(greenDurationRange);
        }
        else if (UnityEngine.Random.value <= redLightProbability)
        {
            light.isRed = true;
            light.timer = RandomDuration(redDurationRange);
        }
        else
        {
            // Stay green for another random phase.
            light.timer = RandomDuration(greenDurationRange);
        }
    }

    private static float RandomDuration(Vector2 range)
    {
        return UnityEngine.Random.Range(range.x, Mathf.Max(range.x, range.y));
    }

    public float GetLength(int splineIndex)
    {
        EnsureBuilt();

        if (lanes == null || splineIndex < 0 || splineIndex >= lanes.Length)
            return 0f;

        return lanes[splineIndex].length;
    }

    public void BuildPath()
    {
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();

        if (splineContainer == null || splineContainer.Splines == null || splineContainer.Splines.Count == 0)
        {
            Debug.LogError($"TrafficPath '{name}' has no SplineContainer or no spline.", this);
            lanes = new SplineLane[0];
            return;
        }

        int splineTotal = splineContainer.Splines.Count;
        lanes = new SplineLane[splineTotal];

        for (int s = 0; s < splineTotal; s++)
            lanes[s] = BuildLane(splineContainer.Splines[s], s);
    }

    private SplineLane BuildLane(Spline spline, int splineIndex)
    {
        SplineLane lane = new SplineLane();

        int count = Mathf.Max(8, sampleCount);
        lane.positions = new Vector3[count + 1];
        lane.tangents = new Vector3[count + 1];
        lane.ups = new Vector3[count + 1];
        lane.cumulativeDistances = new float[count + 1];

        Transform splineTransform = splineContainer.transform;

        for (int i = 0; i <= count; i++)
        {
            float t = i / (float)count;

            SplineUtility.Evaluate(
                spline,
                t,
                out Unity.Mathematics.float3 localPosition,
                out Unity.Mathematics.float3 localTangent,
                out Unity.Mathematics.float3 localUp
            );

            lane.positions[i] = splineTransform.TransformPoint((Vector3)localPosition);
            lane.tangents[i] = splineTransform.TransformDirection((Vector3)localTangent).normalized;
            lane.ups[i] = splineTransform.TransformDirection((Vector3)localUp).normalized;

            if (i > 0)
            {
                lane.cumulativeDistances[i] =
                    lane.cumulativeDistances[i - 1] +
                    Vector3.Distance(lane.positions[i - 1], lane.positions[i]);
            }
        }

        lane.length = lane.cumulativeDistances[count];

        if (lane.length <= 0.001f)
        {
            Debug.LogWarning($"TrafficPath '{name}': spline {splineIndex} is too short or has invalid data.", this);
            return lane;
        }

        BuildLights(lane);
        return lane;
    }

    private void BuildLights(SplineLane lane)
    {
        if (!enableRedLights || redLightsPerSpline <= 0)
        {
            lane.lights = null;
            return;
        }

        lane.lights = new RedLight[redLightsPerSpline];

        // One light per section of the spline, at a random position inside it.
        for (int i = 0; i < redLightsPerSpline; i++)
        {
            float section = lane.length / redLightsPerSpline;

            lane.lights[i] = new RedLight
            {
                distance = section * (i + UnityEngine.Random.Range(0.2f, 0.8f)),
                isRed = false,
                // Desynchronise lights with a random first green phase.
                timer = RandomDuration(greenDurationRange) * UnityEngine.Random.value
            };
        }
    }

    public bool EvaluateAtDistance(
        int splineIndex,
        float distance,
        float lateralOffset,
        out Vector3 position,
        out Vector3 forward,
        out Vector3 up)
    {
        position = transform.position;
        forward = transform.forward;
        up = transform.up;

        if (lanes == null || splineIndex < 0 || splineIndex >= lanes.Length)
            return false;

        SplineLane lane = lanes[splineIndex];

        if (lane.positions == null || lane.positions.Length < 2 || lane.length <= 0.001f)
            return false;

        // Wrap the distance so vehicles loop on open or closed splines.
        distance = Mathf.Repeat(distance, lane.length);

        float[] cumulative = lane.cumulativeDistances;
        int low = 0;
        int high = cumulative.Length - 1;

        // Find the sample segment containing the requested distance.
        while (low < high - 1)
        {
            int middle = (low + high) >> 1;

            if (cumulative[middle] < distance)
                low = middle;
            else
                high = middle;
        }

        float segmentLength = cumulative[high] - cumulative[low];
        float segmentT = segmentLength > 0.0001f
            ? (distance - cumulative[low]) / segmentLength
            : 0f;

        Vector3 centrePosition = Vector3.Lerp(lane.positions[low], lane.positions[high], segmentT);

        // Lerp is cheaper than Slerp and accurate enough with dense samples.
        Vector3 interpolatedForward = Vector3.Lerp(lane.tangents[low], lane.tangents[high], segmentT);
        forward = interpolatedForward.sqrMagnitude > 0.0001f
            ? interpolatedForward.normalized
            : transform.forward;

        Vector3 interpolatedUp = Vector3.Lerp(lane.ups[low], lane.ups[high], segmentT);
        up = interpolatedUp.sqrMagnitude > 0.0001f
            ? interpolatedUp.normalized
            : Vector3.up;

        // Offset each direction to its own side of the spline.
        Vector3 right = Vector3.Cross(up, forward).normalized;
        position = centrePosition + right * lateralOffset;

        return true;
    }

    public void RegisterVehicle(TrafficVehicle vehicle)
    {
        if (vehicle == null)
            return;

        if (!allVehicles.Contains(vehicle))
            allVehicles.Add(vehicle);

        List<TrafficVehicle> list = GetVehicleList(vehicle);

        if (list != null && !list.Contains(vehicle))
            list.Add(vehicle);
    }

    public void UnregisterVehicle(TrafficVehicle vehicle)
    {
        allVehicles.Remove(vehicle);

        List<TrafficVehicle> list = GetVehicleList(vehicle);

        if (list != null)
            list.Remove(vehicle);
    }

    private List<TrafficVehicle> GetVehicleList(TrafficVehicle vehicle)
    {
        if (vehicle == null || lanes == null)
            return null;

        int index = vehicle.SplineIndex;

        if (index < 0 || index >= lanes.Length)
            return null;

        return lanes[index].vehicles[vehicle.Direction > 0 ? 1 : 0];
    }

    // Bumper to bumper distance to the vehicle ahead, or float.MaxValue if none.
    public float GetDistanceToVehicleAhead(TrafficVehicle vehicle)
    {
        float nearestCentreGap = float.MaxValue;
        float nearestHalfLength = 0f;

        List<TrafficVehicle> list = GetVehicleList(vehicle);

        if (list == null)
            return float.MaxValue;

        float length = lanes[vehicle.SplineIndex].length;

        if (length <= 0.001f)
            return float.MaxValue;

        // Only vehicles of the same spline and same direction are in this list.
        for (int i = list.Count - 1; i >= 0; i--)
        {
            TrafficVehicle other = list[i];

            if (other == null)
            {
                list.RemoveAt(i);
                continue;
            }

            if (other == vehicle)
                continue;

            float gap = Mathf.Repeat(
                (other.DistanceTravelled - vehicle.DistanceTravelled) * vehicle.Direction,
                length
            );

            // Ignore vehicles behind us.
            if (gap > 0.01f && gap < nearestCentreGap)
            {
                nearestCentreGap = gap;
                nearestHalfLength = other.HalfLength;
            }
        }

        if (nearestCentreGap >= float.MaxValue)
            return float.MaxValue;

        return Mathf.Max(0f, nearestCentreGap - vehicle.HalfLength - nearestHalfLength);
    }

    // Distance to the nearest red light ahead (from the vehicle centre), or float.MaxValue if none.
    public float GetDistanceToRedLight(int splineIndex, float distance, int direction)
    {
        float nearestDistance = float.MaxValue;

        if (!enableRedLights || lanes == null || splineIndex < 0 || splineIndex >= lanes.Length)
            return nearestDistance;

        SplineLane lane = lanes[splineIndex];
        RedLight[] lights = lane.lights;

        if (lights == null || lane.length <= 0.001f)
            return nearestDistance;

        for (int i = 0; i < lights.Length; i++)
        {
            RedLight light = lights[i];

            if (!light.isRed)
                continue;

            float gap = Mathf.Repeat((light.distance - distance) * direction, lane.length);

            if (gap > 0.01f && gap < nearestDistance)
                nearestDistance = gap;
        }

        return nearestDistance;
    }

    // Bumper to bumper distance to a vehicle of ANOTHER spline that is in front of this one
    // and that this vehicle must yield to, or float.MaxValue if none.
    public float GetDistanceToCrossingVehicle(TrafficVehicle vehicle)
    {
        float nearest = float.MaxValue;

        if (!enableCrossingAvoidance || vehicle == null)
            return nearest;

        Vector3 myPosition = vehicle.Position;
        Vector3 myForward = vehicle.Forward;

        // Work on the horizontal plane.
        float fx = myForward.x;
        float fz = myForward.z;
        float forwardMagnitude = Mathf.Sqrt(fx * fx + fz * fz);

        if (forwardMagnitude < 0.0001f)
            return nearest;

        fx /= forwardMagnitude;
        fz /= forwardMagnitude;

        // Right vector on the horizontal plane.
        float rx = fz;
        float rz = -fx;

        int myId = vehicle.GetInstanceID();

        for (int i = allVehicles.Count - 1; i >= 0; i--)
        {
            TrafficVehicle other = allVehicles[i];

            if (other == null)
            {
                allVehicles.RemoveAt(i);
                continue;
            }

            // Same spline vehicles are handled by GetDistanceToVehicleAhead.
            if (other == vehicle || other.SplineIndex == vehicle.SplineIndex)
                continue;

            Vector3 delta = other.Position - myPosition;

            if (Mathf.Abs(delta.y) > crossingHeightTolerance)
                continue;

            float maxRange = crossingLookAhead + vehicle.Length + other.Length;

            if (delta.x * delta.x + delta.z * delta.z > maxRange * maxRange)
                continue;

            float forwardDistance = delta.x * fx + delta.z * fz;

            // Only vehicles in front matter.
            if (forwardDistance <= 0f)
                continue;

            Vector3 otherForward = other.Forward;
            float otherMagnitude = Mathf.Sqrt(otherForward.x * otherForward.x + otherForward.z * otherForward.z);

            if (otherMagnitude < 0.0001f)
                continue;

            float ofx = otherForward.x / otherMagnitude;
            float ofz = otherForward.z / otherMagnitude;

            float dotForward = ofx * fx + ofz * fz;
            float dotRight = ofx * rx + ofz * rz;

            // Extent of the other vehicle projected on my axes.
            float otherExtentLateral = Mathf.Abs(dotRight) * other.HalfLength + Mathf.Abs(dotForward) * other.HalfWidth;
            float otherExtentForward = Mathf.Abs(dotForward) * other.HalfLength + Mathf.Abs(dotRight) * other.HalfWidth;

            float lateralDistance = delta.x * rx + delta.z * rz;

            if (Mathf.Abs(lateralDistance) > vehicle.HalfWidth + otherExtentLateral + crossingMargin)
                continue;

            // Same heading (merge): always yield to the one in front.
            // Otherwise (crossing or head on): the vehicle with the lowest id yields, so only one waits.
            bool sameHeading = dotForward > 0.7f;

            if (!sameHeading && other.GetInstanceID() < myId)
                continue;

            float bumperGap = Mathf.Max(0f, forwardDistance - vehicle.HalfLength - otherExtentForward);

            if (bumperGap > crossingLookAhead)
                continue;

            if (bumperGap < nearest)
                nearest = bumperGap;
        }

        return nearest;
    }

    // Used at spawn time: false if the vehicle overlaps another one or is too close to it.
    public bool IsSpaceFree(TrafficVehicle vehicle)
    {
        if (vehicle == null || lanes == null)
            return true;

        for (int i = allVehicles.Count - 1; i >= 0; i--)
        {
            TrafficVehicle other = allVehicles[i];

            if (other == null)
            {
                allVehicles.RemoveAt(i);
                continue;
            }

            if (other == vehicle)
                continue;

            if (other.SplineIndex == vehicle.SplineIndex)
            {
                // Opposite directions are on separate lanes.
                if (other.Direction != vehicle.Direction)
                    continue;

                float splineLength = lanes[vehicle.SplineIndex].length;
                float distance = Mathf.Abs(other.DistanceTravelled - vehicle.DistanceTravelled);
                distance = Mathf.Min(distance, splineLength - distance);

                float required = vehicle.HalfLength + other.HalfLength + minimumFollowingDistance;

                if (distance < required)
                    return false;
            }
            else
            {
                Vector3 delta = other.Position - vehicle.Position;

                if (Mathf.Abs(delta.y) > crossingHeightTolerance)
                    continue;

                float radius = (Mathf.Max(vehicle.Length, vehicle.Width) + Mathf.Max(other.Length, other.Width)) * 0.5f;

                if (delta.x * delta.x + delta.z * delta.z < radius * radius)
                    return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || lanes == null || !enableRedLights)
            return;

        float halfWidth = laneOffset + 1f;

        for (int l = 0; l < lanes.Length; l++)
        {
            RedLight[] lights = lanes[l].lights;

            if (lights == null)
                continue;

            for (int i = 0; i < lights.Length; i++)
            {
                if (!EvaluateAtDistance(l, lights[i].distance, 0f,
                        out Vector3 position, out Vector3 forward, out Vector3 up))
                    continue;

                Vector3 right = Vector3.Cross(up, forward).normalized;
                Gizmos.color = lights[i].isRed ? Color.red : Color.green;
                Gizmos.DrawLine(position - right * halfWidth, position + right * halfWidth);
            }
        }
    }

    private void OnValidate()
    {
        sampleCount = Mathf.Max(8, sampleCount);
        vehicleCount = Mathf.Max(0, vehicleCount);
        vehicleSpeed = Mathf.Max(0.01f, vehicleSpeed);
        laneOffset = Mathf.Max(0f, laneOffset);
        minimumFollowingDistance = Mathf.Max(0f, minimumFollowingDistance);
        braking = Mathf.Max(0.1f, braking);
        slowdownSpeed = Mathf.Max(0f, slowdownSpeed);
        slowdownDuration = Mathf.Max(0.1f, slowdownDuration);
        slowdownInterval = Mathf.Max(0.1f, slowdownInterval);
        redLightsPerSpline = Mathf.Max(0, redLightsPerSpline);
        stopLineMargin = Mathf.Max(0f, stopLineMargin);
        crossingLookAhead = Mathf.Max(0f, crossingLookAhead);
        crossingMargin = Mathf.Max(0f, crossingMargin);
        crossingHeightTolerance = Mathf.Max(0f, crossingHeightTolerance);
        crossingDeadlockTimeout = Mathf.Max(0.5f, crossingDeadlockTimeout);
        crossingDeadlockIgnoreDuration = Mathf.Max(0.5f, crossingDeadlockIgnoreDuration);

        redDurationRange.x = Mathf.Max(0.1f, redDurationRange.x);
        redDurationRange.y = Mathf.Max(redDurationRange.x, redDurationRange.y);
        greenDurationRange.x = Mathf.Max(0.1f, greenDurationRange.x);
        greenDurationRange.y = Mathf.Max(greenDurationRange.x, greenDurationRange.y);
    }
#endif
}

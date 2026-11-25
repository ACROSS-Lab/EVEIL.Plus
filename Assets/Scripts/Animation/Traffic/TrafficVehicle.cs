using UnityEngine;

[DisallowMultipleComponent]
public class TrafficVehicle : MonoBehaviour
{
    [Header("Movement")]
    [Min(0.01f)]
    [SerializeField] private float acceleration = 3f;
    [Min(0.1f)]
    [SerializeField] private float brakingMultiplier = 2f;
    [SerializeField] private bool orientAlongPath = true;

    private Transform cachedTransform;
    private TrafficPath currentPath;
    private int splineIndex;
    private float pathLength;
    private float distanceTravelled;
    private float currentSpeed;
    private float slowdownTimer;
    private float nextSlowdownTime;
    private float lateralOffset;

    public int Direction { get; private set; } = 1;
    public int SplineIndex => splineIndex;
    public float DistanceTravelled => distanceTravelled;

    private void Awake()
    {
        cachedTransform = transform;
    }

    public void Initialize(TrafficPath path, int splineIdx, float startDistance, int direction)
    {
        if (currentPath != null)
            currentPath.UnregisterVehicle(this);

        currentPath = path;
        splineIndex = splineIdx;
        Direction = direction >= 0 ? 1 : -1;
        distanceTravelled = Mathf.Max(0f, startDistance);
        pathLength = currentPath != null ? currentPath.GetLength(splineIndex) : 0f;

        if (currentPath == null || pathLength <= 0.001f)
        {
            enabled = false;
            return;
        }

        distanceTravelled = Mathf.Repeat(distanceTravelled, pathLength);

        // Opposing directions use opposite sides of the spline.
        lateralOffset = Direction > 0
            ? -currentPath.LaneOffset
            : currentPath.LaneOffset;

        currentSpeed = currentPath.VehicleSpeed;
        nextSlowdownTime = Random.Range(
            currentPath.SlowdownInterval * 0.5f,
            currentPath.SlowdownInterval * 1.5f
        );

        currentPath.RegisterVehicle(this);
        UpdatePose();
    }

    private void OnEnable()
    {
        if (currentPath != null)
            currentPath.RegisterVehicle(this);
    }

    private void OnDisable()
    {
        if (currentPath != null)
            currentPath.UnregisterVehicle(this);
    }

    private void Update()
    {
        if (currentPath == null)
            return;

        float deltaTime = Time.deltaTime;

        UpdateSlowdownCycle(deltaTime);

        float targetSpeed = currentPath.VehicleSpeed;

        // Limit speed when approaching another vehicle travelling in the same direction.
        float vehicleGap = currentPath.GetDistanceToVehicleAhead(this);

        if (vehicleGap < float.MaxValue)
        {
            targetSpeed = LimitSpeedForGap(
                targetSpeed,
                vehicleGap - currentPath.MinimumFollowingDistance
            );
        }

        // Limit speed when approaching a red light. Followers then stop behind the first vehicle.
        float lightGap = currentPath.GetDistanceToRedLight(splineIndex, distanceTravelled, Direction);

        if (lightGap < float.MaxValue)
        {
            targetSpeed = LimitSpeedForGap(
                targetSpeed,
                lightGap - currentPath.StopLineMargin
            );
        }

        if (slowdownTimer > 0f)
            targetSpeed = Mathf.Min(targetSpeed, currentPath.SlowdownSpeed);

        float rate = targetSpeed < currentSpeed
            ? acceleration * brakingMultiplier
            : acceleration;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * deltaTime);

        // A stopped vehicle does not need any pose update.
        if (currentSpeed <= 0f)
            return;

        distanceTravelled += Direction * currentSpeed * deltaTime;
        distanceTravelled = Mathf.Repeat(distanceTravelled, pathLength);

        UpdatePose();
    }

    private float LimitSpeedForGap(float targetSpeed, float availableGap)
    {
        availableGap = Mathf.Max(0f, availableGap);
        float safeSpeed = Mathf.Sqrt(2f * currentPath.Braking * availableGap);
        return Mathf.Min(targetSpeed, safeSpeed);
    }

    private void UpdateSlowdownCycle(float deltaTime)
    {
        if (slowdownTimer > 0f)
        {
            slowdownTimer -= deltaTime;
            return;
        }

        nextSlowdownTime -= deltaTime;

        if (nextSlowdownTime <= 0f)
        {
            if (Random.value <= currentPath.SlowdownProbability)
                slowdownTimer = currentPath.SlowdownDuration;

            nextSlowdownTime = Random.Range(
                currentPath.SlowdownInterval * 0.5f,
                currentPath.SlowdownInterval * 1.5f
            );
        }
    }

    private void UpdatePose()
    {
        if (!currentPath.EvaluateAtDistance(
                splineIndex,
                distanceTravelled,
                lateralOffset,
                out Vector3 position,
                out Vector3 forward,
                out Vector3 up))
            return;

        cachedTransform.position = position;

        if (orientAlongPath)
        {
            Vector3 travelDirection = forward * Direction;

            if (travelDirection.sqrMagnitude > 0.0001f)
                cachedTransform.rotation = Quaternion.LookRotation(travelDirection, up);
        }
    }
}

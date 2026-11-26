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

    [Header("Size (used to avoid collisions)")]
    [Tooltip("Vehicle length in metres along its forward axis. 0 = measured automatically from the renderers.")]
    [Min(0f)]
    [SerializeField] private float vehicleLength = 0f;
    [Tooltip("Vehicle width in metres. 0 = measured automatically from the renderers.")]
    [Min(0f)]
    [SerializeField] private float vehicleWidth = 0f;

    [Header("Physics")]
    [Tooltip("Vehicles are moved by script, so their Rigidbodies are made kinematic to avoid physical pushes.")]
    [SerializeField] private bool forceKinematicRigidbodies = true;

    private const float DefaultLength = 4.5f;
    private const float DefaultWidth = 1.8f;

    private Transform cachedTransform;
    private TrafficPath currentPath;
    private int splineIndex;
    private float pathLength;
    private float distanceTravelled;
    private float currentSpeed;
    private float slowdownTimer;
    private float nextSlowdownTime;
    private float lateralOffset;
    private float crossingBlockedTimer;
    private float crossingIgnoreTimer;

    public int Direction { get; private set; } = 1;
    public int SplineIndex => splineIndex;
    public float DistanceTravelled => distanceTravelled;
    public float Length { get; private set; } = DefaultLength;
    public float Width { get; private set; } = DefaultWidth;
    public float HalfLength => Length * 0.5f;
    public float HalfWidth => Width * 0.5f;
    public Vector3 Position { get; private set; }
    public Vector3 Forward { get; private set; } = Vector3.forward;

    private void Awake()
    {
        cachedTransform = transform;
        Position = cachedTransform.position;
        Forward = cachedTransform.forward;

        MeasureSize();

        if (forceKinematicRigidbodies)
        {
            Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>();

            for (int i = 0; i < bodies.Length; i++)
                bodies[i].isKinematic = true;
        }
    }

    private void MeasureSize()
    {
        float length = vehicleLength;
        float width = vehicleWidth;

        if (length <= 0f || width <= 0f)
        {
            bool hasBounds = TryMeasureLocalBounds(out Bounds localBounds);

            Vector3 scale = cachedTransform.lossyScale;
            float measuredLength = hasBounds ? localBounds.size.z * Mathf.Abs(scale.z) : DefaultLength;
            float measuredWidth = hasBounds ? localBounds.size.x * Mathf.Abs(scale.x) : DefaultWidth;

            if (length <= 0f)
                length = measuredLength > 0.1f ? measuredLength : DefaultLength;

            if (width <= 0f)
                width = measuredWidth > 0.1f ? measuredWidth : DefaultWidth;
        }

        Length = length;
        Width = width;
    }

    // Encapsulates every renderer in the local space of the vehicle (forward = local Z).
    private bool TryMeasureLocalBounds(out Bounds result)
    {
        result = new Bounds();
        bool initialised = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer renderer = renderers[r];

            // Particles and trails would give a wrong size.
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                continue;

            Bounds local = renderer.localBounds;
            Vector3 centre = local.center;
            Vector3 extents = local.extents;

            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = centre + new Vector3(
                    (c & 1) == 0 ? -extents.x : extents.x,
                    (c & 2) == 0 ? -extents.y : extents.y,
                    (c & 4) == 0 ? -extents.z : extents.z
                );

                Vector3 point = cachedTransform.InverseTransformPoint(
                    renderer.transform.TransformPoint(corner)
                );

                if (!initialised)
                {
                    result = new Bounds(point, Vector3.zero);
                    initialised = true;
                }
                else
                {
                    result.Encapsulate(point);
                }
            }
        }

        return initialised;
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
        crossingBlockedTimer = 0f;
        crossingIgnoreTimer = 0f;
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

        // Vehicle ahead on the same spline and direction (bumper to bumper gap).
        float vehicleGap = currentPath.GetDistanceToVehicleAhead(this);

        if (vehicleGap < float.MaxValue)
        {
            targetSpeed = LimitSpeedForGap(
                targetSpeed,
                vehicleGap - currentPath.MinimumFollowingDistance
            );
        }

        // Red light: the front bumper must stop before the line.
        float lightGap = currentPath.GetDistanceToRedLight(splineIndex, distanceTravelled, Direction);

        if (lightGap < float.MaxValue)
        {
            targetSpeed = LimitSpeedForGap(
                targetSpeed,
                lightGap - HalfLength - currentPath.StopLineMargin
            );
        }

        // Vehicles of other splines (crossings, merges).
        targetSpeed = ApplyCrossingAvoidance(targetSpeed, deltaTime);

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

    private float ApplyCrossingAvoidance(float targetSpeed, float deltaTime)
    {
        // After a deadlock timeout, crossing checks are skipped for a short time.
        if (crossingIgnoreTimer > 0f)
        {
            crossingIgnoreTimer -= deltaTime;
            return targetSpeed;
        }

        float crossingGap = currentPath.GetDistanceToCrossingVehicle(this);

        if (crossingGap >= float.MaxValue)
        {
            crossingBlockedTimer = 0f;
            return targetSpeed;
        }

        float crossingSpeed = LimitSpeedForGap(
            currentPath.VehicleSpeed,
            crossingGap - currentPath.MinimumFollowingDistance
        );

        // Safety valve: vehicles waiting for each other must not block the road forever.
        if (crossingSpeed < 0.05f && currentSpeed < 0.05f)
        {
            crossingBlockedTimer += deltaTime;

            if (crossingBlockedTimer >= currentPath.CrossingDeadlockTimeout)
            {
                crossingBlockedTimer = 0f;
                crossingIgnoreTimer = currentPath.CrossingDeadlockIgnoreDuration;
            }
        }
        else
        {
            crossingBlockedTimer = 0f;
        }

        return Mathf.Min(targetSpeed, crossingSpeed);
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
        Position = position;

        Vector3 travelDirection = forward * Direction;

        if (travelDirection.sqrMagnitude > 0.0001f)
        {
            Forward = travelDirection;

            if (orientAlongPath)
                cachedTransform.rotation = Quaternion.LookRotation(travelDirection, up);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class FollowSpline : MonoBehaviour
{
    [SerializeField] private SplineContainer splineContainer;

    [Header("Movement")]
    public float Speed = 1f;
    [Tooltip("Starting position along the spline, 0-1")]
    [Range(0f, 1f)] public float StartT = 0f;
    public bool Loop = true;

    [Header("Offsets")]
    public float HorizontalOffset = 0f;
    public float VerticalOffset = 0f;
    [Tooltip("Adds a random value on top of the base offsets, useful when spawning many particles")]
    public bool RandomizeOffsets = false;
    public float RandomHorizontalRange = 0.5f;
    public float RandomVerticalRange = 0.5f;

    [Header("Along Spline Offset")]
    [Tooltip("Shifts the starting point forward or backward along the spline, as a fraction of its total length (0-1)")]
    public bool RandomizeAlongSpline = false;
    [Range(0f, 1f)] public float RandomAlongSplineRange = 0.1f;

    [Header("Multiple Splines")]
    [Tooltip("If the container holds several splines, pick one at random per instance, weighted by its length so short parts are not over represented")]
    public bool RandomizeSplineIndex = true;
    [Tooltip("Used only when RandomizeSplineIndex is false")]
    public int SplineIndex = 0;

    private Spline _spline;
    private float _t;
    private float _splineLength;
    private bool _isValid;
    private bool _initialized;

    private void Start()
    {
        // If nothing external called Initialize yet (e.g. particle placed manually in the scene),
        // fall back to self-initializing with the inspector settings.
        if (!_initialized) Initialize();
    }

    /// <summary>
    /// Sets up this particle. Can be called manually right after Instantiate by a spawner,
    /// in which case it takes priority over the automatic Start() initialization below.
    /// </summary>
    /// <param name="container">Optional override for the spline container, useful when the prefab does not have one pre-assigned.</param>
    /// <param name="forcedSplineIndex">If 0 or greater, forces this particle onto that specific spline index, bypassing RandomizeSplineIndex/SplineIndex.</param>
    public void Initialize(SplineContainer container = null, int forcedSplineIndex = -1)
    {
        if (container != null) splineContainer = container;

        // Guard against a missing spline reference so the component fails safely instead of throwing every frame
        if (splineContainer == null || splineContainer.Splines == null || splineContainer.Splines.Count == 0)
        {
            Debug.LogWarning($"[FollowSpline] No spline assigned on '{name}', disabling component.", this);
            _isValid = false;
            enabled = false;
            return;
        }

        var splineIndex = forcedSplineIndex >= 0
            ? Mathf.Clamp(forcedSplineIndex, 0, splineContainer.Splines.Count - 1)
            : RandomizeSplineIndex
                ? PickWeightedRandomSplineIndex(splineContainer)
                : Mathf.Clamp(SplineIndex, 0, splineContainer.Splines.Count - 1);

        _spline = splineContainer.Splines[splineIndex];
        _splineLength = splineContainer.CalculateLength(splineIndex);

        if (_spline == null || _splineLength <= 0f)
        {
            Debug.LogWarning($"[FollowSpline] Spline at index {splineIndex} is invalid or has zero length on '{name}', disabling component.", this);
            _isValid = false;
            enabled = false;
            return;
        }

        _isValid = true;
        _initialized = true;
        _t = StartT;

        if (RandomizeOffsets)
        {
            HorizontalOffset += Random.Range(-RandomHorizontalRange, RandomHorizontalRange);
            VerticalOffset += Random.Range(-RandomVerticalRange, RandomVerticalRange);
        }

        if (RandomizeAlongSpline)
        {
            // Already a fraction of the spline length, no conversion needed
            _t += Random.Range(-RandomAlongSplineRange, RandomAlongSplineRange);
            _t = ((_t % 1f) + 1f) % 1f; // wrap into 0-1 regardless of sign
        }
    }

    /// <summary>
    /// Picks a spline index at random, weighted by each spline's length so longer parts get proportionally more picks.
    /// Exposed as public static so a spawner script can reuse the exact same logic.
    /// </summary>
    public static int PickWeightedRandomSplineIndex(SplineContainer container, IReadOnlyList<int> restrictToIndices = null)
    {
        var candidateIndices = restrictToIndices != null && restrictToIndices.Count > 0
            ? restrictToIndices
            : null;

        var count = candidateIndices?.Count ?? container.Splines.Count;
        var lengths = new float[count];
        var totalLength = 0f;

        for (var i = 0; i < count; i++)
        {
            var splineIndex = candidateIndices != null ? candidateIndices[i] : i;
            lengths[i] = container.CalculateLength(splineIndex);
            totalLength += lengths[i];
        }

        if (totalLength <= 0f) return candidateIndices != null ? candidateIndices[0] : 0;

        var randomValue = Random.Range(0f, totalLength);
        var cumulative = 0f;

        for (var i = 0; i < count; i++)
        {
            cumulative += lengths[i];
            if (randomValue <= cumulative) return candidateIndices != null ? candidateIndices[i] : i;
        }

        var lastIndex = count - 1;
        return candidateIndices != null ? candidateIndices[lastIndex] : lastIndex;
    }

    private void Update()
    {
        // Extra safety in case the reference is cleared or swapped at runtime
        if (!_isValid || splineContainer == null || _spline == null || _splineLength <= 0f) return;

        // Advance normalized position based on a real world speed (units per second)
        _t += (Speed / _splineLength) * Time.deltaTime;

        if (Loop)
        {
            _t %= 1f;
            if (_t < 0f) _t += 1f;
        }
        else
        {
            _t = Mathf.Clamp01(_t);
        }

        // Direct evaluation at t, no nearest point search needed
        var localPosition = SplineUtility.EvaluatePosition(_spline, _t);
        var localTangent = SplineUtility.EvaluateTangent(_spline, _t);
        var localUpVector = SplineUtility.EvaluateUpVector(_spline, _t);

        var worldPosition = splineContainer.transform.TransformPoint(localPosition);
        var worldTangent = splineContainer.transform.TransformDirection(localTangent).normalized;
        var worldUp = splineContainer.transform.TransformDirection(localUpVector).normalized;

        // Right vector derived from the spline's own up vector, stays stable even on vertical sections
        var worldRight = Vector3.Cross(worldUp, worldTangent).normalized;

        var offsetPosition = worldPosition
            + worldRight * HorizontalOffset
            + worldUp * VerticalOffset;

        transform.SetPositionAndRotation(offsetPosition, Quaternion.LookRotation(worldTangent, worldUp));
    }
}

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

    private float _t;
    private float _splineLength;
    private bool _isValid;

    private void Start()
    {
        // Guard against a missing spline reference so the component fails safely instead of throwing every frame
        if (splineContainer == null || splineContainer.Spline == null)
        {
            Debug.LogWarning($"[FollowSpline] No spline assigned on '{name}', disabling component.", this);
            _isValid = false;
            enabled = false;
            return;
        }

        _splineLength = splineContainer.CalculateLength();

        if (_splineLength <= 0f)
        {
            Debug.LogWarning($"[FollowSpline] Spline length is zero on '{name}', disabling component.", this);
            _isValid = false;
            enabled = false;
            return;
        }

        _isValid = true;
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

    private void Update()
    {
        // Extra safety in case the reference is cleared or swapped at runtime
        if (!_isValid || splineContainer == null || splineContainer.Spline == null || _splineLength <= 0f) return;

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
        var localPosition = SplineUtility.EvaluatePosition(splineContainer.Spline, _t);
        var localTangent = SplineUtility.EvaluateTangent(splineContainer.Spline, _t);
        var localUpVector = SplineUtility.EvaluateUpVector(splineContainer.Spline, _t);

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

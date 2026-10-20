using UnityEngine;
using UnityEngine.Events;

public class RainCloudAnimator : MonoBehaviour
{
    [Header("Drops")]
    public Transform[] drops = new Transform[3];

    [Tooltip("Fall direction in the drop's local space. (0, -1, 0) means along the local Y axis, downward.")]
    public Vector3 fallDirection = Vector3.down;

    [Tooltip("If true, the direction follows the drop's own local rotation (its local Y axis). If false, it is expressed in the parent's space.")]
    public bool useDropLocalAxis = true;

    [Tooltip("Travel distance from point A to point B")]
    public float fallDistance = 2.0f;

    [Tooltip("Duration of one full fall, in seconds")]
    public float fallDuration = 1.2f;

    [Tooltip("Start delay between each drop, in seconds")]
    public float delayBetweenDrops = 0.4f;

    [Tooltip("Speed curve. 0 = point A (top), 1 = point B (bottom). Use an ease in curve for a gravity feel.")]
    public AnimationCurve fallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Drop spawn and despawn (optional)")]
    public bool scaleFade = true;

    [Range(0.01f, 0.5f)]
    public float fadeZone = 0.1f;

    [Header("Drop rotation (optional)")]
    [Tooltip("Continuous spin around an axis in the drop's local space")]
    public bool enableSpin = true;

    public Vector3 spinAxis = Vector3.up;

    [Tooltip("Base spin speed in degrees per second")]
    public float spinSpeed = 90f;

    [Tooltip("Tilt oscillation around an axis in the drop's local space")]
    public bool enableSway = false;

    public Vector3 swayAxis = Vector3.forward;

    [Tooltip("Max tilt angle in degrees")]
    public float swayAngle = 12f;

    [Tooltip("Sway oscillations per second")]
    public float swaySpeed = 1.5f;

    [Header("Rotation desync")]
    [Tooltip("Gives each drop a random start phase for spin and sway")]
    public bool randomizePhase = true;

    [Tooltip("Each drop's speed is multiplied by a random value in this range (x = min, y = max). Use (1, 1) for identical speeds.")]
    public Vector2 speedMultiplierRange = new Vector2(0.7f, 1.3f);

    [Tooltip("Randomly flips the spin direction of some drops")]
    public bool randomizeSpinDirection = true;

    [Tooltip("Seed for repeatable results. Change it to get a different pattern.")]
    public int randomSeed = 12345;

    [Header("Cloud (optional)")]
    public Transform cloud;
    public float floatAmplitude = 0.1f;
    public float floatSpeed = 1.2f;

    [Header("Show / Hide")]
    [Tooltip("Object scaled by the show/hide tween. Leave empty to use this object.")]
    public Transform visualRoot;

    public bool startHidden = false;

    [Tooltip("Duration of the show/hide tween, in seconds")]
    public float tweenDuration = 0.35f;

    [Tooltip("0 = hidden, 1 = fully visible. Output is used as a scale multiplier.")]
    public AnimationCurve tweenCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public UnityEvent onShown;
    public UnityEvent onHidden;

    private Vector3[] dropStartPos;
    private Vector3[] dropStartScale;
    private Quaternion[] dropStartRot;
    private float[] spinPhase;       // degrees
    private float[] swayPhase;       // radians
    private float[] speedMultiplier;
    private float[] spinSign;
    private Vector3 cloudStartPos;
    private Vector3 rootStartScale;
    private Renderer[] renderers;

    private float visibility;       // current tween value, 0 to 1
    private float targetVisibility; // 0 or 1
    private bool rendererState = true;

    void Awake()
    {
        if (visualRoot == null) visualRoot = transform;

        int n = drops.Length;
        dropStartPos = new Vector3[n];
        dropStartScale = new Vector3[n];
        dropStartRot = new Quaternion[n];
        spinPhase = new float[n];
        swayPhase = new float[n];
        speedMultiplier = new float[n];
        spinSign = new float[n];

        // Local random generator so the global Unity random state is untouched
        System.Random rng = new System.Random(randomSeed);

        for (int i = 0; i < n; i++)
        {
            dropStartPos[i] = drops[i].localPosition;
            dropStartScale[i] = drops[i].localScale;
            dropStartRot[i] = drops[i].localRotation;

            spinPhase[i] = randomizePhase ? (float)rng.NextDouble() * 360f : 0f;
            swayPhase[i] = randomizePhase ? (float)rng.NextDouble() * Mathf.PI * 2f : 0f;

            float min = Mathf.Min(speedMultiplierRange.x, speedMultiplierRange.y);
            float max = Mathf.Max(speedMultiplierRange.x, speedMultiplierRange.y);
            speedMultiplier[i] = Mathf.Lerp(min, max, (float)rng.NextDouble());

            bool flip = randomizeSpinDirection && rng.NextDouble() > 0.5;
            spinSign[i] = flip ? -1f : 1f;
        }

        if (cloud != null)
            cloudStartPos = cloud.localPosition;

        rootStartScale = visualRoot.localScale;
        renderers = visualRoot.GetComponentsInChildren<Renderer>(true);

        visibility = startHidden ? 0f : 1f;
        targetVisibility = visibility;
        ApplyVisibility();
    }

    void Update()
    {
        UpdateTween();

        // Skip the work entirely while fully hidden
        if (visibility <= 0f && targetVisibility <= 0f) return;

        AnimateDrops();
        AnimateCloud();
    }

    // ---------------------------------------------------------------
    // Public API, callable from UnityEvents
    // ---------------------------------------------------------------

    public void Show()
    {
        targetVisibility = 1f;
    }

    public void Hide()
    {
        targetVisibility = 0f;
    }

    public void Toggle()
    {
        targetVisibility = targetVisibility > 0.5f ? 0f : 1f;
    }

    public void SetVisible(bool visible)
    {
        targetVisibility = visible ? 1f : 0f;
    }

    // Instant versions, no tween
    public void ShowInstant()
    {
        targetVisibility = 1f;
        visibility = 1f;
        ApplyVisibility();
        onShown?.Invoke();
    }

    public void HideInstant()
    {
        targetVisibility = 0f;
        visibility = 0f;
        ApplyVisibility();
        onHidden?.Invoke();
    }

    // ---------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------

    void UpdateTween()
    {
        if (Mathf.Approximately(visibility, targetVisibility)) return;

        float step = tweenDuration > 0f ? Time.deltaTime / tweenDuration : 1f;
        visibility = Mathf.MoveTowards(visibility, targetVisibility, step);
        ApplyVisibility();

        if (Mathf.Approximately(visibility, targetVisibility))
        {
            if (targetVisibility >= 1f) onShown?.Invoke();
            else onHidden?.Invoke();
        }
    }

    void ApplyVisibility()
    {
        float s = Mathf.Max(0f, tweenCurve.Evaluate(visibility));
        visualRoot.localScale = rootStartScale * s;

        // Disable renderers when fully hidden to save GPU work
        bool shouldRender = visibility > 0f;
        if (shouldRender != rendererState)
        {
            rendererState = shouldRender;
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = shouldRender;
        }
    }

    void AnimateDrops()
    {
        for (int i = 0; i < drops.Length; i++)
        {
            // Progress 0 to 1, looping, with a per-drop offset
            float t = (Time.time - i * delayBetweenDrops) / fallDuration;
            float progress = Mathf.Repeat(t, 1f);
            float eased = fallCurve.Evaluate(progress);

            // Direction along the drop's own local axis (or the parent's space)
            Vector3 dir = useDropLocalAxis
                ? dropStartRot[i] * fallDirection.normalized
                : fallDirection.normalized;

            // Point A is the start position, point B is A + dir * distance
            drops[i].localPosition = dropStartPos[i] + dir * (eased * fallDistance);

            // Rotation, desynchronized per drop
            Quaternion rot = dropStartRot[i];

            if (enableSpin)
            {
                float angle = spinPhase[i] + Time.time * spinSpeed * speedMultiplier[i] * spinSign[i];
                rot = rot * Quaternion.AngleAxis(angle, spinAxis.normalized);
            }

            if (enableSway)
            {
                float sway = Mathf.Sin(Time.time * swaySpeed * speedMultiplier[i] * Mathf.PI * 2f + swayPhase[i]) * swayAngle;
                rot = rot * Quaternion.AngleAxis(sway, swayAxis.normalized);
            }

            drops[i].localRotation = rot;

            // Small scale down at the top and bottom to avoid popping
            if (scaleFade)
            {
                float fadeIn = Mathf.Clamp01(progress / fadeZone);
                float fadeOut = Mathf.Clamp01((1f - progress) / fadeZone);
                drops[i].localScale = dropStartScale[i] * Mathf.Min(fadeIn, fadeOut);
            }
        }
    }

    void AnimateCloud()
    {
        if (cloud == null) return;

        float offsetY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        cloud.localPosition = cloudStartPos + new Vector3(0f, offsetY, 0f);
    }
}

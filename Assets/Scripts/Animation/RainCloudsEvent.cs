using DG.Tweening;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Rain clouds event.
/// Show(): clouds arrive from a random far position with a fade in,
///         then the rain FX and the rain sound start.
/// Hide(): the opposite (rain FX and sound fade out, clouds leave with a fade out).
///
/// A single progress float (0 to 1) drives everything: position, alpha, rain FX and rain sound.
/// If Hide() is called during a Show() (or the other way around), the motion
/// simply restarts from the current value, with no pop.
///
/// Hook it to your UnityEvents: RainCloudsEvent.Show and RainCloudsEvent.Hide.
/// </summary>
public class RainCloudsEvent : MonoBehaviour
{
    public enum FadePropertyMode
    {
        /// <summary>Float property (e.g. _Fade or _Alpha from a Shader Graph).</summary>
        Float,
        /// <summary>Alpha channel of a color property (e.g. _BaseColor of a standard URP shader).</summary>
        ColorAlpha
    }

    private class CloudData
    {
        public Renderer renderer;
        public Transform tr;
        public Material mat;
        public Vector3 baseLocalPos;
        public Vector3 farOffset;
        public float delay;
        public Color baseColor;
    }

    [Header("Clouds")]
    [Tooltip("Cloud renderers. If empty, all child Renderers are used (Particle Systems are ignored).")]
    [SerializeField] private Renderer[] clouds;

    [Header("Timing")]
    [Tooltip("Duration of the full appearance, in seconds.")]
    [SerializeField] private float showDuration = 4f;
    [Tooltip("Duration of the full disappearance, in seconds.")]
    [SerializeField] private float hideDuration = 3f;
    [Tooltip("Random start delay per cloud, as a fraction of the duration (0 = all clouds in sync).")]
    [Range(0f, 1f)]
    [SerializeField] private float maxRandomDelay = 0.4f;
    [Tooltip("If enabled, the tween ignores Time.timeScale.")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Movement")]
    [Tooltip("Minimum distance of the far position, relative to the cloud base position.")]
    [SerializeField] private float minFarDistance = 30f;
    [Tooltip("Maximum distance of the far position, relative to the cloud base position.")]
    [SerializeField] private float maxFarDistance = 60f;
    [Tooltip("Vertical amplitude of the far offset (0 = purely horizontal).")]
    [Range(0f, 1f)]
    [SerializeField] private float verticalSpread = 0.2f;
    [Tooltip("Pick new random far positions on each Show (only when clouds are fully hidden).")]
    [SerializeField] private bool rerollFarPositionOnShow = true;
    [Tooltip("Easing of the cloud movement.")]
    [SerializeField] private Ease positionEase = Ease.OutSine;
    [Tooltip("Easing of the fade.")]
    [SerializeField] private Ease alphaEase = Ease.InOutSine;

    [Header("Material")]
    [Tooltip("Property reference name (for Shader Graph: the Reference field, e.g. _Fade).")]
    [SerializeField] private string fadePropertyName = "_Fade";
    [Tooltip("Float: tweens a float property. ColorAlpha: tweens the alpha of a color property (e.g. _BaseColor).")]
    [SerializeField] private FadePropertyMode fadeMode = FadePropertyMode.Float;
    [Tooltip("Property value when the cloud is invisible (Float mode only).")]
    [SerializeField] private float hiddenValue = 0f;
    [Tooltip("Property value when the cloud is fully visible (Float mode only).")]
    [SerializeField] private float visibleValue = 1f;

    [Header("Rain FX")]
    [Tooltip("Existing rain Particle System. Disable its Play On Awake.")]
    [SerializeField] private ParticleSystem rain;
    [Tooltip("Progress (0 to 1) at which the rain starts. Its intensity then ramps up to full at progress 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float rainStartProgress = 0.5f;

    [Header("Rain Sound")]
    [Tooltip("Rain sound source (looping clip). Disable its Play On Awake. Its volume at startup is used as the maximum volume.")]
    [SerializeField] private AudioSource rainSound;
    [Tooltip("Easing applied to the sound volume, based on the same rain factor as the FX (Linear = same ramp as the FX).")]
    [SerializeField] private Ease rainSoundEase = Ease.Linear;

    [Header("Initial State")]
    [Tooltip("If enabled, clouds, rain and sound are active at startup.")]
    [SerializeField] private bool startVisible = false;

    private CloudData[] _data;
    private int _fadeId;
    private float _progress;
    private float _rainBaseRate;
    private float _rainSoundMaxVolume;
    private Tween _tween;

    public bool IsVisible => _progress > 0f;
    public float Progress => _progress;

    private void Awake()
    {
        _fadeId = Shader.PropertyToID(fadePropertyName);
        BuildClouds();

        if (rain != null)
        {
            _rainBaseRate = rain.emission.rateOverTimeMultiplier;
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (rainSound != null)
        {
            _rainSoundMaxVolume = rainSound.volume;
            rainSound.loop = true;
            rainSound.Stop();
        }

        Apply(startVisible ? 1f : 0f);
    }

    private void Start()
    {
        // Safety net in case the Particle System or the AudioSource has Play On Awake
        // enabled and started after our Awake.
        if (_progress > 0f) return;

        if (rain != null)
        {
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (rainSound != null)
        {
            rainSound.Stop();
        }
    }

    private void OnDestroy()
    {
        _tween?.Kill();

        if (_data == null) return;
        foreach (CloudData c in _data)
        {
            if (c.mat != null) Destroy(c.mat);
        }
    }

    private void OnValidate()
    {
        showDuration = Mathf.Max(0.01f, showDuration);
        hideDuration = Mathf.Max(0.01f, hideDuration);
        minFarDistance = Mathf.Max(0f, minFarDistance);
        maxFarDistance = Mathf.Max(minFarDistance, maxFarDistance);
    }

    // ------------------------------------------------------------------
    // Public API (hook these to your UnityEvents)
    // ------------------------------------------------------------------

    public void Show()
    {
        if (Mathf.Approximately(_progress, 1f) && _tween == null) return;

        if (rerollFarPositionOnShow && _progress <= 0.0001f)
        {
            RerollFarPositions();
        }

        StartTween(1f, showDuration);
    }

    public void Hide()
    {
        if (Mathf.Approximately(_progress, 0f) && _tween == null) return;

        StartTween(0f, hideDuration);
    }

    public void Toggle()
    {
        bool goingToShow = _tween != null ? _progress < 0.5f : _progress <= 0f;
        if (goingToShow) Show();
        else Hide();
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    private void BuildClouds()
    {
        Renderer[] source = clouds;

        if (source == null || source.Length == 0)
        {
            Renderer[] all = GetComponentsInChildren<Renderer>(true);
            var filtered = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in all)
            {
                if (r is ParticleSystemRenderer) continue;
                filtered.Add(r);
            }
            source = filtered.ToArray();
        }

        _data = new CloudData[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            Renderer r = source[i];

            var c = new CloudData
            {
                renderer = r,
                tr = r.transform,
                mat = r.material, // creates a material instance owned by this cloud
                baseLocalPos = r.transform.localPosition,
                delay = Random.Range(0f, maxRandomDelay)
            };

            if (fadeMode == FadePropertyMode.ColorAlpha && c.mat.HasProperty(_fadeId))
            {
                c.baseColor = c.mat.GetColor(_fadeId);
            }
            else
            {
                c.baseColor = Color.white;
            }

            c.farOffset = RandomFarOffset();
            _data[i] = c;
        }
    }

    private Vector3 RandomFarOffset()
    {
        Vector2 circle = Random.insideUnitCircle.normalized;
        if (circle == Vector2.zero) circle = Vector2.right;

        Vector3 dir = new Vector3(circle.x, Random.Range(-verticalSpread, verticalSpread), circle.y).normalized;
        return dir * Random.Range(minFarDistance, maxFarDistance);
    }

    private void RerollFarPositions()
    {
        foreach (CloudData c in _data)
        {
            c.farOffset = RandomFarOffset();
            c.delay = Random.Range(0f, maxRandomDelay);
        }
    }

    private void StartTween(float target, float fullDuration)
    {
        _tween?.Kill();

        // Duration is proportional to the remaining distance, so interruptions stay smooth.
        float duration = fullDuration * Mathf.Abs(target - _progress);

        if (duration <= 0f)
        {
            Apply(target);
            _tween = null;
            return;
        }

        _tween = DOVirtual.Float(_progress, target, duration, Apply)
            .SetEase(Ease.Linear)
            .SetUpdate(useUnscaledTime)
            .SetLink(gameObject)
            .OnComplete(() => _tween = null);
    }

    /// <summary>Applies the full state for a global progress t (0 to 1).</summary>
    private void Apply(float t)
    {
        _progress = t;

        float spread = 1f + maxRandomDelay;

        foreach (CloudData c in _data)
        {
            float local = Mathf.Clamp01(t * spread - c.delay);

            float posT = DOVirtual.EasedValue(0f, 1f, local, positionEase);
            float alphaT = Mathf.Clamp01(DOVirtual.EasedValue(0f, 1f, local, alphaEase));

            c.tr.localPosition = Vector3.LerpUnclamped(c.baseLocalPos + c.farOffset, c.baseLocalPos, posT);

            if (fadeMode == FadePropertyMode.Float)
            {
                c.mat.SetFloat(_fadeId, Mathf.Lerp(hiddenValue, visibleValue, alphaT));
            }
            else
            {
                Color col = c.baseColor;
                col.a = c.baseColor.a * alphaT;
                c.mat.SetColor(_fadeId, col);
            }

            // Disable rendering when the cloud is fully hidden.
            c.renderer.enabled = local > 0f;
        }

        ApplyRain(t);
    }

    private void ApplyRain(float t)
    {
        // Shared factor (0 to 1): drives both the rain FX and the rain sound.
        float factor = Mathf.Clamp01(Mathf.InverseLerp(rainStartProgress, 1f, t));

        if (rain != null)
        {
            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTimeMultiplier = _rainBaseRate * factor;

            if (factor > 0f && !rain.isEmitting)
            {
                rain.Play();
            }
            else if (factor <= 0f && rain.isEmitting)
            {
                // Stops emission without removing drops already in the air.
                rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        if (rainSound != null)
        {
            float easedFactor = DOVirtual.EasedValue(0f, 1f, factor, rainSoundEase);
            rainSound.volume = _rainSoundMaxVolume * easedFactor;

            if (factor > 0f && !rainSound.isPlaying)
            {
                rainSound.Play();
            }
            else if (factor <= 0f && rainSound.isPlaying)
            {
                rainSound.Stop();
            }
        }
    }
}

#if UNITY_EDITOR
/// <summary>Custom inspector with debug buttons (Play Mode only).</summary>
[CustomEditor(typeof(RainCloudsEvent))]
public class RainCloudsEventEditor : Editor
{
    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var script = (RainCloudsEvent)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Debug buttons are available in Play Mode only.", MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            Rect barRect = EditorGUILayout.GetControlRect();
            EditorGUI.ProgressBar(barRect, script.Progress, "Progress " + script.Progress.ToString("0.00"));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Show")) script.Show();
                if (GUILayout.Button("Hide")) script.Hide();
                if (GUILayout.Button("Toggle")) script.Toggle();
            }
        }
    }
}
#endif

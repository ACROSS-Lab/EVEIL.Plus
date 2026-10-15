using System.Collections;
using UnityEngine;

/// <summary>
/// Fades static meshes using a dissolve shader where:
///   0 = fully visible
///   1 = fully transparent/dissolved
///
/// Uses MaterialPropertyBlock — does NOT break Static Batching.
/// All public methods are callable via Unity Events.
/// </summary>
public class StaticMeshFader : MonoBehaviour
{
    [Header("Dissolve Settings")]
    [Tooltip("Duration of the fade in seconds")]
    [SerializeField] private float fadeDuration = 1f;

    [Tooltip("Shader property name for dissolve (must match your shader)")]
    [SerializeField] private string dissolvePropertyName = "_Dissolve";

    [Header("Curve")]
    [Tooltip("Easing curve for the fade (X = normalized time 0-1, Y = dissolve value 0-1)")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Options")]
    [SerializeField] private bool includeSelf = false;

    [Tooltip("Disable Renderers when fully dissolved (saves GPU fillrate)")]
    [SerializeField] private bool disableWhenHidden = true;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private Renderer[] _renderers;
    private MaterialPropertyBlock _propertyBlock;
    private Coroutine _fadeCoroutine;
    private int _dissolvePropertyID;

    // Track actual current value so interrupted fades resume from correct position
    private float _currentDissolve = 1f; // Start hidden by default

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _dissolvePropertyID = Shader.PropertyToID(dissolvePropertyName);
        _propertyBlock = new MaterialPropertyBlock();
        _renderers = GetTargetRenderers();

        // Apply initial state so shader matches _currentDissolve
        SetDissolveOnAll(_currentDissolve);
    }

    // -------------------------------------------------------------------------
    // PUBLIC METHODS — usable via Unity Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fade in: dissolve goes from current value → 0 (appears progressively).
    /// Renderers are re-enabled automatically before the fade starts.
    /// </summary>
    public void FadeIn()
    {
        // FIX: always re-enable renderers BEFORE starting the coroutine
        // so the mesh is visible as soon as dissolve starts decreasing
        SetRenderersEnabled(true);
        StartFade(_currentDissolve, 0f);
    }

    /// <summary>
    /// Fade out: dissolve goes from current value → 1 (disappears progressively).
    /// </summary>
    public void FadeOut()
    {
        StartFade(_currentDissolve, 1f);
    }

    /// <summary>Instantly visible. No fade.</summary>
    public void ShowInstant()
    {
        StopCurrentFade();
        SetRenderersEnabled(true);
        SetDissolveOnAll(0f);
    }

    /// <summary>Instantly hidden. No fade.</summary>
    public void HideInstant()
    {
        StopCurrentFade();
        SetDissolveOnAll(1f);
        if (disableWhenHidden) SetRenderersEnabled(false);
    }

    /// <summary>
    /// Set a custom duration (in seconds), then fade in.
    /// Tip: use large values (5, 10, 30) for very slow atmospheric transitions.
    /// </summary>
    public void FadeInWithDuration(float duration)
    {
        fadeDuration = Mathf.Max(0.01f, duration);
        FadeIn();
    }

    /// <summary>
    /// Set a custom duration (in seconds), then fade out.
    /// </summary>
    public void FadeOutWithDuration(float duration)
    {
        fadeDuration = Mathf.Max(0.01f, duration);
        FadeOut();
    }

    // -------------------------------------------------------------------------
    // PRIVATE — Fade logic
    // -------------------------------------------------------------------------

    private void StartFade(float from, float to)
    {
        // If already at target, nothing to do
        if (Mathf.Approximately(from, to)) return;

        StopCurrentFade();
        _fadeCoroutine = StartCoroutine(FadeCoroutine(from, to));
    }

    private void StopCurrentFade()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
    }

    private IEnumerator FadeCoroutine(float from, float to)
    {
        float elapsed = 0f;

        // Scale duration by remaining distance so interrupted fades
        // don't take the full duration to travel a small range
        float range = Mathf.Abs(to - from);
        float scaledDuration = fadeDuration * range;

        while (elapsed < scaledDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scaledDuration);
            float dissolve = Mathf.Lerp(from, to, fadeCurve.Evaluate(t));
            SetDissolveOnAll(dissolve);
            yield return null;
        }

        // Ensure we land exactly on the target value
        SetDissolveOnAll(to);

        if (disableWhenHidden && to >= 1f)
            SetRenderersEnabled(false);

        _fadeCoroutine = null;
    }

    /// <summary>
    /// Core method — MaterialPropertyBlock does NOT break Static Batching.
    /// </summary>
    private void SetDissolveOnAll(float dissolve)
    {
        _currentDissolve = dissolve;

        foreach (Renderer rend in _renderers)
        {
            if (rend == null) continue;
            rend.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(_dissolvePropertyID, dissolve);
            rend.SetPropertyBlock(_propertyBlock);
        }
    }

    private void SetRenderersEnabled(bool enabled)
    {
        foreach (Renderer rend in _renderers)
            if (rend != null) rend.enabled = enabled;
    }

    private Renderer[] GetTargetRenderers()
    {
        if (includeSelf)
            return GetComponentsInChildren<Renderer>(includeInactive: true);

        Renderer[] all = GetComponentsInChildren<Renderer>(includeInactive: true);
        Renderer self = GetComponent<Renderer>();
        if (self == null) return all;

        var children = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in all)
            if (r != self) children.Add(r);

        return children.ToArray();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
    }
#endif
}

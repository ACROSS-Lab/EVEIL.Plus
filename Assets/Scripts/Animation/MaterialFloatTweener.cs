using System.Collections;
using UnityEngine;

/// <summary>
/// Fades a float property of a material in and out.
/// Call FadeIn() and FadeOut() from external UnityEvents.
/// </summary>
public class MaterialFloatTweener : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private string propertyName = "_Cutoff";
    [SerializeField] private float outValue = 0f;
    [SerializeField] private float inValue = 1f;
    [SerializeField] private float duration = 0.5f;
    [Tooltip("Delay in seconds before FadeIn starts.")]
    [SerializeField] private float delayIn = 0f;
    [Tooltip("Delay in seconds before FadeOut starts.")]
    [SerializeField] private float delayOut = 0f;

    private MaterialPropertyBlock block;
    private int propertyId;
    private Coroutine routine;
    private float currentValue;

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        propertyId = Shader.PropertyToID(propertyName);
        Apply(outValue);
    }

    public void FadeIn() => StartTween(inValue, delayIn);

    public void FadeOut() => StartTween(outValue, delayOut);

    private void StartTween(float target, float delay)
    {
        // Cancels any running fade (including one still waiting on its delay)
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Tween(target, delay));
    }

    private IEnumerator Tween(float target, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // Starts from the current value, so In/Out can be reversed mid-fade
        float start = currentValue;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
            Apply(Mathf.Lerp(start, target, t));
            yield return null;
        }

        Apply(target);
        routine = null;
    }

    private void Apply(float value)
    {
        currentValue = value;
        targetRenderer.GetPropertyBlock(block);
        block.SetFloat(propertyId, value);
        targetRenderer.SetPropertyBlock(block);
    }
}

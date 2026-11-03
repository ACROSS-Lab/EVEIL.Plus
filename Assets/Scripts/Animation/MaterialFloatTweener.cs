using DG.Tweening;
using UnityEngine;

/// <summary>
/// Fades a material float property and/or a CanvasGroup alpha using DOTween.
/// Call FadeIn() and FadeOut() from external UnityEvents.
/// </summary>
public class MaterialFloatTweener : MonoBehaviour
{
    [Header("Material Settings")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private string propertyName = "_Cutoff";

    [Header("UI CanvasGroup (Optional)")]
    [SerializeField] private CanvasGroup targetCanvasGroup;

    [Header("Fade Values")]
    [SerializeField] private float outValue = 0f;
    [SerializeField] private float inValue = 1f;
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private Ease easeType = Ease.Linear;

    [Header("Delays")]
    [Tooltip("Delay in seconds before FadeIn starts.")]
    [SerializeField] private float delayIn = 0f;
    [Tooltip("Delay in seconds before FadeOut starts.")]
    [SerializeField] private float delayOut = 0f;

    private MaterialPropertyBlock block;
    private int propertyId;
    private Sequence currentSequence;
    private float currentMaterialValue;

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            block = new MaterialPropertyBlock();
            propertyId = Shader.PropertyToID(propertyName);
        }

        if (targetCanvasGroup == null) targetCanvasGroup = GetComponent<CanvasGroup>();

        ApplyImmediate(outValue);
    }

    public void FadeIn() => StartTween(inValue, delayIn);

    public void FadeOut() => StartTween(outValue, delayOut);

    private void StartTween(float target, float delay)
    {
        currentSequence?.Kill();
        currentSequence = DOTween.Sequence();

        float safeDuration = Mathf.Max(duration, 0.0001f);

        if (targetRenderer != null)
        {
            Tween matTween = DOVirtual.Float(currentMaterialValue, target, safeDuration, ApplyMaterialProperty)
                .SetEase(easeType);
            currentSequence.Join(matTween);
        }

        if (targetCanvasGroup != null)
        {
            Tween cgTween = targetCanvasGroup.DOFade(target, safeDuration)
                .SetEase(easeType);
            currentSequence.Join(cgTween);
        }

        currentSequence
            .SetDelay(delay)
            .SetLink(gameObject);
    }

    private void ApplyMaterialProperty(float value)
    {
        currentMaterialValue = value;
        targetRenderer.GetPropertyBlock(block);
        block.SetFloat(propertyId, value);
        targetRenderer.SetPropertyBlock(block);
    }

    private void ApplyImmediate(float value)
    {
        if (targetRenderer != null) ApplyMaterialProperty(value);
        if (targetCanvasGroup != null) targetCanvasGroup.alpha = value;
    }

    private void OnDestroy()
    {
        currentSequence?.Kill();
    }
}
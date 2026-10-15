using System.Collections;
using UnityEngine;

public class OnboardingHighlight : MonoBehaviour
{
    [Header("Material")]
    public string alphaPropertyName = "_Alpha";

    [Header("Tween")]
    [Tooltip("Durée en secondes d'un aller simple (min vers max, ou max vers min)")]
    [SerializeField] private float blinkRate = 0.5f;
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Renderer objRenderer;
    private MaterialPropertyBlock propertyBlock;
    private int alphaId;
    private Coroutine blinkCoroutine;

    private void Awake()
    {
        objRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        CacheAlphaId();
        HideHighlights();
    }

    private void OnValidate()
    {
        CacheAlphaId();
    }

    private void CacheAlphaId()
    {
        alphaId = Shader.PropertyToID(alphaPropertyName);
    }

    public void ShowHighlights()
    {
        if (objRenderer == null)
            return;

        // Evite d'empiler plusieurs coroutines si ShowHighlights est appelé plusieurs fois
        if (blinkCoroutine != null)
            StopCoroutine(blinkCoroutine);

        blinkCoroutine = StartCoroutine(Blink());
    }

    public void HideHighlights()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        if (objRenderer != null)
            objRenderer.enabled = false;
    }

    public IEnumerator Blink()
    {
        objRenderer.enabled = true;
        SetAlpha(minAlpha);

        float duration = Mathf.Max(0.01f, blinkRate);
        bool fadingIn = true;

        while (true)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = easing.Evaluate(Mathf.Clamp01(elapsed / duration));
                float from = fadingIn ? minAlpha : maxAlpha;
                float to = fadingIn ? maxAlpha : minAlpha;
                SetAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            fadingIn = !fadingIn;
        }
    }

    private void SetAlpha(float value)
    {
        objRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(alphaId, value);
        objRenderer.SetPropertyBlock(propertyBlock);
    }

    private void OnDisable()
    {
        HideHighlights();
    }
}

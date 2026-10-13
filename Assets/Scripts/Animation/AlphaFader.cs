using System.Collections;
using UnityEngine;

public class AlphaFader : MonoBehaviour
{
    public float fadeDuration = 1f;
    public string alphaProperty = "_Alpha";

    private Renderer _renderer;
    private Coroutine _currentFade;

    private void Init()
    {
        if (_renderer != null) return;

        _renderer = GetComponent<Renderer>();

        if (_renderer == null)
            Debug.LogWarning($"[AlphaFader] No Renderer found on {gameObject.name}.");
        else if (!_renderer.sharedMaterial.HasProperty(alphaProperty))
            Debug.LogWarning($"[AlphaFader] Property '{alphaProperty}' not found on material '{_renderer.sharedMaterial.name}'.");
    }

    public void FadeIn()
    {
        gameObject.SetActive(true);
        Init();
        if (!IsReady()) return;
        StartFade(0f, 1f);
    }

    public void FadeOut()
    {
        Init();
        if (!IsReady()) return;
        StartFade(1f, 0f, disableAfter: true);
    }

    private bool IsReady()
    {
        return _renderer != null && _renderer.sharedMaterial.HasProperty(alphaProperty);
    }

    private void StartFade(float from, float to, bool disableAfter = false)
    {
        if (_currentFade != null) StopCoroutine(_currentFade);
        _currentFade = StartCoroutine(FadeRoutine(from, to, disableAfter));
    }

    private IEnumerator FadeRoutine(float from, float to, bool disableAfter)
    {
        float elapsed = 0f;
        SetAlpha(from);

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (!IsReady()) yield break;
            SetAlpha(Mathf.Lerp(from, to, elapsed / fadeDuration));
            yield return null;
        }

        SetAlpha(to);
        if (disableAfter) gameObject.SetActive(false);
    }

    private void SetAlpha(float alpha)
    {
        if (!IsReady()) return;

        var block = new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(block);
        block.SetFloat(alphaProperty, alpha);
        _renderer.SetPropertyBlock(block);
    }
}
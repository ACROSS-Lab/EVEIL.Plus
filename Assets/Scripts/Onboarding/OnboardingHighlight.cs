using System.Collections;
using UnityEngine;

public class OnboardingHighlight : MonoBehaviour
{
    [SerializeField] private float blinkRate = 0.5f;
    
    private Renderer objRenderer;
    private Coroutine blinkCoroutine;
    
    private void Awake()
    {
        objRenderer = GetComponent<Renderer>();
        HideHighlights();
    }

    public void ShowHighlights()
    {
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
        
        while (true)
        {
            yield return new WaitForSeconds(blinkRate);
            objRenderer.enabled = !objRenderer.enabled;
        }
    }
    
    private void OnDisable()
    {
        HideHighlights();
    }
}
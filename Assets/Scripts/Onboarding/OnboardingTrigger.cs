using System.Collections;
using UnityEngine;

public class OnboardingTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    
    [SerializeField] private GameObject highlightVisual;
    
    private bool hasTriggered;
    
    private Renderer objRenderer;

    void Start()
    {
        objRenderer = GetComponent<Renderer>();
        HideHighlights();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        
        if (hasTriggered)
            return;

        if (!other.CompareTag(playerTag))
            return;

        hasTriggered = true;

        HideHighlights();
        
        SequenceDirector.Instance.PerformAction();
    }
    
    public IEnumerator Blink()
    {
        while (true)
        {
            objRenderer.enabled = !objRenderer.enabled;
            yield return new WaitForSeconds(0.5f);
        }
    }

    public void ShowHighlights()
    {
        StartCoroutine(Blink());
    }
    
    public void HideHighlights()
    {
        StopCoroutine(Blink());
        objRenderer.enabled = false;
    }
}
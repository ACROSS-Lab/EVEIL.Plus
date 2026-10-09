using UnityEngine;

public class OnboardingTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    
    [SerializeField] private GameObject highlightVisual;
    
    private bool hasTriggered;

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
    
    public void ShowHighlights()
    {
        highlightVisual.SetActive(true);
    }

    public void HideHighlights()
    {
        highlightVisual.SetActive(false);
    }
}
using UnityEngine;

public class OnboardingTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    
    [SerializeField] private GameObject highlightVisual;

    [SerializeField] private string sequenceTriggerKey;
    
    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("awdawfawdawd");
        
        if (hasTriggered)
            return;

        if (!other.CompareTag(playerTag))
            return;

        hasTriggered = true;

        HideHighlights();
        
        SequenceDirector.Instance.SetTrigger(sequenceTriggerKey);
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
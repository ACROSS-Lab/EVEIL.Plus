using UnityEngine;
using UnityEngine.Events;

public class OnboardingTrigger : MonoBehaviour
{
    [SerializeField] private string objectTriggerTag = "GameController";
    
    [Header("Onboarding Events")]
    [SerializeField] private UnityEvent onOnboardingTriggerEnter;

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered)
            return;

        if (!other.CompareTag(objectTriggerTag))
            return;

        hasTriggered = true;
        
        onOnboardingTriggerEnter.Invoke();
    }
}
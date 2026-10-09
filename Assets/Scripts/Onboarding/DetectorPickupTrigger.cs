using UnityEngine;

public class DetectorPickupTrigger : MonoBehaviour
{
    [SerializeField] private string controllerTag = "Controller";
    
    [SerializeField] private GameObject objectToActivate;
    [SerializeField] private GameObject objectToHide;
    
    [SerializeField] private string sequenceTriggerKey = "DetectorPickedUp";

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered)
            return;

        if (!other.CompareTag(controllerTag))
            return;

        hasTriggered = true;
        
        objectToActivate.SetActive(true);
        objectToHide.SetActive(false);
        
        SequenceDirector.Instance.SetTrigger(sequenceTriggerKey);
    }
}
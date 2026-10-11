using UnityEngine;
using UnityEngine.Analytics;

public class OnboardingManager : MonoBehaviour
{
    [SerializeField] private PointOfInterest poiToScan;
    [SerializeField] private VRHandMenu playerGuide;
    
    private void OnEnable()
    {
        poiToScan.OnScanCompleted += ContinueOnboarding;
        poiToScan.OnTagValidated += ContinueOnboarding;
        playerGuide.guideOpened.AddListener(OnGuideOpened);
    }
    
    private void OnDisable()
    {
        poiToScan.OnScanCompleted -= ContinueOnboarding;
        poiToScan.OnTagValidated -= ContinueOnboarding;
        playerGuide.guideOpened.RemoveListener(OnGuideOpened);
    }

    private void ContinueOnboarding(PointOfInterest poi)
    {
        SequenceDirector.Instance.PerformAction();
    }

    private void OnGuideOpened()
    {
        SequenceDirector.Instance.PerformAction();
    }
}

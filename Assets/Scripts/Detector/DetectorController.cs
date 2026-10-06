using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class DetectorController : MonoBehaviour
{
    enum DetectorState
    {
        Idle,
        Scanning,
        DisplayingResult,
        Cooldown,
        WaitingForTriggerRelease
    }

    [Header("XR Interactor")]
    [Tooltip("The XR Interactor on the right controller (e.g. NearFarInteractor or XRRayInteractor).")]
    [SerializeField] NearFarInteractor rightInteractor;

    [Header("Input Settings")]
    [Tooltip("Input Action for holding the trigger on the right controller.")]
    [SerializeField] InputActionReference triggerAction;
    [SerializeField] float triggerThreshold = 0.5f;

    [Header("Scanning Configuration")]
    [SerializeField] float scanDuration = 1.0f;
    [SerializeField] float scanCooldown = 0.5f;

    [Header("Movement Disruption Limits")]
    [SerializeField] float maxPositionDelta = 0.12f;
    [SerializeField] float maxAngleDelta = 15.0f;

    [Header("UI Canvas & Sections")]
    [SerializeField] Canvas scanningCanvas;
    [SerializeField] GameObject scanningSection;
    [SerializeField] GameObject detectedSection;
    [SerializeField] GameObject undetectedSection;
    [SerializeField] ScanProgressDisplay progressDisplay;

    [Header("Haptic Feedback on Scan")]
    [SerializeField] bool enableHaptics = true;
    [SerializeField] float hapticAmplitude = 0.1f;
    [SerializeField] float hapticDurationPerFrame = 0.05f;

    [Header("Result Display")]
    [SerializeField] float resultDisplayDuration = 1.5f;

    [Header("Audio Feedback (Optional)")]
    [SerializeField] AudioPitchRandomizer audioPitchRandomizer;
    [SerializeField] AudioClip scanStartSFX;
    [SerializeField] AudioClip scanLoopSFX;
    [SerializeField] AudioClip scanDisruptedSFX;
    [SerializeField] AudioClip detectedSFX;
    [SerializeField] AudioClip undetectedSFX;

    DetectorState currentState = DetectorState.Idle;

    float scanTimer = 0f;
    Vector3 scanStartPosition;
    Quaternion scanStartRotation;
    Coroutine resultRoutine;
    bool isHoveringUI = false;

    void Awake()
    {
        if (rightInteractor == null)
        {
            rightInteractor = transform.parent.GetComponentInChildren<NearFarInteractor>();
        }

        ResetAllUI();
    }

    void OnEnable()
    {
        if (rightInteractor != null)
        {
            rightInteractor.uiHoverEntered.AddListener(OnUIHoverEntered);
            rightInteractor.uiHoverExited.AddListener(OnUIHoverExited);
        }
    }

    void OnDisable()
    {
        if (rightInteractor != null)
        {
            rightInteractor.uiHoverEntered.RemoveListener(OnUIHoverEntered);
            rightInteractor.uiHoverExited.RemoveListener(OnUIHoverExited);
        }

        CancelScan(disrupted: false);
    }

    void OnUIHoverEntered(UIHoverEventArgs args) => isHoveringUI = true;
    void OnUIHoverExited(UIHoverEventArgs args) => isHoveringUI = false;

    void Update()
    {
        switch (currentState)
        {
            case DetectorState.Idle:
                CheckStartScanInput();
                break;

            case DetectorState.Scanning:
                ProcessScanning();
                break;
            
            case DetectorState.WaitingForTriggerRelease:
                if (!IsTriggerHeld())
                {
                    currentState = DetectorState.Idle;
                }
                break;

            case DetectorState.DisplayingResult:
            case DetectorState.Cooldown:
                break;
        }
    }

    void CheckStartScanInput()
    {
        // Do not initiate scanning if currently pointing at or interacting with a UI element
        if (IsOverUI())
        {
            if (IsTriggerHeld())
            {
                currentState = DetectorState.WaitingForTriggerRelease;
            }
            return;
        }

        if (IsTriggerHeld())
        {
            StartScan();
        }
    }

    void StartScan()
    {
        currentState = DetectorState.Scanning;
        scanTimer = 0f;

        scanStartPosition = transform.position;
        scanStartRotation = transform.rotation;

        scanningCanvas.gameObject.SetActive(true);
        scanningSection.SetActive(true);
        detectedSection.SetActive(false);
        undetectedSection.SetActive(false);

        UpdateProgress(0f);

        if (audioPitchRandomizer != null)
        {
            if (scanLoopSFX != null)
            {
                audioPitchRandomizer.PlayLoopSound(scanLoopSFX);
            }
            if (scanStartSFX != null)
            {
                PlayOneShotSFX(scanStartSFX);
            }
        }
    }

    void ProcessScanning()
    {
        if (!IsTriggerHeld())
        {
            CancelScan(disrupted: true);
            return;
        }

        if (IsOverUI())
        {
            CancelScan(disrupted: true);
            return;
        }

        float posDelta = Vector3.Distance(transform.position, scanStartPosition);
        float rotDelta = Quaternion.Angle(transform.rotation, scanStartRotation);

        if (posDelta > maxPositionDelta || rotDelta > maxAngleDelta)
        {
            CancelScan(disrupted: true);
            return;
        }

        scanTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(scanTimer / scanDuration);
        UpdateProgress(progress);

        if (enableHaptics) rightInteractor.SendHapticImpulse(hapticAmplitude, hapticDurationPerFrame);

        if (scanTimer >= scanDuration)
        {
            CompleteScan();
        }
    }

    void CompleteScan()
    {
        StopScanAudio();

        if (scanningSection != null)
        {
            scanningSection.SetActive(false);
        }

        PointOfInterest targetPOI = GetHoveredPointOfInterest();

        if (targetPOI != null && !targetPOI.IsScanned)
        {
            detectedSection.SetActive(true);
            undetectedSection.SetActive(false);

            PlayOneShotSFX(detectedSFX);
            targetPOI.CompleteScan();
        }
        else
        {
            undetectedSection.SetActive(true);
            detectedSection.SetActive(false);

            PlayOneShotSFX(undetectedSFX);
        }

        currentState = DetectorState.DisplayingResult;

        if (resultRoutine != null)
        {
            StopCoroutine(resultRoutine);
        }
        resultRoutine = StartCoroutine(ShowResultAndCooldownRoutine());
    }

    PointOfInterest GetHoveredPointOfInterest()
    {
        if (rightInteractor == null)
            return null;

        List<IXRSelectInteractable> selectedInteractables = rightInteractor.interactablesSelected;

        for(int i = 0; i < selectedInteractables.Count; i++)
        {
            IXRSelectInteractable interactable = selectedInteractables[i];
            PointOfInterest poi = interactable.transform.GetComponent<PointOfInterest>();
            if (poi != null)
            {
                return poi;
            }
        }

        return null;
    }

    void CancelScan(bool disrupted)
    {
        StopScanAudio();

        if (disrupted && audioPitchRandomizer != null && scanDisruptedSFX != null)
        {
            audioPitchRandomizer.PlayPopSound(scanDisruptedSFX);
        }

        scanTimer = 0f;
        UpdateProgress(0f);
        ResetAllUI();

        if (IsTriggerHeld())
        {
            currentState = DetectorState.WaitingForTriggerRelease;
        }
        else
        {
            currentState = DetectorState.Idle;
        }
    }

    IEnumerator ShowResultAndCooldownRoutine()
    {
        yield return new WaitForSeconds(resultDisplayDuration);

        ResetAllUI();
        currentState = DetectorState.Cooldown;

        float timer = 0f;
        while (timer < scanCooldown)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // Must release trigger before scanning again
        while (IsTriggerHeld())
        {
            yield return null;
        }

        currentState = DetectorState.Idle;
        resultRoutine = null;
    }

    void UpdateProgress(float progress01)
    {
        if (progressDisplay != null)
        {
            progressDisplay.SetProgress(progress01);
            return;
        }
    }

    void ResetAllUI()
    {
        if (scanningSection != null) scanningSection.SetActive(false);
        if (detectedSection != null) detectedSection.SetActive(false);
        if (undetectedSection != null) undetectedSection.SetActive(false);

        if (scanningCanvas != null)
        {
            scanningCanvas.gameObject.SetActive(false);
        }

        UpdateProgress(0f);
    }

    bool IsTriggerHeld()
    {
        if (triggerAction != null && triggerAction.action != null)
        {
            float value = triggerAction.action.ReadValue<float>();
            if (value >= triggerThreshold || triggerAction.action.IsPressed())
            {
                return true;
            }
        }
        return false;
    }

    void StopScanAudio()
    {
        if (audioPitchRandomizer != null && audioPitchRandomizer.IsPlaying)
        {
            audioPitchRandomizer.StopSound();
        }
    }

    void PlayOneShotSFX(AudioClip clip)
    {
        if (audioPitchRandomizer != null && clip != null)
        {
            audioPitchRandomizer.PlayPopSound(clip);
        }
    }

    bool IsOverUI()
    {
        if (rightInteractor == null)
            return false;

        if (isHoveringUI)
            return true;

        if (rightInteractor.TryGetCurrentUIRaycastResult(out RaycastResult uiHit) && uiHit.isValid)
            return true;

        if (rightInteractor.TryGetUIModel(out TrackedDeviceModel model) && model.currentRaycast.isValid)
            return true;

        return false;
    }
}
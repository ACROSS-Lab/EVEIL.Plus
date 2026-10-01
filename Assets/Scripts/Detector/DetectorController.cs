using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using System.Collections.Generic;

public class DetectorController : MonoBehaviour
{
    enum DetectorState
    {
        Idle,
        Scanning,
        DisplayingResult,
        Cooldown
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

    [Header("Result Display")]
    [SerializeField] float resultDisplayDuration = 1.5f;

    [Header("Audio Feedback (Optional)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip scanLoopSFX;
    [SerializeField] AudioClip scanDisruptedSFX;
    [SerializeField] AudioClip detectedSFX;
    [SerializeField] AudioClip undetectedSFX;

    DetectorState currentState = DetectorState.Idle;

    float scanTimer = 0f;
    Vector3 scanStartPosition;
    Quaternion scanStartRotation;
    Coroutine resultRoutine;

    void Awake()
    {
        if (rightInteractor == null)
        {
            rightInteractor = transform.parent.GetComponentInChildren<NearFarInteractor>();
        }

        ResetAllUI();
    }

    void OnDisable()
    {
        CancelScan(disrupted: false);
    }

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

            case DetectorState.DisplayingResult:
            case DetectorState.Cooldown:
                break;
        }
    }

    void CheckStartScanInput()
    {
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

        if (audioSource != null && scanLoopSFX != null)
        {
            audioSource.clip = scanLoopSFX;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    void ProcessScanning()
    {
        // 1. Check if trigger was released early
        if (!IsTriggerHeld())
        {
            CancelScan(disrupted: true);
            return;
        }

        // 2. Check for excess movement or rotation
        float posDelta = Vector3.Distance(transform.position, scanStartPosition);
        float rotDelta = Quaternion.Angle(transform.rotation, scanStartRotation);

        if (posDelta > maxPositionDelta || rotDelta > maxAngleDelta)
        {
            CancelScan(disrupted: true);
            return;
        }

        // 3. Accumulate progress
        scanTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(scanTimer / scanDuration);
        UpdateProgress(progress);

        // 4. Completed 1 second hold
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

        if (disrupted && audioSource != null && scanDisruptedSFX != null)
        {
            audioSource.PlayOneShot(scanDisruptedSFX);
        }

        scanTimer = 0f;
        UpdateProgress(0f);
        ResetAllUI();

        currentState = DetectorState.Idle;
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
#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
        {
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            return true;
        }
#endif
        return false;
    }

    void StopScanAudio()
    {
        if (audioSource != null && audioSource.isPlaying && audioSource.clip == scanLoopSFX)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }
    }

    void PlayOneShotSFX(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
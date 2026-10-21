using System;
using UnityEngine;
using DG.Tweening;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PointOfInterest : MonoBehaviour
{
    [Header("Localization")]
    [SerializeField] string localizationKey;
    [SerializeField] LocalizedKey farMarkerLocalizedKey;

    [Header("Gameobject Interactable")]
    [SerializeField] GameObject interactableObject;

    [Header("Canvas & Sub-Elements")]
    [Tooltip("World space canvas containing all POI UI markers and the tag pie menu.")]
    [SerializeField] Canvas displayCanvas;
    [SerializeField] GameObject scannedMarker;
    [SerializeField] TagCarouselDisplay tagCarousel;
    [SerializeField] GameObject farMarker;
    [SerializeField] float heightOffset = 1.5f;

    [Header("Proximity Trigger Distance")]
    [SerializeField] float triggerDistance = 3f;

    [Header("Scanned Marker Animation")]
    [Tooltip("Distance the scanned marker floats upward while fading out.")]
    [SerializeField] float scannedMarkerFloatDistance = 0.4f;
    [Tooltip("Duration in seconds for the scanned marker to float up and fade out.")]
    [SerializeField] float scannedMarkerFadeDuration = 1.5f;

    [Header("Tags & Validation")]
    [SerializeField] TagData emptyTagData;
    [SerializeField] TagData[] availableTags;
    [Tooltip("Tags accepted as correct answers.")]
    [SerializeField] TagData[] validTags;    

    [Header("Audio & Effects")]
    [Tooltip("SFX played when an option has been held long enough to be confirmed.")]
    [SerializeField] AudioClip tagSelectSFX;
    [SerializeField] GameObject validationFXPrefab;
    [SerializeField] AudioClip validationSFX;
    [Tooltip("SFX played when an incorrect tag is validated.")]
    [SerializeField] AudioClip incorrectSFX;
    [SerializeField] AudioSource audioSource;

    [Header("Idle Animation")]
    [Tooltip("Vertical bobbing amplitude applied to the canvas.")]
    [SerializeField] float idleBobAmplitude = 0.05f;
    [Tooltip("Speed of the idle bobbing motion.")]
    [SerializeField] float idleBobSpeed = 1.5f;

    /// <summary>Triggered when an option is confirmed, and when the selection is cleared after an incorrect answer.</summary>
    public event Action<PointOfInterest> OnTagChanged;
    public event Action<PointOfInterest> OnScanCompleted;
    public event Action<PointOfInterest> OnTagValidated;
    /// <summary>Triggered every time the player confirms an option (correct or not).</summary>
    public event Action<PointOfInterest> OnValidateButtonPressed;
    public event Action<PointOfInterest> OnTagValidationCancelled;

    public bool HasTag => currentTagIndex != -1;
    public bool IsScanned => isScanned;
    public bool IsTagValidated => isTagValidated;
    public float ScanProgress01 => scanProgress;

    public TagData CurrentTag =>
        HasTag && availableTags != null && currentTagIndex < availableTags.Length
            ? availableTags[currentTagIndex]
            : null;

    public bool IsCorrect
    {
        get
        {
            if (!HasTag || validTags == null)
                return false;

            TagData currentTag = availableTags[currentTagIndex];

            foreach (TagData validTag in validTags)
            {
                if (validTag == currentTag)
                    return true;
            }

            return false;
        }
    }

    Transform camTransform;
    Collider poiCollider;
    XRSimpleInteractable interactable;
    ScanProgressDisplay scanProgressDisplay;
    GameObject hoverHighlightInstance;

    Vector3 canvasBaseLocalPosition;
    int currentTagIndex = -1;
    float scanProgress = 0f;
    bool isScanned = false;
    bool isTagValidated = false;
    bool isHoveredByRightHand = false;
    bool isHovered = false;

#if UNITY_EDITOR
    void OnValidate()
    {
        FindOrValidateReferences();
        ApplyCanvasPosition();
    }
#endif

    void Awake()
    {
        FindOrValidateReferences();
        SetupCanvasPositionAndBob();
        SetupLocalization();
        SetupTagCarousel();

        // Canvas components must be off initially before any interaction
        SetCanvasSubComponentsActive(false);
    }

    void Start()
    {
        if (Camera.main != null)
        {
            camTransform = Camera.main.transform;
        }
    }

    void OnEnable()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }
    }

    void OnDisable()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }
    }

    void OnDestroy()
    {
        if (tagCarousel != null)
        {
            tagCarousel.OnOptionConfirmed -= HandleOptionConfirmed;
            tagCarousel.OnIncorrectFeedbackFinished -= HandleIncorrectFeedbackFinished;
        }
    }

    void Update()
    {
        if (camTransform == null)
            return;

        // Proper squared distance calculation
        float sqrDistance = (camTransform.position - transform.position).sqrMagnitude;
        bool inRange = sqrDistance <= (triggerDistance * triggerDistance);

        UpdateProximityDisplay(inRange);
    }

    void FindOrValidateReferences()
    {
        poiCollider = GetComponent<BoxCollider>();
        interactable = GetComponent<XRSimpleInteractable>();
    }

    void SetupCanvasPositionAndBob()
    {
        ApplyCanvasPosition();

        if (displayCanvas != null)
        {
            StartIdleBob(displayCanvas.transform, canvasBaseLocalPosition.y);
        }
    }

    void ApplyCanvasPosition()
    {
        if (displayCanvas == null)
            return;

        BoxCollider boxCollider = GetComponent<BoxCollider>();
        Vector3 centerOffset = boxCollider != null ? boxCollider.center : Vector3.zero;

        canvasBaseLocalPosition = new Vector3(
            centerOffset.x,
            transform.position.y + heightOffset,
            centerOffset.z
        );

        displayCanvas.transform.localPosition = canvasBaseLocalPosition;
    }

    void StartIdleBob(Transform target, float baseHeight)
    {
        target
            .DOLocalMoveY(baseHeight + idleBobAmplitude, 1f / idleBobSpeed)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetDelay(UnityEngine.Random.Range(0f, 1f / idleBobSpeed));
    }

    void SetupLocalization()
    {
        if (farMarker != null)
        {
            farMarkerLocalizedKey.localizationKey = localizationKey;
            farMarkerLocalizedKey.UpdateText();
        }
    }

    void SetupTagCarousel()
    {
        if (tagCarousel != null)
        {
            tagCarousel.Initialize(availableTags, emptyTagData);
            tagCarousel.OnOptionConfirmed += HandleOptionConfirmed;
            tagCarousel.OnIncorrectFeedbackFinished += HandleIncorrectFeedbackFinished;
        }
    }

    void SetCanvasSubComponentsActive(bool active)
    {
        if (scannedMarker != null) scannedMarker.SetActive(active);
        if (tagCarousel != null) tagCarousel.gameObject.SetActive(active);
        if (farMarker != null) farMarker.SetActive(active);
    }

    public void CompleteScan()
    {
        isScanned = true;
        isHoveredByRightHand = false;
        SetHovered(false);

        if (validationFXPrefab != null)
        {
            Instantiate(validationFXPrefab, transform.position, Quaternion.identity);
        }

        if (audioSource != null && validationSFX != null)
        {
            audioSource.PlayOneShot(validationSFX);
        }

        PlayScannedMarkerAnimation();

        // Disable collider and interactable so player interacts with the pie menu next
        if (poiCollider != null)
        {
            poiCollider.enabled = false;
        }

        if (interactable != null)
        {
            interactable.enabled = false;
        }

        OnScanCompleted?.Invoke(this);
    }

    void PlayScannedMarkerAnimation()
    {
        if (scannedMarker == null)
            return;

        scannedMarker.SetActive(true);

        CanvasGroup group = scannedMarker.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = scannedMarker.AddComponent<CanvasGroup>();
        }

        group.alpha = 1f;
        Vector3 initialPos = scannedMarker.transform.localPosition;

        scannedMarker.transform.DOKill();
        group.DOKill();

        Sequence seq = DOTween.Sequence();
        seq.Join(scannedMarker.transform.DOLocalMoveY(initialPos.y + scannedMarkerFloatDistance, scannedMarkerFadeDuration).SetEase(Ease.OutQuad));
        seq.Join(group.DOFade(0f, scannedMarkerFadeDuration).SetEase(Ease.InQuad));
        seq.OnComplete(() =>
        {
            scannedMarker.SetActive(false);
            scannedMarker.transform.localPosition = initialPos;
            group.alpha = 1f;
        });
    }

    void UpdateProximityDisplay(bool inRange)
    {
        if (!isScanned)
        {
            if (tagCarousel != null && tagCarousel.gameObject.activeSelf)
            {
                tagCarousel.gameObject.SetActive(false);
            }

            if (farMarker != null && farMarker.activeSelf)
            {
                farMarker.SetActive(false);
            }

            return;
        }

        // Once scanned: close proximity shows the pie menu, far distance shows FarMarker
        if (tagCarousel != null && tagCarousel.gameObject.activeSelf != inRange)
        {
            tagCarousel.gameObject.SetActive(inRange);
        }

        if (farMarker != null && farMarker.activeSelf == inRange)
        {
            farMarker.SetActive(!inRange);
        }
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (isScanned)
            return;

        if (IsRightHandInteractor(args.interactorObject))
        {
            isHoveredByRightHand = true;
            SetHovered(true);
        }
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (IsRightHandInteractor(args.interactorObject))
        {
            isHoveredByRightHand = false;
            SetHovered(false);
        }
    }

    bool IsRightHandInteractor(IXRInteractor interactor)
    {
        if (interactor is NearFarInteractor nearFar)
        {
            return nearFar.handedness == InteractorHandedness.Right;
        }

        if (interactor is Component comp)
        {
            NearFarInteractor parentNearFar = comp.GetComponentInParent<NearFarInteractor>();
            if (parentNearFar != null)
            {
                return parentNearFar.handedness == InteractorHandedness.Right;
            }

            if (comp.name.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    void SetHovered(bool hovered)
    {
        isHovered = hovered;

        if (hoverHighlightInstance != null && hoverHighlightInstance.activeSelf != hovered)
        {
            hoverHighlightInstance.SetActive(hovered);
        }
    }

    /// <summary>
    /// Called by the pie menu once an option has been held long enough.
    /// Selects the tag, checks it, and asks the pie menu to show the matching feedback.
    /// </summary>
    void HandleOptionConfirmed(int optionIndex)
    {
        if (!isScanned || isTagValidated)
            return;

        if (availableTags == null || optionIndex < 0 || optionIndex >= availableTags.Length)
            return;

        currentTagIndex = optionIndex;

        if (audioSource != null && tagSelectSFX != null)
        {
            audioSource.PlayOneShot(tagSelectSFX);
        }

        OnTagChanged?.Invoke(this);

        if (IsCorrect)
        {
            isTagValidated = true;

            if (tagCarousel != null)
            {
                tagCarousel.ShowCorrectFeedback(currentTagIndex);
            }

            if (audioSource != null && validationSFX != null)
            {
                audioSource.PlayOneShot(validationSFX);
            }

            if (validationFXPrefab != null)
            {
                Instantiate(validationFXPrefab, transform.position, Quaternion.identity);
            }

            OnTagValidated?.Invoke(this);
        }
        else
        {
            if (tagCarousel != null)
            {
                tagCarousel.ShowIncorrectFeedback(currentTagIndex);
            }

            if (audioSource != null && incorrectSFX != null)
            {
                audioSource.PlayOneShot(incorrectSFX);
            }
        }

        OnValidateButtonPressed?.Invoke(this);
    }

    /// <summary>
    /// Called by the pie menu when the incorrect feedback is over and everything is back to its initial state.
    /// </summary>
    void HandleIncorrectFeedbackFinished()
    {
        if (isTagValidated)
            return;

        currentTagIndex = -1;
        OnTagChanged?.Invoke(this);
    }

    int FindFirstValidTagIndex()
    {
        if (validTags == null || availableTags == null)
            return -1;

        foreach (TagData validTag in validTags)
        {
            for (int i = 0; i < availableTags.Length; i++)
            {
                if (availableTags[i] == validTag)
                    return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Validates the chosen tag or unlocks it for editing.
    /// When validated without any selected tag, the first valid tag is shown in the center.
    /// Unlocking resets the pie menu and clears the selection.
    /// </summary>
    public void SetTagValidated(bool validated)
    {
        if (isTagValidated == validated)
            return;

        isTagValidated = validated;

        if (validated && !HasTag)
        {
            int validIndex = FindFirstValidTagIndex();
            if (validIndex >= 0)
            {
                currentTagIndex = validIndex;
            }
        }

        if (tagCarousel != null)
        {
            tagCarousel.SetValidated(validated, currentTagIndex);
        }

        if (!validated)
        {
            currentTagIndex = -1;
        }

        if (validated)
        {
            OnTagValidated?.Invoke(this);
        }
        else
        {
            OnTagValidationCancelled?.Invoke(this);
        }
    }
}

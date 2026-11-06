using System;
using System.Collections.Generic;
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

    [Header("Far Marker Pollutant")]
    [Tooltip("Optional. TagDisplay placed as a child of the Far Marker. It shows the pollutant once it has been found (tag validated).")]
    [SerializeField] TagDisplay farTagDisplay;

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
    [Tooltip("Tags required as correct answers. All valid tags assigned here must be selected to complete this POI.")]
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
    public event Action<PointOfInterest> OnFirstPartialTagFound;

    public bool HasTag => currentTagIndex != -1 || foundTagIndices.Count > 0;
    public bool IsScanned => isScanned;
    public bool IsTagValidated => isTagValidated;
    public float ScanProgress01 => scanProgress;

    /// <summary>True if the most recently confirmed tag option was one of the valid tags.</summary>
    public bool IsLastSelectionCorrect => lastSelectionWasCorrect;

    /// <summary>Total number of valid tags required to complete this Point of Interest.</summary>
    public int RequiredTagCount => Mathf.Max(1, GetValidOptionCount());

    /// <summary>Number of valid tags remaining to be found by the player.</summary>
    public int RemainingTagCount => Mathf.Max(0, RequiredTagCount - foundTagIndices.Count);

    public TagData CurrentTag =>
        currentTagIndex >= 0 && availableTags != null && currentTagIndex < availableTags.Length
            ? availableTags[currentTagIndex]
            : (foundTagIndices.Count > 0 && availableTags != null && foundTagIndices[0] < availableTags.Length
                ? availableTags[foundTagIndices[0]]
                : null);

    /// <summary>True once all required valid tags have been found.</summary>
    public bool IsCorrect
    {
        get
        {
            int goalCount = GetValidOptionCount();
            if (goalCount <= 0)
                return false;

            return foundTagIndices.Count >= goalCount;
        }
    }

    Transform camTransform;
    Collider poiCollider;
    XRSimpleInteractable interactable;
    ScanProgressDisplay scanProgressDisplay;
    GameObject hoverHighlightInstance;

    Vector3 canvasBaseLocalPosition;
    readonly List<int> foundTagIndices = new List<int>();
    int currentTagIndex = -1;
    bool lastSelectionWasCorrect = false;
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

        // The pollutant is hidden on the far marker until it has been found
        RefreshFarTagDisplay();
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
            tagCarousel.Initialize(availableTags, emptyTagData, RequiredTagCount);
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

    /// <summary>
    /// Shows the found pollutant(s) on the far marker when the tag is validated,
    /// hides it otherwise. The display should be a child of the far marker so it
    /// follows the far marker visibility (only visible when the player is far).
    /// </summary>
    void RefreshFarTagDisplay()
    {
        if (farTagDisplay == null)
            return;

        if (isTagValidated)
        {
            List<TagData> foundTags = GetFoundTagDataList();
            if (foundTags.Count > 0)
            {
                farTagDisplay.SetMultipleData(foundTags);
                farTagDisplay.gameObject.SetActive(true);
                return;
            }
        }

        farTagDisplay.gameObject.SetActive(false);
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
    /// Checks whether the option at the given index in availableTags is one of the validTags.
    /// </summary>
    bool IsOptionValid(int optionIndex)
    {
        if (availableTags == null || validTags == null || optionIndex < 0 || optionIndex >= availableTags.Length)
            return false;

        TagData candidate = availableTags[optionIndex];
        if (candidate == null)
            return false;

        for (int i = 0; i < validTags.Length; i++)
        {
            if (validTags[i] == candidate)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Counts how many valid options must be found to complete this POI.
    /// </summary>
    int GetValidOptionCount()
    {
        if (validTags == null || validTags.Length == 0)
            return 0;

        int count = 0;
        if (availableTags != null && availableTags.Length > 0)
        {
            for (int i = 0; i < availableTags.Length; i++)
            {
                if (IsOptionValid(i))
                    count++;
            }
        }

        if (count == 0)
        {
            for (int i = 0; i < validTags.Length; i++)
            {
                if (validTags[i] != null)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Returns the list of TagData objects for all valid options found so far.
    /// </summary>
    List<TagData> GetFoundTagDataList()
    {
        List<TagData> result = new List<TagData>(foundTagIndices.Count);
        if (availableTags == null)
            return result;

        for (int i = 0; i < foundTagIndices.Count; i++)
        {
            int idx = foundTagIndices[i];
            if (idx >= 0 && idx < availableTags.Length && availableTags[idx] != null)
            {
                result.Add(availableTags[idx]);
            }
        }

        return result;
    }

    /// <summary>
    /// Called by the pie menu once an option has been held long enough.
    /// Selects the tag, checks if it is valid, and either marks it as found (if more tags remain)
    /// or completes the POI validation once all required tags are found.
    /// </summary>
    void HandleOptionConfirmed(int optionIndex)
    {
        if (!isScanned || isTagValidated)
            return;

        if (availableTags == null || optionIndex < 0 || optionIndex >= availableTags.Length)
            return;

        if (foundTagIndices.Contains(optionIndex))
            return;

        currentTagIndex = optionIndex;
        lastSelectionWasCorrect = IsOptionValid(optionIndex);

        if (lastSelectionWasCorrect)
        {
            foundTagIndices.Add(optionIndex);
        }

        if (audioSource != null && tagSelectSFX != null)
        {
            audioSource.PlayOneShot(tagSelectSFX);
        }

        OnTagChanged?.Invoke(this);

        if (lastSelectionWasCorrect)
        {
            if (audioSource != null && validationSFX != null)
            {
                audioSource.PlayOneShot(validationSFX);
            }

            if (validationFXPrefab != null)
            {
                Instantiate(validationFXPrefab, transform.position, Quaternion.identity);
            }

            if (IsCorrect)
            {
                isTagValidated = true;

                if (tagCarousel != null)
                {
                    tagCarousel.ShowCorrectFeedback(GetFoundTagDataList());
                }

                RefreshFarTagDisplay();

                OnTagValidated?.Invoke(this);
            }
            else
            {
                if (tagCarousel != null)
                {
                    tagCarousel.MarkOptionFound(optionIndex, RemainingTagCount, RequiredTagCount, foundTagIndices.Count);
                }
                if (foundTagIndices.Count == 1)
                {
                    OnFirstPartialTagFound?.Invoke(this);
                }
            }
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
    /// Called by the pie menu when the incorrect feedback is over and the remaining options return to idle.
    /// </summary>
    void HandleIncorrectFeedbackFinished()
    {
        if (isTagValidated)
            return;

        currentTagIndex = foundTagIndices.Count > 0 ? foundTagIndices[foundTagIndices.Count - 1] : -1;
        OnTagChanged?.Invoke(this);
    }

    void PopulateAllValidTagIndices()
    {
        foundTagIndices.Clear();

        if (availableTags == null || validTags == null)
            return;

        for (int i = 0; i < availableTags.Length; i++)
        {
            if (IsOptionValid(i))
            {
                foundTagIndices.Add(i);
            }
        }

        if (foundTagIndices.Count > 0)
        {
            currentTagIndex = foundTagIndices[0];
        }
    }

    /// <summary>
    /// Validates the required tags or unlocks them for editing.
    /// When validated without all tags selected, all valid tags are automatically populated and shown in the center.
    /// Unlocking resets the pie menu and clears all selections.
    /// </summary>
    public void SetTagValidated(bool validated)
    {
        if (isTagValidated == validated)
            return;

        isTagValidated = validated;

        if (validated && !IsCorrect)
        {
            PopulateAllValidTagIndices();
        }
        else if (!validated)
        {
            foundTagIndices.Clear();
            currentTagIndex = -1;
            lastSelectionWasCorrect = false;
        }

        if (tagCarousel != null)
        {
            tagCarousel.SetValidated(validated, GetFoundTagDataList(), RequiredTagCount);
        }

        RefreshFarTagDisplay();

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

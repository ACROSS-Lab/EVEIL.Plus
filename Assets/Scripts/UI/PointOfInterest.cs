using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PointOfInterest : MonoBehaviour
{
    [Header("Localization")]
    [SerializeField] string localizationKey;
    [Tooltip("Localization key shown instead of the name before the source has been scanned, e.g. a key pointing to \"?\" in the CSV.")]
    [SerializeField] string unscannedPlaceholderKey = "poi_unscanned_placeholder";

    [Header("Info Display")]
    [SerializeField] GameObject displayPrefab;
    [SerializeField] float heightOffset = 1.5f;
    [SerializeField] float triggerDistance = 3f;
    [Tooltip("Color applied to the info display's background while the detector is pointed at this source.")]
    [SerializeField] Color displayHoverColor = Color.yellow;

    [Header("Tags")]
    [SerializeField] GameObject tagPrefab;
    [SerializeField] TagData emptyTagData;
    [SerializeField] TagData[] availableTags;

    [Tooltip("Tags accepted as correct answers")]
    [SerializeField] TagData[] validTags;

    [SerializeField] float tagHeightOffset = 2f;
    [Tooltip("Played whenever a tag is selected, regardless of whether it's correct.")]
    [SerializeField] AudioClip tagSelectSFX;
    [Tooltip("Scale multiplier reached mid-bounce when a tag is selected, before settling back to normal size.")]
    [SerializeField] float tagPunchScale = 1.25f;
    [SerializeField] float tagPunchDuration = 0.2f;

    [Header("Far Marker")]
    [SerializeField] GameObject farMarkerPrefab;
    [SerializeField] float farMarkerHeightOffset = 2f;

    [Header("Scanning")]
    [Tooltip("UI shown while the detector is pointed at this source, displaying scan progress.")]
    [SerializeField] GameObject scanUIPrefab;
    [SerializeField] float scanUIHeightOffset = 2.5f;
    [Tooltip("Time in seconds needed to fully analyze the source while it is being pointed at.")]
    [SerializeField] float scanDuration = 2f;
    [Tooltip("How fast scan progress falls off (in 'scans per second') once the detector stops pointing at the source. Use a large value for a near-instant reset.")]
    [SerializeField] float scanDecaySpeed = 1f;

    [Header("Scan Validation")]
    [SerializeField] GameObject validationFXPrefab;
    [SerializeField] AudioClip validationSFX;
    [SerializeField] AudioSource audioSource;
    [Tooltip("Duration in seconds of the bounce/pop animation played when the scanned indicator appears.")]
    [SerializeField] float scanCompletePopDuration = 0.35f;

    [Header("Scanned Indicator")]
    [Tooltip("Persistent indicator shown once the source has been fully scanned. Stays visible even after the player walks away.")]
    [SerializeField] GameObject scannedIndicatorPrefab;
    [SerializeField] float scannedIndicatorHeightOffset = 2f;
    [Tooltip("Uniform scale applied to the indicator once the player leaves range, to reduce visual clutter.")]
    [SerializeField] float scannedIndicatorMinifiedScale = 0.4f;

    [Header("Idle Animation")]
    [Tooltip("Vertical bobbing amplitude, in local units, applied to the far marker and the scanned indicator.")]
    [SerializeField] float idleBobAmplitude = 0.05f;
    [Tooltip("Speed of the idle bobbing motion.")]
    [SerializeField] float idleBobSpeed = 1.5f;

    public event Action<PointOfInterest> OnTagChanged;
    public event Action<PointOfInterest> OnScanCompleted;

    public bool HasTag => currentTagIndex != -1;
    public bool IsScanned => isScanned;
    public float ScanProgress01 => scanProgress;

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

    private GameObject displayRoot;
    private TextMeshProUGUI textLabel;
    private Image displayBackground;
    private Color displayNormalColor;
    private LocalizedKey localizedKey;
    private Transform camTransform;
    private bool isVisible = false;

    private GameObject tagInstance;
    private TagCarouselDisplay tagCarousel;
    private Vector3 tagBaseScale = Vector3.one;
    private Image tagBackground;
    private Color tagNormalColor;
    private int currentTagIndex = -1;
    private XRSimpleInteractable interactable;

    private GameObject farMarkerInstance;

    private GameObject scanUIInstance;
    private ScanProgressDisplay scanProgressDisplay;
    private bool isHoveredForScan = false;
    private float scanProgress = 0f;
    private bool isScanned = false;

    private GameObject scannedIndicatorInstance;
    private Vector3 scannedIndicatorBaseScale = Vector3.one;
    private bool isPlayingScanCompletePop = false;

#if UNITY_EDITOR
    private void Reset()
    {
        if (GetComponent<LocalizedKey>() == null)
        {
            LocalizedKey localizedKey = gameObject.AddComponent<LocalizedKey>();
            Undo.RegisterCreatedObjectUndo(localizedKey, "Add LocalizedKey");
        }
    }

    private void OnValidate()
    {
        LocalizedKey localizedKey = GetComponent<LocalizedKey>();

        if (localizedKey != null)
        {
            localizedKey.localizationKey = localizationKey;
        }
    }
#endif

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        if (interactable != null)
        {
            interactable.enabled = false;
        }

        localizedKey = GetComponent<LocalizedKey>();

        if (localizedKey == null)
        {
            localizedKey = gameObject.AddComponent<LocalizedKey>();
        }

        localizedKey.localizationKey = unscannedPlaceholderKey;

        if (displayPrefab != null)
        {
            displayRoot = Instantiate(displayPrefab, transform);
            displayRoot.transform.localPosition = Vector3.up * heightOffset;
            displayRoot.SetActive(false);

            textLabel = displayRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            localizedKey.textComponent = textLabel;

            displayBackground = displayRoot.GetComponentInChildren<Image>(true);

            if (displayBackground != null)
            {
                displayNormalColor = displayBackground.color;
            }
        }

        if (tagPrefab != null)
        {
            tagInstance = Instantiate(tagPrefab, transform);
            tagInstance.transform.localPosition = Vector3.up * tagHeightOffset;
            tagBaseScale = tagInstance.transform.localScale;

            tagBackground = tagInstance.GetComponentInChildren<Image>(true);

            if (tagBackground != null)
            {
                tagNormalColor = tagBackground.color;
            }

            tagInstance.SetActive(false);

            tagCarousel = tagInstance.GetComponentInChildren<TagCarouselDisplay>(true);

            if (tagCarousel != null)
            {
                // Charge le carrousel avec le emptyTagData visible au départ
                tagCarousel.Initialize(availableTags, emptyTagData);
            }
        }

        if (farMarkerPrefab != null)
        {
            farMarkerInstance = Instantiate(farMarkerPrefab, transform);
            farMarkerInstance.transform.localPosition = Vector3.up * farMarkerHeightOffset;

            TextMeshProUGUI farMarkerTextLabel = farMarkerInstance.GetComponentInChildren<TextMeshProUGUI>(true);

            if (farMarkerTextLabel != null)
            {
                LocalizedKey farMarkerLocalizedKey = farMarkerTextLabel.GetComponent<LocalizedKey>();

                if (farMarkerLocalizedKey == null)
                {
                    farMarkerLocalizedKey = farMarkerTextLabel.gameObject.AddComponent<LocalizedKey>();
                }

                farMarkerLocalizedKey.textComponent = farMarkerTextLabel;
                farMarkerLocalizedKey.localizationKey = localizationKey;
            }

            StartIdleBob(farMarkerInstance.transform, farMarkerHeightOffset);
            farMarkerInstance.SetActive(false);
        }

        if (scanUIPrefab != null)
        {
            scanUIInstance = Instantiate(scanUIPrefab, transform);
            scanUIInstance.transform.localPosition = Vector3.up * scanUIHeightOffset;
            scanProgressDisplay = scanUIInstance.GetComponent<ScanProgressDisplay>();
            scanUIInstance.SetActive(false);
        }

        if (scannedIndicatorPrefab != null)
        {
            scannedIndicatorInstance = Instantiate(scannedIndicatorPrefab, transform);
            scannedIndicatorInstance.transform.localPosition = Vector3.up * scannedIndicatorHeightOffset;
            scannedIndicatorBaseScale = scannedIndicatorInstance.transform.localScale;

            StartIdleBob(scannedIndicatorInstance.transform, scannedIndicatorHeightOffset);
            scannedIndicatorInstance.SetActive(false);
        }
    }

    private void StartIdleBob(Transform target, float baseHeight)
    {
        target
            .DOLocalMoveY(baseHeight + idleBobAmplitude, 1f / idleBobSpeed)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetDelay(UnityEngine.Random.Range(0f, 1f / idleBobSpeed));
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            camTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (camTransform == null)
            return;

        UpdateInfoDisplay();
        UpdateTagVisibility();
        UpdateFarMarkerVisibility();
        UpdateScanning();
        UpdateScannedIndicator();
    }

    private void UpdateInfoDisplay()
    {
        if (displayRoot == null)
            return;

        float distance = Vector3.Distance(camTransform.position, transform.position);
        bool shouldBeVisible = distance <= triggerDistance;

        if (shouldBeVisible != isVisible)
        {
            isVisible = shouldBeVisible;
            displayRoot.SetActive(isVisible);

            if (interactable != null)
            {
                interactable.enabled = isVisible;
            }
        }

        if (isVisible)
        {
            displayRoot.transform.rotation =
                Quaternion.LookRotation(displayRoot.transform.position - camTransform.position);
        }
    }

    private void UpdateTagVisibility()
    {
        if (tagInstance == null)
            return;

        bool shouldShowTag = isVisible && isScanned;

        if (tagInstance.activeSelf != shouldShowTag)
        {
            tagInstance.SetActive(shouldShowTag);
        }

        if (shouldShowTag)
        {
            tagInstance.transform.rotation =
                Quaternion.LookRotation(tagInstance.transform.position - camTransform.position);
        }
    }

    private void UpdateFarMarkerVisibility()
    {
        if (farMarkerInstance == null)
            return;

        bool shouldShowMarker = HasTag && !isVisible;

        if (farMarkerInstance.activeSelf != shouldShowMarker)
        {
            farMarkerInstance.SetActive(shouldShowMarker);
        }

        if (shouldShowMarker)
        {
            farMarkerInstance.transform.rotation =
                Quaternion.LookRotation(farMarkerInstance.transform.position - camTransform.position);
        }
    }

    private void UpdateScanning()
    {
        if (scanUIInstance == null || isScanned)
            return;

        if (isHoveredForScan)
        {
            scanProgress += Time.deltaTime / scanDuration;
        }
        else
        {
            scanProgress -= Time.deltaTime * scanDecaySpeed;
        }

        scanProgress = Mathf.Clamp01(scanProgress);

        bool shouldShowScanUI = scanProgress > 0f && isVisible;

        if (scanUIInstance.activeSelf != shouldShowScanUI)
        {
            scanUIInstance.SetActive(shouldShowScanUI);
        }

        if (shouldShowScanUI)
        {
            if (scanProgressDisplay != null)
            {
                scanProgressDisplay.SetProgress(scanProgress);
            }

            scanUIInstance.transform.rotation =
                Quaternion.LookRotation(scanUIInstance.transform.position - camTransform.position);
        }

        if (scanProgress >= 1f)
        {
            CompleteScan();
        }
    }

    private void UpdateScannedIndicator()
    {
        if (scannedIndicatorInstance == null || !isScanned)
            return;

        if (!isPlayingScanCompletePop)
        {
            float targetScale = isVisible ? 1f : scannedIndicatorMinifiedScale;

            scannedIndicatorInstance.transform.localScale =
                scannedIndicatorBaseScale * targetScale;
        }

        scannedIndicatorInstance.transform.rotation =
            Quaternion.LookRotation(scannedIndicatorInstance.transform.position - camTransform.position);
    }

    private void CompleteScan()
    {
        isScanned = true;
        isHoveredForScan = false;

        if (scanUIInstance != null)
        {
            scanUIInstance.SetActive(false);
        }

        if (localizedKey != null)
        {
            localizedKey.localizationKey = localizationKey;
            localizedKey.UpdateText();
        }

        if (validationFXPrefab != null)
        {
            Instantiate(validationFXPrefab, transform.position, Quaternion.identity);
        }

        if (audioSource != null && validationSFX != null)
        {
            audioSource.PlayOneShot(validationSFX);
        }

        if (scannedIndicatorInstance != null)
        {
            scannedIndicatorInstance.SetActive(true);
            scannedIndicatorInstance.transform.localScale = Vector3.zero;

            Vector3 targetScale =
                scannedIndicatorBaseScale *
                (isVisible ? 1f : scannedIndicatorMinifiedScale);

            isPlayingScanCompletePop = true;

            scannedIndicatorInstance.transform
                .DOScale(targetScale, scanCompletePopDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() => isPlayingScanCompletePop = false);
        }

        OnScanCompleted?.Invoke(this);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (displayBackground != null)
        {
            displayBackground.color = displayHoverColor;
        }

        if (tagBackground != null)
        {
            tagBackground.color = displayHoverColor;
        }

        if (isScanned)
            return;

        isHoveredForScan = true;
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (displayBackground != null)
        {
            displayBackground.color = displayNormalColor;
        }

        if (tagBackground != null)
        {
            tagBackground.color = tagNormalColor;
        }

        isHoveredForScan = false;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        CycleTag();
    }

    private void CycleTag()
    {
        if (!isScanned)
            return;

        if (tagCarousel == null || availableTags == null || availableTags.Length == 0)
            return;

        bool isFirstSelection = currentTagIndex == -1;

        if (isFirstSelection)
        {
            currentTagIndex = 0;
            tagCarousel.EnableAvailableTagsMode();
            tagCarousel.SetIndex(currentTagIndex, animate: true);
        }
        else
        {
            currentTagIndex = (currentTagIndex + 1) % availableTags.Length;
            tagCarousel.SetIndex(currentTagIndex, animate: true);
        }

        if (audioSource != null && tagSelectSFX != null)
        {
            audioSource.PlayOneShot(tagSelectSFX);
        }

        if (tagInstance != null)
        {
            tagInstance.transform.DOKill();
            tagInstance.transform.localScale = tagBaseScale;

            tagInstance.transform.DOPunchScale(
                tagBaseScale * (tagPunchScale - 1f),
                tagPunchDuration,
                vibrato: 1,
                elasticity: 0.5f
            );
        }

        OnTagChanged?.Invoke(this);
    }
}
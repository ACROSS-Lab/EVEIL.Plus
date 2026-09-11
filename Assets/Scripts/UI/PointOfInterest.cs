using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PointOfInterest : MonoBehaviour
{
    [Header("Localization")]
    [SerializeField] string localizationKey;

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

    [Header("Scanned Indicator")]
    [Tooltip("Persistent indicator shown once the source has been fully scanned. Stays visible even after the player walks away.")]
    [SerializeField] GameObject scannedIndicatorPrefab;
    [SerializeField] float scannedIndicatorHeightOffset = 2f;
    [Tooltip("Uniform scale applied to the indicator once the player leaves range, to reduce visual clutter.")]
    [SerializeField] float scannedIndicatorMinifiedScale = 0.4f;

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
    private Transform camTransform;
    private bool isVisible = false;

    private GameObject tagInstance;
    private TagDisplay tagDisplay;
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

#if UNITY_EDITOR
    private void Reset()
    {
        // Add the localization component automatically in the editor.
        if (GetComponent<LocalizedKey>() == null)
        {
            LocalizedKey localizedKey = gameObject.AddComponent<LocalizedKey>();
            Undo.RegisterCreatedObjectUndo(
                localizedKey,
                "Add LocalizedKey"
            );
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

        // Start disabled, matching isVisible's initial value, so the POI can't be
        // selected before the player has come into range at least once.
        if (interactable != null)
        {
            interactable.enabled = false;
        }

        // Safety net in case the LocalizedKey component was not added in the editor.
        LocalizedKey localizedKey = GetComponent<LocalizedKey>();

        if (localizedKey == null)
        {
            localizedKey = gameObject.AddComponent<LocalizedKey>();
        }

        localizedKey.localizationKey = localizationKey;

        // Create the information display.
        if (displayPrefab != null)
        {
            displayRoot = Instantiate(displayPrefab, transform);
            displayRoot.transform.localPosition = Vector3.up * heightOffset;
            displayRoot.SetActive(false);

            textLabel =
                displayRoot.GetComponentInChildren<TextMeshProUGUI>(true);

            localizedKey.textComponent = textLabel;

            displayBackground =
                displayRoot.GetComponentInChildren<Image>(true);

            if (displayBackground != null)
            {
                displayNormalColor = displayBackground.color;
            }
        }

        // Create the tag display.
        if (tagPrefab != null)
        {
            tagInstance = Instantiate(tagPrefab, transform);
            tagInstance.transform.localPosition =
                Vector3.up * tagHeightOffset;

            tagDisplay = tagInstance.GetComponent<TagDisplay>();

            tagInstance.SetActive(false);
        }

        // Create the far marker.
        if (farMarkerPrefab != null)
        {
            farMarkerInstance =
                Instantiate(farMarkerPrefab, transform);

            farMarkerInstance.transform.localPosition =
                Vector3.up * farMarkerHeightOffset;

            farMarkerInstance.SetActive(false);
        }

        // Create the scan progress UI.
        if (scanUIPrefab != null)
        {
            scanUIInstance = Instantiate(scanUIPrefab, transform);
            scanUIInstance.transform.localPosition =
                Vector3.up * scanUIHeightOffset;

            scanProgressDisplay = scanUIInstance.GetComponent<ScanProgressDisplay>();

            scanUIInstance.SetActive(false);
        }

        // Create the scanned indicator (persists once fully scanned).
        if (scannedIndicatorPrefab != null)
        {
            scannedIndicatorInstance = Instantiate(scannedIndicatorPrefab, transform);
            scannedIndicatorInstance.transform.localPosition =
                Vector3.up * scannedIndicatorHeightOffset;

            // Remember the prefab's own scale so minifying it later multiplies
            // from that baseline instead of overwriting it with (1,1,1).
            scannedIndicatorBaseScale = scannedIndicatorInstance.transform.localScale;

            scannedIndicatorInstance.SetActive(false);
        }
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

        // Show the empty tag until the player selects a tag.
        if (tagDisplay != null && emptyTagData != null)
        {
            tagDisplay.SetData(emptyTagData);
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

        float distance =
            Vector3.Distance(
                camTransform.position,
                transform.position
            );

        bool shouldBeVisible =
            distance <= triggerDistance;

        if (shouldBeVisible != isVisible)
        {
            isVisible = shouldBeVisible;
            displayRoot.SetActive(isVisible);

            // Disabling the interactable unregisters it from the interaction manager,
            // so it can no longer be hovered or selected while out of range.
            if (interactable != null)
            {
                interactable.enabled = isVisible;
            }
        }

        if (isVisible)
        {
            // Keep the display facing the camera.
            displayRoot.transform.rotation =
                Quaternion.LookRotation(
                    displayRoot.transform.position -
                    camTransform.position
                );
        }
    }

    private void UpdateTagVisibility()
    {
        if (tagInstance == null)
            return;

        // The tag is only visible once the source has been scanned,
        // and while the player is close enough.
        bool shouldShowTag = isVisible && isScanned;

        if (tagInstance.activeSelf != shouldShowTag)
        {
            tagInstance.SetActive(shouldShowTag);
        }

        if (shouldShowTag)
        {
            // Keep the tag facing the camera.
            tagInstance.transform.rotation =
                Quaternion.LookRotation(
                    tagInstance.transform.position -
                    camTransform.position
                );
        }
    }

    private void UpdateFarMarkerVisibility()
    {
        if (farMarkerInstance == null)
            return;

        // The far marker is shown after the point has been tagged
        // and the player is no longer close to it.
        bool shouldShowMarker =
            HasTag && !isVisible;

        if (farMarkerInstance.activeSelf != shouldShowMarker)
        {
            farMarkerInstance.SetActive(shouldShowMarker);
        }

        if (shouldShowMarker)
        {
            // Keep the marker facing the camera.
            farMarkerInstance.transform.rotation =
                Quaternion.LookRotation(
                    farMarkerInstance.transform.position -
                    camTransform.position
                );
        }
    }

    private void UpdateScanning()
    {
        if (scanUIInstance == null || isScanned)
            return;

        // Progress fills while the detector points at the source (hover active),
        // and falls off again once the player looks away.
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

            // Keep the scan UI facing the camera.
            scanUIInstance.transform.rotation =
                Quaternion.LookRotation(
                    scanUIInstance.transform.position -
                    camTransform.position
                );
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

        // Shrink the indicator once the player leaves range, so it stays
        // visible from anywhere without cluttering the scene up close.
        float targetScale = isVisible ? 1f : scannedIndicatorMinifiedScale;

        scannedIndicatorInstance.transform.localScale =
            scannedIndicatorBaseScale * targetScale;

        scannedIndicatorInstance.transform.rotation =
            Quaternion.LookRotation(
                scannedIndicatorInstance.transform.position -
                camTransform.position
            );
    }

    private void CompleteScan()
    {
        isScanned = true;
        isHoveredForScan = false;

        if (scanUIInstance != null)
        {
            scanUIInstance.SetActive(false);
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
        }

        OnScanCompleted?.Invoke(this);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (displayBackground != null)
        {
            displayBackground.color = displayHoverColor;
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

        isHoveredForScan = false;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        CycleTag();
    }

    private void CycleTag()
    {
        // Tagging is locked until the source has been fully scanned.
        if (!isScanned)
            return;

        if (tagDisplay == null ||
            availableTags == null ||
            availableTags.Length == 0)
        {
            return;
        }

        // Cycle through the available tags.
        // The first selection chooses the first available tag.
        currentTagIndex =
            (currentTagIndex + 1) % availableTags.Length;

        tagDisplay.SetData(
            availableTags[currentTagIndex]
        );

        // Notify the game manager that the selected tag has changed.
        OnTagChanged?.Invoke(this);
    }
}

using UnityEngine;
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

    [Header("Info Display (Prefab)")]
    [SerializeField] GameObject displayPrefab; // prefab asset for the info text/canvas
    [SerializeField] float heightOffset = 1.5f;
    [SerializeField] float triggerDistance = 3f;

    [Header("Tags")]
    [SerializeField] GameObject tagPrefab; // generic prefab holding a TagDisplay component
    [SerializeField] TagData emptyTagData; // placeholder shown before the player picks a tag
    [SerializeField] TagData[] availableTags;
    [SerializeField] float tagHeightOffset = 2f;

    [Header("Far Marker (shown once tagged, even from afar)")]
    [SerializeField] GameObject farMarkerPrefab; // small dot/icon prefab
    [SerializeField] float farMarkerHeightOffset = 2f;

    GameObject displayRoot;
    TextMeshProUGUI textLabel;
    Transform camTransform;
    bool isVisible = false;

    GameObject tagInstance;
    TagDisplay tagDisplay;
    int currentTagIndex = -1; // -1 means placeholder (no tag chosen yet)
    XRSimpleInteractable interactable;

    GameObject farMarkerInstance;

#if UNITY_EDITOR
    void Reset()
    {
        // Add the localization component at edit time, same approach as the original script
        if (GetComponent<LocalizedKey>() == null)
        {
            LocalizedKey localizedKey = gameObject.AddComponent<LocalizedKey>();
            Undo.RegisterCreatedObjectUndo(localizedKey, "Add LocalizedKey Component");
        }
    }

    void OnValidate()
    {
        LocalizedKey localizedKey = GetComponent<LocalizedKey>();
        if (localizedKey != null)
        {
            localizedKey.localizationKey = localizationKey;
        }
    }
#endif

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        // Safety net in case Reset() never ran on this object
        LocalizedKey localizedKey = GetComponent<LocalizedKey>();
        if (localizedKey == null)
        {
            localizedKey = gameObject.AddComponent<LocalizedKey>();
        }
        localizedKey.localizationKey = localizationKey;

        if (displayPrefab != null)
        {
            displayRoot = Instantiate(displayPrefab, transform);
            displayRoot.transform.localPosition = Vector3.up * heightOffset;
            displayRoot.SetActive(false);

            textLabel = displayRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            localizedKey.textComponent = textLabel;
        }

        if (tagPrefab != null)
        {
            // Only instantiate here, data is applied later in Start()
            // because LocalizationManager.Instance might not be ready yet during Awake
            tagInstance = Instantiate(tagPrefab, transform);
            tagInstance.transform.localPosition = Vector3.up * tagHeightOffset;
            tagDisplay = tagInstance.GetComponent<TagDisplay>();
            tagInstance.SetActive(false);
        }

        if (farMarkerPrefab != null)
        {
            farMarkerInstance = Instantiate(farMarkerPrefab, transform);
            farMarkerInstance.transform.localPosition = Vector3.up * farMarkerHeightOffset;
            farMarkerInstance.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
        }
    }

    void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
        }
    }

    void Start()
    {
        camTransform = Camera.main.transform;

        // Safe to call now: every Awake() in the scene has already run, including LocalizationManager's
        if (tagDisplay != null && emptyTagData != null)
        {
            tagDisplay.SetData(emptyTagData);
        }
    }

    void Update()
    {
        UpdateInfoDisplay();
        UpdateTagVisibility();
        UpdateFarMarkerVisibility();
    }

    void UpdateInfoDisplay()
    {
        if (displayRoot == null) return;

        float distance = Vector3.Distance(camTransform.position, transform.position);
        bool shouldBeVisible = distance <= triggerDistance;

        if (shouldBeVisible != isVisible)
        {
            isVisible = shouldBeVisible;
            displayRoot.SetActive(isVisible);
        }

        if (isVisible)
        {
            // Billboard effect, text always faces the camera
            displayRoot.transform.rotation = Quaternion.LookRotation(displayRoot.transform.position - camTransform.position);
        }
    }

    void UpdateTagVisibility()
    {
        if (tagInstance == null) return;

        // The tag (placeholder or chosen) only shows up close, same proximity rule as the info display
        if (tagInstance.activeSelf != isVisible)
        {
            tagInstance.SetActive(isVisible);
        }

        if (isVisible)
        {
            tagInstance.transform.rotation = Quaternion.LookRotation(tagInstance.transform.position - camTransform.position);
        }
    }

    void UpdateFarMarkerVisibility()
    {
        if (farMarkerInstance == null) return;

        // Shown only when: the point has already been tagged, AND the player is far away
        bool hasBeenTagged = currentTagIndex != -1;
        bool shouldShowMarker = hasBeenTagged && !isVisible;

        if (farMarkerInstance.activeSelf != shouldShowMarker)
        {
            farMarkerInstance.SetActive(shouldShowMarker);
        }

        if (shouldShowMarker)
        {
            farMarkerInstance.transform.rotation = Quaternion.LookRotation(farMarkerInstance.transform.position - camTransform.position);
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        CycleTag();
    }

    void CycleTag()
    {
        if (tagDisplay == null || availableTags == null || availableTags.Length == 0) return;

        // Loop through the available tags, starting from the first one on the first press
        currentTagIndex = (currentTagIndex + 1) % availableTags.Length;
        tagDisplay.SetData(availableTags[currentTagIndex]);
    }
}
using System;
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

    [Header("Info Display")]
    [SerializeField] GameObject displayPrefab;
    [SerializeField] float heightOffset = 1.5f;
    [SerializeField] float triggerDistance = 3f;

    [Header("Tags")]
    [SerializeField] GameObject tagPrefab;
    [SerializeField] TagData emptyTagData;
    [SerializeField] TagData[] availableTags;

    [Tooltip("Tags considérés comme corrects")]
    [SerializeField] TagData[] validTags;

    [SerializeField] float tagHeightOffset = 2f;

    [Header("Far Marker")]
    [SerializeField] GameObject farMarkerPrefab;
    [SerializeField] float farMarkerHeightOffset = 2f;

    public event Action<PointOfInterest> OnTagChanged;

    public bool HasTag => currentTagIndex != -1;

    public bool IsCorrect
    {
        get
        {
            if (!HasTag)
                return false;

            TagData current = availableTags[currentTagIndex];

            foreach (TagData tag in validTags)
            {
                if (tag == current)
                    return true;
            }

            return false;
        }
    }

    GameObject displayRoot;
    TextMeshProUGUI textLabel;
    Transform camTransform;
    bool isVisible;

    GameObject tagInstance;
    TagDisplay tagDisplay;
    int currentTagIndex = -1;
    XRSimpleInteractable interactable;

    GameObject farMarkerInstance;

#if UNITY_EDITOR
    void Reset()
    {
        if (GetComponent<LocalizedKey>() == null)
        {
            LocalizedKey localizedKey = gameObject.AddComponent<LocalizedKey>();
            Undo.RegisterCreatedObjectUndo(localizedKey, "Add LocalizedKey");
        }
    }

    void OnValidate()
    {
        LocalizedKey localizedKey = GetComponent<LocalizedKey>();

        if (localizedKey != null)
            localizedKey.localizationKey = localizationKey;
    }
#endif

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        LocalizedKey localizedKey = GetComponent<LocalizedKey>();

        if (localizedKey == null)
            localizedKey = gameObject.AddComponent<LocalizedKey>();

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
            interactable.selectEntered.AddListener(OnSelectEntered);
    }

    void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    void Start()
    {
        camTransform = Camera.main.transform;

        if (tagDisplay != null && emptyTagData != null)
            tagDisplay.SetData(emptyTagData);
    }

    void Update()
    {
        UpdateInfoDisplay();
        UpdateTagVisibility();
        UpdateFarMarkerVisibility();
    }

    void UpdateInfoDisplay()
    {
        if (displayRoot == null)
            return;

        float distance = Vector3.Distance(camTransform.position, transform.position);
        bool shouldBeVisible = distance <= triggerDistance;

        if (shouldBeVisible != isVisible)
        {
            isVisible = shouldBeVisible;
            displayRoot.SetActive(isVisible);
        }

        if (isVisible)
        {
            displayRoot.transform.rotation =
                Quaternion.LookRotation(displayRoot.transform.position - camTransform.position);
        }
    }

    void UpdateTagVisibility()
    {
        if (tagInstance == null)
            return;

        tagInstance.SetActive(isVisible);

        if (isVisible)
        {
            tagInstance.transform.rotation =
                Quaternion.LookRotation(tagInstance.transform.position - camTransform.position);
        }
    }

    void UpdateFarMarkerVisibility()
    {
        if (farMarkerInstance == null)
            return;

        bool show = HasTag && !isVisible;

        farMarkerInstance.SetActive(show);

        if (show)
        {
            farMarkerInstance.transform.rotation =
                Quaternion.LookRotation(farMarkerInstance.transform.position - camTransform.position);
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        CycleTag();
    }

    void CycleTag()
    {
        if (tagDisplay == null || availableTags.Length == 0)
            return;

        currentTagIndex = (currentTagIndex + 1) % availableTags.Length;

        tagDisplay.SetData(availableTags[currentTagIndex]);

        OnTagChanged?.Invoke(this);
    }
}
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

    [Tooltip("Tags accepted as correct answers")]
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
    private Transform camTransform;
    private bool isVisible = false;

    private GameObject tagInstance;
    private TagDisplay tagDisplay;
    private int currentTagIndex = -1;
    private XRSimpleInteractable interactable;

    private GameObject farMarkerInstance;

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
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
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

        // The tag is only visible when the player is close enough.
        if (tagInstance.activeSelf != isVisible)
        {
            tagInstance.SetActive(isVisible);
        }

        if (isVisible)
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

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        CycleTag();
    }

    private void CycleTag()
    {
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
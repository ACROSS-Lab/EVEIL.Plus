using UnityEngine;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PointOfInterest : MonoBehaviour
{
    [Header("Localization")]
    [SerializeField] string localizationKey;

    [Header("Reference (Prefab)")]
    [SerializeField] GameObject displayPrefab; // prefab asset, not an instance in the scene

    [Header("Position")]
    [SerializeField] float heightOffset = 1.5f;

    [Header("Detection")]
    [SerializeField] float triggerDistance = 3f;

    GameObject displayRoot; // runtime instance of the prefab
    TextMeshProUGUI textLabel;
    Transform camTransform;
    bool isVisible = false;

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
        // --- Debug step 1: check the LocalizedKey component itself ---
        LocalizedKey localizedKey = GetComponent<LocalizedKey>();
        Debug.Log($"[POI Debug] LocalizedKey component found: {localizedKey != null}");

        if (localizedKey != null)
        {
            localizedKey.localizationKey = localizationKey;
            Debug.Log($"[POI Debug] localizationKey assigned: '{localizedKey.localizationKey}'");
        }

        // --- Debug step 2: check the LocalizationManager singleton ---
        Debug.Log($"[POI Debug] LocalizationManager.Instance is null: {LocalizationManager.Instance == null}");

        if (LocalizationManager.Instance != null && !string.IsNullOrEmpty(localizationKey))
        {
            string testValue = LocalizationManager.Instance.GetLocalizedValue(localizationKey);
            Debug.Log($"[POI Debug] GetLocalizedValue('{localizationKey}') returned: '{testValue}'");
        }

        // --- Debug step 3: check the prefab instantiation ---
        if (displayPrefab == null)
        {
            Debug.Log("[POI Debug] displayPrefab is null, nothing to instantiate.");
            return;
        }

        displayRoot = Instantiate(displayPrefab, transform);
        displayRoot.transform.localPosition = Vector3.up * heightOffset;
        displayRoot.SetActive(false);

        textLabel = displayRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        Debug.Log($"[POI Debug] TextMeshProUGUI found in prefab: {textLabel != null}");

        if (localizedKey != null)
        {
            localizedKey.textComponent = textLabel;
        }
    }

    void Start()
    {
        camTransform = Camera.main.transform;
    }

    void Update()
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
}
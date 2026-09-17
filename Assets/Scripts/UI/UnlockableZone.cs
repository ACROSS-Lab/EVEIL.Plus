using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using DG.Tweening;

/// <summary>
/// Manages a teleport zone that can be locked or unlocked.
/// Locked: shows a locked-state prefab (icon + localized text), tints the zone meshes,
/// and plays a DoTween feedback on click instead of triggering teleportation.
/// Unlocked: shows an unlocked-state prefab (icon + localized text) and clears the tint.
/// Both info prefabs are held on a runtime billboard root that always faces the camera,
/// independently of the zone's own rotation.
/// </summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
public class UnlockableZone : MonoBehaviour
{
    [Header("Zone State")]
    [Tooltip("Initial state when the scene loads. Can be changed later via SetUnlocked().")]
    [SerializeField] private bool startUnlocked = false;

    [Header("State Prefabs (icon + localized text)")]
    [Tooltip("Prefab instantiated and shown while the zone is locked")]
    [SerializeField] private GameObject lockedInfoPrefab;

    [Tooltip("Prefab instantiated and shown while the zone is unlocked")]
    [SerializeField] private GameObject unlockedInfoPrefab;

    [Header("Localization")]
    [Tooltip("Localization key applied to the locked prefab's LocalizedKey component")]
    [SerializeField] private string lockedLocalizationKey;

    [Tooltip("Localization key applied to the unlocked prefab's LocalizedKey component")]
    [SerializeField] private string unlockedLocalizationKey;

    [Header("Info Window Placement")]
    [Tooltip("Optional reference point for the info window's base position. Defaults to this transform if left empty")]
    [SerializeField] private Transform infoAnchor;

    [Tooltip("Height (in local units) at which the info window is placed above the anchor")]
    [SerializeField] private float infoHeightOffset = 2f;

    [Header("Billboard (face the camera at runtime)")]
    [Tooltip("Should the info window rotate to face the player? If left empty, Camera.main is used automatically")]
    [SerializeField] private Transform playerCamera;

    [Tooltip("Should the info window rotate to face the player?")]
    [SerializeField] private bool faceCamera = true;

    [Tooltip("Ignore the camera's up/down tilt (only rotate on the Y axis, avoids weird tilting)")]
    [SerializeField] private bool lockYAxis = true;

    [Tooltip("Check this if the info window appears to face 'backwards' towards the player, flips the rotation by 180 degrees")]
    [SerializeField] private bool flip180 = false;

    [Header("Zone Mesh Tint")]
    [Tooltip("Meshes tinted while the zone is locked or hovered. Auto-filled from child Renderers at Awake if left empty")]
    [SerializeField] private Renderer[] zoneRenderers;

    [Tooltip("Color multiplied onto the zone meshes while locked")]
    [SerializeField] private Color lockedTintColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Tooltip("Extra color multiplied onto the zone meshes while hovered, combined with the locked tint if both apply")]
    [SerializeField] private Color hoverTintColor = new Color(1.2f, 1.2f, 1.2f, 1f);

    [Tooltip("Name of the single color property exposed by your custom shader (multiply blend), used for both the locked tint and the hover feedback")]
    [SerializeField] private string tintColorProperty = "_TintColor";

    [Header("Click Feedback (locked)")]
    [Tooltip("Strength of the shake")]
    [SerializeField] private float shakeStrength = 0.05f;

    [Tooltip("Duration of the shake")]
    [SerializeField] private float shakeDuration = 0.3f;

    [Header("XR Interaction")]
    [Tooltip("Leave empty: fetched automatically via GetComponent")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;

    [Header("Events")]
    [Tooltip("Triggered on click when the zone is unlocked (e.g. teleport to the matching scene)")]
    public UnityEvent onZoneSelected;

    [Tooltip("Optional: triggered on click when the zone is still locked")]
    public UnityEvent onLockedZoneClicked;

    private bool isUnlocked;
    private bool isHovered;
    private Tween feedbackTween;
    private Transform billboardRoot;
    private GameObject lockedInfoInstance;
    private GameObject unlockedInfoInstance;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        if (interactable == null)
        {
            interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        }

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        propertyBlock = new MaterialPropertyBlock();

        // Gather zone renderers BEFORE creating the info window,
        // so the icon/text meshes never get included and tinted by mistake.
        if (zoneRenderers == null || zoneRenderers.Length == 0)
        {
            zoneRenderers = GetComponentsInChildren<Renderer>(true);
        }

        CreateBillboardRoot();
        InstantiateInfoPrefabs();
        ApplyLocalizationKeys();
    }

    private void CreateBillboardRoot()
    {
        // A dedicated runtime root keeps the info window's rotation independent
        // from the zone's own rotation, only this root is rotated to face the camera.
        Vector3 basePosition = infoAnchor != null ? infoAnchor.position : transform.position;

        GameObject billboardRootObject = new GameObject("InfoBillboardRoot");
        billboardRootObject.transform.SetParent(transform, worldPositionStays: false);
        billboardRootObject.transform.position = basePosition + Vector3.up * infoHeightOffset;

        billboardRoot = billboardRootObject.transform;
    }

    private void InstantiateInfoPrefabs()
    {
        if (lockedInfoPrefab != null)
        {
            lockedInfoInstance = Instantiate(lockedInfoPrefab, billboardRoot);
        }

        if (unlockedInfoPrefab != null)
        {
            unlockedInfoInstance = Instantiate(unlockedInfoPrefab, billboardRoot);
        }
    }

    private void ApplyLocalizationKeys()
    {
        SetLocalizationKey(lockedInfoInstance, lockedLocalizationKey);
        SetLocalizationKey(unlockedInfoInstance, unlockedLocalizationKey);
    }

    private void SetLocalizationKey(GameObject instance, string key)
    {
        if (instance == null || string.IsNullOrEmpty(key)) return;

        LocalizedKey localizedKey = instance.GetComponentInChildren<LocalizedKey>(true);
        if (localizedKey == null)
        {
            Debug.LogWarning($"No LocalizedKey component found on '{instance.name}'.");
            return;
        }

        localizedKey.localizationKey = key;
        localizedKey.UpdateText();
    }

    private void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnSelectEntered);

        isUnlocked = startUnlocked;
        RefreshVisualState();
    }

    private void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        interactable.selectEntered.RemoveListener(OnSelectEntered);

        feedbackTween?.Kill();
    }

    private void LateUpdate()
    {
        // LateUpdate: this guarantees the VR camera has already been updated this frame,
        // which avoids small rotation jitter.
        if (faceCamera && playerCamera != null && billboardRoot != null)
        {
            FaceCamera();
        }
    }

    /// <summary>
    /// Called from an external progression manager (save data, completed quest, etc.)
    /// to lock or unlock the zone at runtime.
    /// </summary>
    public void SetUnlocked(bool unlocked)
    {
        isUnlocked = unlocked;
        RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        if (lockedInfoInstance != null)
        {
            lockedInfoInstance.SetActive(!isUnlocked);
        }

        if (unlockedInfoInstance != null)
        {
            unlockedInfoInstance.SetActive(isUnlocked);
        }

        ApplyMeshTint();
    }

    private void ApplyMeshTint()
    {
        if (zoneRenderers == null) return;

        // White leaves the base texture untouched in a multiply blend,
        // so it is used as the "no effect" value for both the lock state and the hover state.
        Color lockPart = !isUnlocked ? lockedTintColor : Color.white;
        Color hoverPart = isHovered ? hoverTintColor : Color.white;
        Color targetColor = lockPart * hoverPart;

        foreach (Renderer zoneRenderer in zoneRenderers)
        {
            if (zoneRenderer == null) continue;

            zoneRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(tintColorProperty, targetColor);
            zoneRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void FaceCamera()
    {
        // A World Space Canvas shows its content on its -Z side, so the root's forward (+Z)
        // must point AWAY from the camera for the readable side to face the player.
        Vector3 direction = billboardRoot.position - playerCamera.position;

        if (lockYAxis)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f) return;

        billboardRoot.rotation = Quaternion.LookRotation(direction);

        if (flip180)
        {
            billboardRoot.Rotate(0f, 180f, 0f);
        }
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        isHovered = true;
        ApplyMeshTint();
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        isHovered = false;
        ApplyMeshTint();
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (isUnlocked)
        {
            onZoneSelected.Invoke();
        }
        else
        {
            PlayLockedFeedback();
            onLockedZoneClicked.Invoke();
        }
    }

    private void PlayLockedFeedback()
    {
        if (lockedInfoInstance == null) return;

        Transform feedbackTarget = lockedInfoInstance.transform;

        feedbackTween?.Kill();
        feedbackTween = feedbackTarget
            .DOShakePosition(shakeDuration, shakeStrength)
            .SetUpdate(UpdateType.Normal);
    }
}

using UnityEngine;

/// <summary>
/// Activates/deactivates an object based on its distance to the player's camera,
/// and rotates it to always face that camera, without ever changing its
/// position in the scene.
/// </summary>
public class ProximityBillboard : MonoBehaviour
{
    [Header("Distance Detection")]
    [Tooltip("Distance below which the object activates")]
    [SerializeField] private float activationDistance = 5f;

    [Tooltip("Extra margin added before deactivation, to avoid flickering if the player stays right at the edge")]
    [SerializeField] private float deactivationMargin = 0.5f;

    [Tooltip("Interval (in seconds) between two distance checks. 0 = every frame")]
    [SerializeField] private float checkInterval = 0.2f;

    [Header("Camera Reference")]
    [Tooltip("Player camera (e.g. the camera transform under your XR Origin). If left empty, Camera.main is used automatically")]
    [SerializeField] private Transform playerCamera;

    [Header("Billboard Effect (face the camera)")]
    [Tooltip("Should the object rotate to face the player?")]
    [SerializeField] private bool faceCamera = true;

    [Tooltip("Ignore the camera's up/down tilt (only rotate on the Y axis, avoids weird tilting)")]
    [SerializeField] private bool lockYAxis = true;

    [Tooltip("Check this if the object appears to face 'backwards' towards the player — flips the rotation by 180°")]
    [SerializeField] private bool flip180 = false;

    private float timer;
    private bool isActive;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // For UI (World Space Canvas + Image/TextMeshProUGUI), we go through a
        // CanvasGroup: these elements use CanvasRenderer/Graphic, not Renderer/Collider,
        // so GetComponentsInChildren<Renderer>() would never find anything on this kind of object.
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }
    }

    private void Start()
    {
        isActive = false;
        SetVisualState(false);
    }

    private void Update()
    {
        if (playerCamera == null) return;

        timer += Time.deltaTime;
        if (timer >= checkInterval)
        {
            timer = 0f;
            CheckDistance();
        }
    }

    private void LateUpdate()
    {
        // LateUpdate: this guarantees the VR camera has already been updated this frame,
        // which avoids small rotation jitter.
        if (isActive && faceCamera && playerCamera != null)
        {
            FaceCamera();
        }
    }

    private void CheckDistance()
    {
        float distance = Vector3.Distance(transform.position, playerCamera.position);

        if (!isActive && distance <= activationDistance)
        {
            isActive = true;
            SetVisualState(true);
        }
        else if (isActive && distance > activationDistance + deactivationMargin)
        {
            isActive = false;
            SetVisualState(false);
        }
    }

    private void SetVisualState(bool state)
    {
        // We never call SetActive(false) on this object:
        // this script (and its Update) would stop running too, and the panel
        // could never reactivate itself again.
        // CanvasGroup lets us cleanly hide/show the UI instead:
        canvasGroup.alpha = state ? 1f : 0f;
        canvasGroup.interactable = state;
        canvasGroup.blocksRaycasts = state;
    }

    private void FaceCamera()
    {
        Vector3 direction = playerCamera.position - transform.position;

        if (lockYAxis)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(direction);

        if (flip180)
        {
            transform.Rotate(0f, 180f, 0f);
        }
    }

    // Small gizmo to visualize the activation distance in the Unity editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, activationDistance);
    }
}

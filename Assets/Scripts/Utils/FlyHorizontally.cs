using UnityEngine;
using UnityEngine.InputSystem;

public class FlyHorizontally : MonoBehaviour
{   
    [Header("Camera References")]
    [SerializeField] Transform mainCameraTransform;

    [Header("Movement Settings")]
    [SerializeField] float speed = 10f;
    [SerializeField] float minX = 0;
    [SerializeField] float maxX = 0;
    [SerializeField] float minZ = 0;
    [SerializeField] float maxZ = 0;
    [SerializeField] bool revertedX = false;
    [SerializeField] bool revertedZ = false;

    [Header("Input References")]
    [SerializeField] private InputActionReference inputLeft;
    [SerializeField] private InputActionReference inputRight;

    [Header("New Boundaries")]
    public bool showNewBoundaries = true;

    [SerializeField] float newMinX = 0;
    [SerializeField] float newMaxX = 0;
    [SerializeField] float newMinZ = 0;
    [SerializeField] float newMaxZ = 0;

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(minX, transform.position.y, minZ), new Vector3(maxX, transform.position.y, minZ));
        Gizmos.DrawLine(new Vector3(maxX, transform.position.y, minZ), new Vector3(maxX, transform.position.y, maxZ));
        Gizmos.DrawLine(new Vector3(maxX, transform.position.y, maxZ), new Vector3(minX, transform.position.y, maxZ));
        Gizmos.DrawLine(new Vector3(minX, transform.position.y, maxZ), new Vector3(minX, transform.position.y, minZ));

        if (showNewBoundaries)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(new Vector3(newMinX, transform.position.y, newMinZ), new Vector3(newMaxX, transform.position.y, newMinZ));
            Gizmos.DrawLine(new Vector3(newMaxX, transform.position.y, newMinZ), new Vector3(newMaxX, transform.position.y, newMaxZ));
            Gizmos.DrawLine(new Vector3(newMaxX, transform.position.y, newMaxZ), new Vector3(newMinX, transform.position.y, newMaxZ));
            Gizmos.DrawLine(new Vector3(newMinX, transform.position.y, newMaxZ), new Vector3(newMinX, transform.position.y, newMinZ));
        }
    }

    void Start()
    {
        
    }

    void Update()
    {
        Vector2 inputVectorLeft = inputLeft.action.ReadValue<Vector2>();
        Vector2 inputVectorRight = inputRight.action.ReadValue<Vector2>();

        if (inputVectorLeft == Vector2.zero && inputVectorRight == Vector2.zero)
        {
            return;
        }

        Vector2 input = Vector2.ClampMagnitude(inputVectorLeft + inputVectorRight, 1f);
        float inputX = revertedX ? -input.x : input.x;
        float inputZ = revertedZ ? -input.y : input.y;

        Vector3 forward = mainCameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = mainCameraTransform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 movement = (forward * inputZ + right * inputX) * speed * Time.deltaTime;
        Vector3 newPosition = transform.position + movement;

        if (minX != maxX)
        {
            newPosition.x = Mathf.Clamp(newPosition.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));
        }
        if (minZ != maxZ)
        {
            newPosition.z = Mathf.Clamp(newPosition.z, Mathf.Min(minZ, maxZ), Mathf.Max(minZ, maxZ));
        }

        transform.position = newPosition;    
    }

    public void ApplyNewBoundaries()
    {
        minX = newMinX;
        maxX = newMaxX;
        minZ = newMinZ;
        maxZ = newMaxZ;
    }
}

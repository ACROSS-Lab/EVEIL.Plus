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

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(minX, transform.position.y, minZ), new Vector3(maxX, transform.position.y, minZ));
        Gizmos.DrawLine(new Vector3(maxX, transform.position.y, minZ), new Vector3(maxX, transform.position.y, maxZ));
        Gizmos.DrawLine(new Vector3(maxX, transform.position.y, maxZ), new Vector3(minX, transform.position.y, maxZ));
        Gizmos.DrawLine(new Vector3(minX, transform.position.y, maxZ), new Vector3(minX, transform.position.y, minZ));
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

        if (minX < maxX)
        {
            newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
        }
        if (minZ < maxZ)
        {
            newPosition.z = Mathf.Clamp(newPosition.z, minZ, maxZ);
        }

        transform.position = newPosition;    
    }
}

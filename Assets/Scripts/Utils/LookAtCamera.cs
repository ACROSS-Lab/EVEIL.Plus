using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    [SerializeField]
    private Camera targetCamera;

    [SerializeField]
    private bool lockYAxis = true;

    void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    void LateUpdate()
    {
        Vector3 direction = targetCamera.transform.position - transform.position;

        if (lockYAxis)
        {
            direction.y = 0;
        }

        transform.rotation = Quaternion.LookRotation(direction);
    }
}
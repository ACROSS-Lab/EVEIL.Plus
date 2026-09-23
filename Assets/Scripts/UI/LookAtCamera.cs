using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] bool lockYAxis = true;
    [SerializeField] bool reverseZ = false;

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
        direction = reverseZ ? -direction : direction;

        if (lockYAxis)
        {
            direction.y = 0;
        }

        transform.rotation = Quaternion.LookRotation(direction);
    }
}
using UnityEngine;

public class Rotator : MonoBehaviour
{
    [SerializeField] private float speed = 90f;
    [SerializeField] private Vector3 axis = Vector3.up;
    [SerializeField] private Space space = Space.Self;

    private Vector3 normalizedAxis;

    private void Awake()
    {
        normalizedAxis = axis.normalized;
    }

    private void OnValidate()
    {
        normalizedAxis = axis.normalized;
    }

    private void Update()
    {
        transform.Rotate(normalizedAxis, speed * Time.deltaTime, space);
    }
}

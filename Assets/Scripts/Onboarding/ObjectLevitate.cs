using UnityEngine;

public class ObjectLevitate : MonoBehaviour
{
    public float amplitude;
    public float levitateSpeed;
    public float rotationSpeed;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * levitateSpeed) * amplitude;
        transform.position = startPosition + Vector3.up * y;
        
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
    }
}

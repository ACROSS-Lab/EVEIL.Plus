using UnityEngine;

public class HeadLookAt : MonoBehaviour
{
    [Header("Références")]
    public Transform headBone;
    public Transform target;

    [Header("Activation")]
    public bool enableLookAt = true;

    [Header("Contraintes")]
    public float maxAngle = 60f;
    public float speed = 5f;

    [Header("Offset")]
    public Vector3 rotationOffset = Vector3.zero;

    private Quaternion _initialLocalRotation;
    private Quaternion _currentLookRotation;
    private float _blendWeight = 0f; // 0 = animation pure, 1 = lookat pur

    void Start()
    {
        if (headBone != null)
            _initialLocalRotation = headBone.localRotation;

        _currentLookRotation = headBone.rotation;
    }

    void LateUpdate()
    {
        if (headBone == null) return;

        // Rotation appliquée par l'animator CE frame (on la capture après)
        Quaternion animatorRotation = headBone.rotation;

        // Calcul du target look
        if (target != null)
        {
            Vector3 directionToTarget = target.position - headBone.position;
            float angle = Vector3.Angle(transform.forward, directionToTarget);

            if (angle < maxAngle)
            {
                Quaternion lookRotation = Quaternion.LookRotation(directionToTarget);
                lookRotation *= Quaternion.Euler(rotationOffset);
                _currentLookRotation = Quaternion.Slerp(
                    _currentLookRotation,
                    lookRotation,
                    Time.deltaTime * speed
                );
            }
            else
            {
                _currentLookRotation = Quaternion.Slerp(
                    _currentLookRotation,
                    animatorRotation,
                    Time.deltaTime * speed
                );
            }
        }

        // Blend weight vers 0 ou 1 progressivement
        float targetWeight = enableLookAt ? 1f : 0f;
        _blendWeight = Mathf.Lerp(_blendWeight, targetWeight, Time.deltaTime * speed);

        // Mélange entre la rotation de l'animator et le lookat
        headBone.rotation = Quaternion.Slerp(
            animatorRotation,        // ← rotation de l'animation ce frame
            _currentLookRotation,    // ← rotation calculée par le script
            _blendWeight
        );
    }

    public void SetLookAtState( bool lookAt )
    {
        enableLookAt = lookAt;
    }
}
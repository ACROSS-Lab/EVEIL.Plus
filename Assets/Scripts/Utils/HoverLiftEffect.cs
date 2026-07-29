using DG.Tweening;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach this to the same GameObject as an XRBaseInteractable (e.g. XRSimpleInteractable or XRGrabInteractable).
/// Smoothly lifts the object up when hovered by an OpenXR controller/hand, and lowers it back down on hover exit.
/// Works with the XR Interaction Toolkit, which is driven by the OpenXR plugin.
/// Requires DOTween (Assets > Import Package, or via Package Manager / Asset Store).
/// </summary>
[RequireComponent(typeof(XRBaseInteractable))]
public class HoverLiftEffect : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Child transform holding the visual mesh, animated independently from the collider. " +
             "Keep the root object (with the collider) completely static so the ray never loses hover " +
             "while the mesh moves up.")]
    [SerializeField] private Transform visualTransform;

    [Header("Movement amount")]
    [Tooltip("Lift height in meters, in local space")]
    [SerializeField] private float liftHeight = 0.1f;

    [Header("Tweening")]
    [Tooltip("Duration of the lift animation, in seconds")]
    [SerializeField] private float liftDuration = 0.25f;

    [Tooltip("Duration of the drop animation, in seconds")]
    [SerializeField] private float dropDuration = 0.2f;

    [Tooltip("Ease used for the lift tween")]
    [SerializeField] private Ease liftEase = Ease.OutCubic;

    [Tooltip("Ease used for the drop tween")]
    [SerializeField] private Ease dropEase = Ease.InCubic;

    [Header("Optional")]
    [Tooltip("If enabled, the object slowly spins while lifted")]
    [SerializeField] private bool addFloatingRotation = false;
    [SerializeField] private float rotationDuration = 4f;

    private XRBaseInteractable _interactable;
    private Vector3 _restPosition;
    private Vector3 _liftedPosition;
    private Quaternion _restRotation;
    private Tween _moveTween;
    private Tween _rotateTween;
    private int _hoverCount = 0;

    private void Awake()
    {
        _interactable = GetComponent<XRBaseInteractable>();

        if (visualTransform == null)
        {
            Debug.LogWarning($"[{nameof(HoverLiftEffect)}] No visualTransform assigned on '{name}', " +
                              "falling back to the root transform. This may cause the hover ray to lose " +
                              "the object while it lifts. Assign a separate child transform instead.");
            visualTransform = transform;
        }

        _restPosition = visualTransform.localPosition;
        _liftedPosition = _restPosition + Vector3.up * liftHeight;
        _restRotation = visualTransform.localRotation;
    }

    private void OnEnable()
    {
        _interactable.hoverEntered.AddListener(OnHoverEntered);
        _interactable.hoverExited.AddListener(OnHoverExited);
    }

    private void OnDisable()
    {
        _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        _interactable.hoverExited.RemoveListener(OnHoverExited);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        _hoverCount++;
        if (_hoverCount == 1)
        {
            _moveTween?.Kill();
            _moveTween = visualTransform
                .DOLocalMove(_liftedPosition, liftDuration)
                .SetEase(liftEase);

            if (addFloatingRotation)
            {
                _rotateTween?.Kill();
                _rotateTween = visualTransform
                    .DOLocalRotate(new Vector3(0f, 360f, 0f), rotationDuration, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Restart);
            }
        }
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        _hoverCount = Mathf.Max(0, _hoverCount - 1);
        if (_hoverCount == 0)
        {
            _rotateTween?.Kill();

            _moveTween?.Kill();
            _moveTween = visualTransform
                .DOLocalMove(_restPosition, dropDuration)
                .SetEase(dropEase);

            if (addFloatingRotation)
            {
                // Reset rotation smoothly back to the original one, so the next
                // lift always starts from a known angle instead of drifting.
                visualTransform
                    .DOLocalRotateQuaternion(_restRotation, dropDuration)
                    .SetEase(dropEase);
            }
        }
    }

    private void OnDestroy()
    {
        _moveTween?.Kill();
        _rotateTween?.Kill();
    }
}

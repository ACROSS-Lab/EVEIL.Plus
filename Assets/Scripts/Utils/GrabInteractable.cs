using DG.Tweening;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class GrabInteractable : MonoBehaviour
{
    [SerializeField] float resetDuration = 1f;

    XRGrabInteractable interactable;
    MaterialPropertyBlock propertyBlock;
    Vector3 originalPosition;
    Quaternion originalRotation;
    Vector3 originalScale;

    int hoverCount = 0;
    int selectCount = 0;

    static readonly int propertyID = Shader.PropertyToID("_Highlight");

    void Awake()
    {
        interactable = GetComponent<XRGrabInteractable>();
        propertyBlock = new MaterialPropertyBlock();
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
    }

    void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.selectExited.RemoveListener(OnSelectExited);
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        hoverCount++;
        if (hoverCount == 1) SetHightlight(true);
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        hoverCount = Mathf.Max(hoverCount - 1, 0);
        if (hoverCount == 0) SetHightlight(false);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        selectCount++;
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        selectCount = Mathf.Max(selectCount - 1, 0);
        if (selectCount == 0) ResetTransform();
    }

    void SetHightlight(bool highlight)
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(propertyID, highlight ? 1f : 0f);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    void ResetTransform()
    {
        DOTween.Kill(transform);

        Sequence sequence = DOTween.Sequence();

        sequence.Join(transform.DOMove(originalPosition, resetDuration));
        sequence.Join(transform.DORotateQuaternion(originalRotation, resetDuration));
        sequence.Join(transform.DOScale(originalScale, resetDuration));

        sequence.SetEase(Ease.InOutSine);
    }
}

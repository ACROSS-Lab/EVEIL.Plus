using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class GrabInteractable : MonoBehaviour
{
    [SerializeField] float resetDuration = 1f;
    
    [Header("Hint Pulse")]
    [SerializeField] private int hintBlinkCount = 3;
    [SerializeField] private float hintBlinkInterval = 0.3f;
    
    XRGrabInteractable interactable;
    MaterialPropertyBlock propertyBlock;
    Vector3 originalPosition;
    Quaternion originalRotation;
    Vector3 originalScale;
    Renderer[] meshRenderers;

    int hoverCount = 0;
    int selectCount = 0;
    private Coroutine _pulseRoutine;

    static readonly int propertyID = Shader.PropertyToID("_Highlight");

    #if UNITY_EDITOR
    void Reset()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null && rb.isKinematic == false)
        {
            rb.isKinematic = true;
        }

        interactable = GetComponent<XRGrabInteractable>();
        interactable.useDynamicAttach = true;
        interactable.matchAttachPosition = true;
        interactable.matchAttachRotation = true;
        interactable.snapToColliderVolume = true;
        interactable.reinitializeDynamicAttachEverySingleGrab = true;
    }
    #endif

    void Awake()
    {
        interactable = GetComponent<XRGrabInteractable>();
        propertyBlock = new MaterialPropertyBlock();
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
        meshRenderers = GetComponentsInChildren<Renderer>();
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
        if (hoverCount == 1) SetHighlight(true);
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        hoverCount = Mathf.Max(hoverCount - 1, 0);
        if (hoverCount == 0) SetHighlight(false);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        selectCount++;
        DOTween.Kill(transform);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        selectCount = Mathf.Max(selectCount - 1, 0);
        if (selectCount == 0) ResetTransform();
    }

    void SetHighlight(bool highlight)
    {
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            meshRenderers[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(propertyID, highlight ? 1f : 0f);
            meshRenderers[i].SetPropertyBlock(propertyBlock);
        }
    }
    
    public void PulseHighlight()
    {
        if (_pulseRoutine != null)
            StopCoroutine(_pulseRoutine);
 
        _pulseRoutine = StartCoroutine(PulseRoutine());
    }
 
    private IEnumerator PulseRoutine()
    {
        for (int i = 0; i < hintBlinkCount; i++)
        {
            SetHighlight(true);
            yield return new WaitForSeconds(hintBlinkInterval);
            
            if (hoverCount == 0)
                SetHighlight(false);
 
            yield return new WaitForSeconds(hintBlinkInterval);
        }
 
        if (hoverCount > 0)
            SetHighlight(true);
 
        _pulseRoutine = null;
    }

    void ResetTransform()
    {
        interactable.enabled = false;

        Sequence sequence = DOTween.Sequence();

        sequence.Join(transform.DOMove(originalPosition, resetDuration));
        sequence.Join(transform.DORotateQuaternion(originalRotation, resetDuration));
        sequence.Join(transform.DOScale(originalScale, resetDuration));

        sequence.SetEase(Ease.InOutSine);
        sequence.OnComplete(() => 
        {
            if (interactable != null)
                interactable.enabled = true;
        });
    }
}

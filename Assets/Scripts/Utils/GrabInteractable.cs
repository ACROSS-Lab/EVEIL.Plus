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
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(propertyID, highlight ? 1f : 0f);
            renderer.SetPropertyBlock(propertyBlock);
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
            SetHightlight(true);
            yield return new WaitForSeconds(hintBlinkInterval);
            
            if (hoverCount == 0)
                SetHightlight(false);
 
            yield return new WaitForSeconds(hintBlinkInterval);
        }
 
        if (hoverCount > 0)
            SetHightlight(true);
 
        _pulseRoutine = null;
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

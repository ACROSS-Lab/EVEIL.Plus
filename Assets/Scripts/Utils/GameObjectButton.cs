using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(XRSimpleInteractable))]
public class GameObjectButton : MonoBehaviour
{
    [SerializeField] bool hoverHighlight = false;
    [SerializeField] UnityEvent[] onButtonPress;

    Renderer[] meshRenderers;
    XRBaseInteractable interactable;
    MaterialPropertyBlock propertyBlock;

    static readonly int propertyID = Shader.PropertyToID("_Highlight");

    #if UNITY_EDITOR
    void Reset()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.center = transform.InverseTransformPoint(bounds.center);
        boxCollider.size = transform.InverseTransformVector(bounds.size);
    }
    #endif

    void Awake()
    {
        meshRenderers = GetComponentsInChildren<Renderer>();
        interactable = GetComponent<XRSimpleInteractable>();
        propertyBlock = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnSelectEntered);
    }

    void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        for (int i = 0; i < onButtonPress.Length; i++)
        {
            onButtonPress[i].Invoke();
        }
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (hoverHighlight)
        {
            SetHighlight(true);
        }
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (hoverHighlight)
        {
            SetHighlight(false);
        }
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
}

using System.Collections;
using UnityEngine;

public class HighlightPulse : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private int blinkCount = 3;
    [SerializeField] private float blinkInterval = 0.3f;

    private Material[] originalMaterials;
    private Coroutine pulseRoutine;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null)
            originalMaterials = targetRenderer.materials;
    }
    
    public void Pulse()
    {
        if (targetRenderer == null || highlightMaterial == null) return;

        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);

        pulseRoutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        for (int i = 0; i < blinkCount; i++)
        {
            SetHighlighted(true);
            yield return new WaitForSeconds(blinkInterval);
            SetHighlighted(false);
            yield return new WaitForSeconds(blinkInterval);
        }

        pulseRoutine = null;
    }

    // Sets highlighted only the first material of the tool (as for now there are 2 materials, one for the body and one for the screen)
    private void SetHighlighted(bool highlighted)
    {
        if (highlighted)
        {
            // (deactivated bc that's not what we want) Sets highlighted all materials
            // var swapped = new Material[targetRenderer.sharedMaterials.Length];
            // for (int i = 0; i < swapped.Length; i++)
            //     swapped[i] = highlightMaterial;
            //
            // targetRenderer.sharedMaterials = swapped;
            var materials = targetRenderer.materials;
            materials[0] = highlightMaterial;
            targetRenderer.materials = materials;
        }
        else
        {
            targetRenderer.materials = originalMaterials;
        }
    }

    private void OnDestroy()
    {
        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);
    }
}
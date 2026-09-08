using UnityEngine;

public class MagicCam : MonoBehaviour
{
    public Camera camcorderCamera; // référence vers Lens_Camera, enfant de cet objet
    public RenderTexture renderTexture;
    public int frameSkip = 3;
    private int frameCounter = 0;

    void Start()
    {
        camcorderCamera.targetTexture = renderTexture;
        camcorderCamera.enabled = false; // on gère le rendu manuellement
    }

    void Update()
    {
        frameCounter++;
        if (frameCounter >= frameSkip)
        {
            frameCounter = 0;
            camcorderCamera.Render();
        }
    }
}
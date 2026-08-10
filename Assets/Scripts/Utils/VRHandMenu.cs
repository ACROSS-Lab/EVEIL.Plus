using UnityEngine;
using UnityEngine.InputSystem;

public class VRHandMenu : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("The Canvas or Panel GameObject to toggle on and off.")]
    [SerializeField] private GameObject menuCanvas;

    [Header("Inputs")]
    [Tooltip("Reference to the Input Action assigned to the controller button.")]
    [SerializeField] private InputActionReference menuButton;

    private void Update()
    {
        ToggleMenuInput();
    }

    private void ToggleMenuInput()
    {
        // Check if the button mapped to the InputActionReference was pressed this frame
        if (menuButton != null && menuButton.action.WasPerformedThisFrame())
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        if (menuCanvas != null)
        {
            // Toggle the active state of the canvas
            menuCanvas.SetActive(!menuCanvas.activeInHierarchy);
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ManagedDoom;
using ManagedDoom.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class DetectorDoomEasterEgg : MonoBehaviour
{
    public enum ScreenRotation
    {
        None = 0,
        Rotate90CW = 1,
        Rotate180 = 2,
        Rotate270CW = 3
    }

    public enum TransitionEffect
    {
        None = 0,
        CrtPowerOn = 1,
        DigitalGlitch = 2,
        RetroStatic = 3
    }

    [Header("Detector & Player References")]
    [SerializeField] private DetectorController detectorController;
    [SerializeField] private Canvas scanningCanvas;
    [SerializeField] private MagicCam magicCam;
    [SerializeField] private VRHandMenu vrHandMenu;
    [SerializeField] private FlyHorizontally flyHorizontally;

    [Header("Display Output")]
    [Tooltip("The RenderTexture bound to the detector's screen (e.g., RenderTexture_01)")]
    [SerializeField] private RenderTexture targetRenderTexture;
    [SerializeField] private Material customBlitMaterial; // Optional: custom shader material

    [Header("Screen Orientation (Fix 90-degree Rotation)")]
    [Tooltip("Rotates the Doom frame to match the detector's 3D mesh UVs without affecting normal rendering")]
    [SerializeField] private ScreenRotation screenRotation = ScreenRotation.Rotate90CW;
    [SerializeField] private bool flipHorizontal = false;
    [SerializeField] private bool flipVertical = false;

    [Header("Screen Zoom & Framing")]
    [Tooltip("Adjust zoom: values < 1.0 zoom OUT (shrink image to fit screen), > 1.0 zoom IN")]
    [Range(0.2f, 2.0f)]
    [SerializeField] private float screenZoom = 1.0f;
    [Tooltip("Adjust horizontal stretch/squish if the 3D screen UV is not 1:1")]
    [Range(0.5f, 2.0f)]
    [SerializeField] private float aspectCorrection = 1.0f;
    [Tooltip("Nudge Doom screen left/right (X) or up/down (Y)")]
    [SerializeField] private Vector2 screenOffset = Vector2.zero;

    [Header("Screen Transition Effect")]
    [SerializeField] private TransitionEffect transitionEffect = TransitionEffect.CrtPowerOn;
    [Tooltip("Duration of the boot-up and shut-down transition in seconds")]
    [Range(0.05f, 1.5f)]
    [SerializeField] private float transitionDuration = 0.35f;
    [Tooltip("Optional sound effect to play during screen transition")]
    [SerializeField] private AudioClip transitionSFX;

    [Header("VR Input Actions (Right Hand)")]
    [SerializeField] private InputActionReference triggerAction;
    [SerializeField] private InputActionReference gripAction;
    [SerializeField] private InputActionReference thumbstickAction;
    [SerializeField] private InputActionReference thumbstickClickAction;
    [Tooltip("Right Controller 'A' Button")]
    [SerializeField] private InputActionReference primaryButtonAction;
    [Tooltip("Right Controller 'B' Button")]
    [SerializeField] private InputActionReference secondaryButtonAction;

    [Header("Easter Egg Timing")]
    [Tooltip("Seconds to hold (Grip + Trigger) or (A + B) to boot Doom")]
    [SerializeField] private float activationHoldTime = 3.0f;
    [Tooltip("Seconds to hold Thumbstick Click to exit Doom")]
    [SerializeField] private float exitHoldTime = 2.0f;

    [Header("Target Frame Rate")]
    [Range(20, 60)]
    [SerializeField] private int doomTargetFps = 35; // Classic Doom tick rate is 35 FPS

    // Internal State
    private bool isPlayingDoom = false;
    private float activationTimer = 0f;
    private float exitTimer = 0f;
    private bool stickClickWasHeld = false;

    // Menu Navigation Repeat Timers
    private float menuVRepeatTimer = 0f;
    private bool menuVWasCentered = true;
    private float menuHRepeatTimer = 0f;
    private bool menuHWasCentered = true;

    // Transition State
    private float currentTransitionProgress = 1.0f;
    private Coroutine transitionCoroutine;
    private bool isTransitioningOut = false;

    // Doom Engine
    private UnityDoom doom;
    private Texture2D doomVideoTexture;
    private readonly List<KeyCode> keysPressed = new List<KeyCode>(16);
    private Coroutine doomLoopCoroutine;
    private Material runtimeBlitMaterial;

    // Weapon cycling
    private int currentWeaponSlot = 2; // Start with Pistol (slot 2)
    private readonly KeyCode[] weaponKeyCodes = new KeyCode[]
    {
        KeyCode.Alpha1, // Fist / Chainsaw
        KeyCode.Alpha2, // Pistol
        KeyCode.Alpha3, // Shotgun / Super Shotgun
        KeyCode.Alpha4, // Chaingun
        KeyCode.Alpha5, // Rocket Launcher
        KeyCode.Alpha6, // Plasma Rifle
        KeyCode.Alpha7  // BFG9000
    };

    void Awake()
    {
        if (detectorController == null)
            detectorController = GetComponent<DetectorController>();

        if (magicCam == null)
            magicCam = GetComponentInChildren<MagicCam>(true);

        if (vrHandMenu == null)
            vrHandMenu = FindAnyObjectByType<VRHandMenu>();

        if (flyHorizontally == null)
            flyHorizontally = FindAnyObjectByType<FlyHorizontally>();

        InitializeBlitMaterial();
    }

    private void InitializeBlitMaterial()
    {
        if (customBlitMaterial != null)
        {
            runtimeBlitMaterial = new Material(customBlitMaterial);
        }
        else
        {
            Shader blitShader = Shader.Find("Custom/DoomScreenBlit");
            if (blitShader != null)
            {
                runtimeBlitMaterial = new Material(blitShader);
            }
        }
    }

    void OnDestroy()
    {
        if (runtimeBlitMaterial != null)
        {
            Destroy(runtimeBlitMaterial);
            runtimeBlitMaterial = null;
        }
    }

    void OnDisable()
    {
        if (isPlayingDoom)
        {
            ShutdownDoomImmediate();
        }
    }

    void Update()
    {
        if (!isPlayingDoom)
        {
            CheckActivationCombo();

#if UNITY_EDITOR
            if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
            {
                StartDoom();
            }
#endif
        }
        else
        {
            CheckExitCombo();
            UpdateDoomInputs();

#if UNITY_EDITOR
            if (Keyboard.current != null && (Keyboard.current.f10Key.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame))
            {
                StopDoom();
            }
#endif
        }
    }

    #region Activation / Deactivation

    private void CheckActivationCombo()
    {
        bool gripHeld = IsActionPressed(gripAction, 0.6f);
        bool triggerHeld = IsActionPressed(triggerAction, 0.6f);
        bool aHeld = IsActionPressed(primaryButtonAction, 0.5f);
        bool bHeld = IsActionPressed(secondaryButtonAction, 0.5f);

        // Can activate by holding either (Grip + Trigger) OR (A + B) for 3 seconds
        bool isActivating = (gripHeld && triggerHeld) || (aHeld && bHeld);

        if (isActivating)
        {
            activationTimer += Time.deltaTime;
            if (activationTimer >= activationHoldTime)
            {
                activationTimer = 0f;
                StartDoom();
            }
        }
        else
        {
            activationTimer = 0f;
        }
    }

    private void CheckExitCombo()
    {
        bool stickClicked = IsActionPressed(thumbstickClickAction, 0.5f);

        if (stickClicked)
        {
            exitTimer += Time.deltaTime;
            if (exitTimer >= exitHoldTime)
            {
                exitTimer = 0f;
                stickClickWasHeld = true;
                StopDoom();
            }
        }
        else
        {
            exitTimer = 0f;
            stickClickWasHeld = false;
        }
    }

    public void StartDoom()
    {
        if (isPlayingDoom) return;

        string wadPath = Path.Combine(Application.streamingAssetsPath, "DOOM1.WAD");
        if (!File.Exists(wadPath))
        {
            Debug.LogError($"[DoomEasterEgg] DOOM1.WAD not found at: {wadPath}!");
            return;
        }

        string sfPath = Path.Combine(Application.streamingAssetsPath, "RLNDGM.SF2");
        if (!File.Exists(sfPath))
        {
            sfPath = null;
        }

        try
        {
            var args = new CommandLineArgs(new string[] { "iwad", wadPath });
            doom = new UnityDoom(args, sfPath);
            doom.OnLoad();
            doomVideoTexture = doom.GetVideoTexture();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DoomEasterEgg] Failed to initialize Doom engine: {ex}");
            StopDoom();
            return;
        }

        isPlayingDoom = true;

        // 1. Pause normal detector scanning
        if (detectorController != null)
        {
            detectorController.CancelScan(disrupted: false);
            detectorController.SetScanningEnabled(false);
        }

        // 2. Hide scanning UI canvas
        if (scanningCanvas != null)
        {
            scanningCanvas.gameObject.SetActive(false);
        }

        // 3. Pause magic camera if running
        if (magicCam != null)
        {
            magicCam.enabled = false;
        }

        // 4. Start Doom frame rendering loop
        if (doomLoopCoroutine != null) StopCoroutine(doomLoopCoroutine);
        doomLoopCoroutine = StartCoroutine(DoomGameLoopRoutine());

        if (flyHorizontally != null)
        {
            flyHorizontally.enabled = false;
        }

        if (vrHandMenu != null)
        {
            vrHandMenu.ToggleClose();
        }

        var allHandMenus = FindObjectsByType<VRHandMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var menu in allHandMenus)
        {
            menu.ToggleClose();
        }

        // 5. Trigger screen boot-up transition
        currentTransitionProgress = (transitionEffect != TransitionEffect.None) ? 0.0f : 1.0f;
        isTransitioningOut = false;
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        if (transitionEffect != TransitionEffect.None)
        {
            transitionCoroutine = StartCoroutine(TransitionInRoutine());
        }

        Debug.Log("<color=green>[DoomEasterEgg] DOOM ACTIVATED ON DETECTOR SCREEN!</color>");
    }

    public void StopDoom()
    {
        if (!isPlayingDoom && doom == null) return;
        if (isTransitioningOut) return;

        if (transitionEffect != TransitionEffect.None && gameObject.activeInHierarchy)
        {
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(TransitionOutAndShutdownRoutine());
        }
        else
        {
            ShutdownDoomImmediate();
        }
    }

    private IEnumerator TransitionInRoutine()
    {
        PlayTransitionAudio();
        float timer = 0f;
        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            currentTransitionProgress = Mathf.Clamp01(timer / transitionDuration);
            yield return null;
        }
        currentTransitionProgress = 1.0f;
        transitionCoroutine = null;
    }

    private IEnumerator TransitionOutAndShutdownRoutine()
    {
        isTransitioningOut = true;
        PlayTransitionAudio();
        float dur = Mathf.Max(0.05f, transitionDuration * 0.6f);
        float timer = dur;
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            currentTransitionProgress = Mathf.Clamp01(timer / dur);
            yield return null;
        }
        currentTransitionProgress = 0.0f;
        isTransitioningOut = false;
        transitionCoroutine = null;
        ShutdownDoomImmediate();
    }

    private void PlayTransitionAudio()
    {
        if (transitionSFX != null)
        {
            AudioSource.PlayClipAtPoint(transitionSFX, transform.position);
        }
    }

    public void ShutdownDoomImmediate()
    {
        if (!isPlayingDoom && doom == null) return;

        isPlayingDoom = false;

        if (doomLoopCoroutine != null)
        {
            StopCoroutine(doomLoopCoroutine);
            doomLoopCoroutine = null;
        }

        if (doom != null)
        {
            try
            {
                doom.OnClose();
                doom.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DoomEasterEgg] Exception while closing Doom: {ex}");
            }
            doom = null;
        }

        keysPressed.Clear();

        // Resume magic camera
        if (magicCam != null)
        {
            magicCam.enabled = true;
        }

        // Resume scanning canvas
        if (scanningCanvas != null)
        {
            scanningCanvas.gameObject.SetActive(true);
        }

        // Resume normal scanning
        if (detectorController != null)
        {
            detectorController.SetScanningEnabled(true);
        }

        if (flyHorizontally != null)
        {
            flyHorizontally.enabled = true;
        }

        if (vrHandMenu != null)
        {
            vrHandMenu.ToggleOpen();
        }

        var allHandMenus = FindObjectsByType<VRHandMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var menu in allHandMenus)
        {
            menu.ToggleOpen();
        }

        Debug.Log("<color=yellow>[DoomEasterEgg] Doom closed. Detector scanning restored.</color>");
    }

    #endregion

    #region Doom Game Loop & Rendering

    private IEnumerator DoomGameLoopRoutine()
    {
        float interval = 1f / doomTargetFps;
        WaitForSeconds wait = new WaitForSeconds(interval);

        while (isPlayingDoom && doom != null)
        {
            // 1. Tick Doom Engine
            doom.UpdateKeys(keysPressed);
            doom.OnUpdate();
            doom.OnRender();

            // 2. Output Framebuffer to Detector Screen with orientation fix
            if (targetRenderTexture != null && doomVideoTexture != null)
            {
                BlitWithOrientation(doomVideoTexture, targetRenderTexture);
            }

            yield return wait;
        }
    }

    private void BlitWithOrientation(Texture2D source, RenderTexture destination)
    {
        if (runtimeBlitMaterial == null)
        {
            InitializeBlitMaterial();
        }

        if (runtimeBlitMaterial != null)
        {
            runtimeBlitMaterial.DisableKeyword("_ROTATION_NONE");
            runtimeBlitMaterial.DisableKeyword("_ROTATION_ROTATE90CW");
            runtimeBlitMaterial.DisableKeyword("_ROTATION_ROTATE180");
            runtimeBlitMaterial.DisableKeyword("_ROTATION_ROTATE270CW");

            switch (screenRotation)
            {
                case ScreenRotation.None:
                    runtimeBlitMaterial.EnableKeyword("_ROTATION_NONE");
                    break;
                case ScreenRotation.Rotate90CW:
                    runtimeBlitMaterial.EnableKeyword("_ROTATION_ROTATE90CW");
                    break;
                case ScreenRotation.Rotate180:
                    runtimeBlitMaterial.EnableKeyword("_ROTATION_ROTATE180");
                    break;
                case ScreenRotation.Rotate270CW:
                    runtimeBlitMaterial.EnableKeyword("_ROTATION_ROTATE270CW");
                    break;
            }

            runtimeBlitMaterial.SetFloat("_FlipX", flipHorizontal ? 1f : 0f);
            runtimeBlitMaterial.SetFloat("_FlipY", flipVertical ? 1f : 0f);
            runtimeBlitMaterial.SetFloat("_Zoom", screenZoom);
            runtimeBlitMaterial.SetFloat("_AspectCorrection", aspectCorrection);
            runtimeBlitMaterial.SetFloat("_OffsetX", screenOffset.x);
            runtimeBlitMaterial.SetFloat("_OffsetY", screenOffset.y);

            // Transition effect keywords & progress
            runtimeBlitMaterial.DisableKeyword("_TRANSITIONTYPE_NONE");
            runtimeBlitMaterial.DisableKeyword("_TRANSITIONTYPE_CRTPOWERON");
            runtimeBlitMaterial.DisableKeyword("_TRANSITIONTYPE_DIGITALGLITCH");
            runtimeBlitMaterial.DisableKeyword("_TRANSITIONTYPE_RETROSTATIC");

            switch (transitionEffect)
            {
                case TransitionEffect.None:
                    runtimeBlitMaterial.EnableKeyword("_TRANSITIONTYPE_NONE");
                    break;
                case TransitionEffect.CrtPowerOn:
                    runtimeBlitMaterial.EnableKeyword("_TRANSITIONTYPE_CRTPOWERON");
                    break;
                case TransitionEffect.DigitalGlitch:
                    runtimeBlitMaterial.EnableKeyword("_TRANSITIONTYPE_DIGITALGLITCH");
                    break;
                case TransitionEffect.RetroStatic:
                    runtimeBlitMaterial.EnableKeyword("_TRANSITIONTYPE_RETROSTATIC");
                    break;
            }

            runtimeBlitMaterial.SetFloat("_TransitionProgress", currentTransitionProgress);

            Graphics.Blit(source, destination, runtimeBlitMaterial);
        }
        else
        {
            Graphics.Blit(source, destination);
        }
    }

    #endregion

    #region Input Mapping

    private void UpdateDoomInputs()
    {
        if (!isPlayingDoom || doom == null) return;

        // Check if we are in the Title/Demo sequence or Main Menu
        bool isMenuOrTitle = doom.Core != null && (doom.Core.Menu.Active || doom.Core.State == DoomState.Opening);

        Vector2 stick = ReadVector2(thumbstickAction);
        const float deadzone = 0.4f;

        if (isMenuOrTitle)
        {
            // ==========================================
            // MENU / TITLE SCREEN CONTROLS
            // ==========================================

            // Vertical stick navigates Menu Up / Down with repeat
            if (Mathf.Abs(stick.y) > deadzone)
            {
                menuVRepeatTimer -= Time.deltaTime;
                if (menuVRepeatTimer <= 0f)
                {
                    if (stick.y > deadzone)
                    {
                        StartCoroutine(TapKeyRoutine(KeyCode.UpArrow));
                    }
                    else
                    {
                        StartCoroutine(TapKeyRoutine(KeyCode.DownArrow));
                    }

                    menuVRepeatTimer = menuVWasCentered ? 0.32f : 0.16f;
                    menuVWasCentered = false;
                }
            }
            else
            {
                menuVWasCentered = true;
                menuVRepeatTimer = 0f;
            }

            // Horizontal stick adjusts sliders / toggles options
            if (Mathf.Abs(stick.x) > deadzone)
            {
                menuHRepeatTimer -= Time.deltaTime;
                if (menuHRepeatTimer <= 0f)
                {
                    if (stick.x > deadzone)
                        StartCoroutine(TapKeyRoutine(KeyCode.RightArrow));
                    else
                        StartCoroutine(TapKeyRoutine(KeyCode.LeftArrow));

                    menuHRepeatTimer = menuHWasCentered ? 0.32f : 0.16f;
                    menuHWasCentered = false;
                }
            }
            else
            {
                menuHWasCentered = true;
                menuHRepeatTimer = 0f;
            }

            // 'A' Button or Trigger = SELECT / CONFIRM (Enter key)
            if (WasActionJustPressed(primaryButtonAction) || WasActionJustPressed(triggerAction))
            {
                StartCoroutine(TapKeyRoutine(KeyCode.Return));
            }

            // 'B' Button = BACK / ESCAPE (Escape key)
            if (WasActionJustPressed(secondaryButtonAction))
            {
                StartCoroutine(TapKeyRoutine(KeyCode.Escape));
            }
        }
        else
        {
            // ==========================================
            // IN-GAMEPLAY CONTROLS
            // ==========================================

            // 1. Forward / Backward Movement
            SetDoomKey(KeyCode.W, stick.y > 0.25f);
            SetDoomKey(KeyCode.S, stick.y < -0.25f);

            // 2. Strafe vs Turn
            bool isStrafeMode = IsActionPressed(gripAction, 0.5f);

            if (isStrafeMode)
            {
                SetDoomKey(KeyCode.A, stick.x < -0.25f);
                SetDoomKey(KeyCode.D, stick.x > 0.25f);
                SetDoomKey(KeyCode.LeftArrow, false);
                SetDoomKey(KeyCode.RightArrow, false);
            }
            else
            {
                SetDoomKey(KeyCode.LeftArrow, stick.x < -0.25f);
                SetDoomKey(KeyCode.RightArrow, stick.x > 0.25f);
                SetDoomKey(KeyCode.A, false);
                SetDoomKey(KeyCode.D, false);
            }

            // 3. Trigger = Fire
            bool fire = IsActionPressed(triggerAction, 0.5f);
            SetDoomKey(KeyCode.LeftControl, fire);

            // 4. 'A' Button = Use (Open door / flip switch)
            bool use = IsActionPressed(primaryButtonAction, 0.5f);
            SetDoomKey(KeyCode.Space, use);

            // 5. 'B' Button = Next Weapon
            if (WasActionJustPressed(secondaryButtonAction))
            {
                CycleWeapon();
            }

            // 6. Thumbstick Click Quick Tap = Toggle Automap
            if (WasActionJustReleased(thumbstickClickAction) && !stickClickWasHeld && exitTimer < exitHoldTime)
            {
                StartCoroutine(TapKeyRoutine(KeyCode.Tab));
            }
        }

#if UNITY_EDITOR
        // Editor Keyboard testing fallback
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) SetDoomKey(KeyCode.W, true);
            if (Keyboard.current.sKey.isPressed) SetDoomKey(KeyCode.S, true);
            if (Keyboard.current.aKey.isPressed) SetDoomKey(KeyCode.A, true);
            if (Keyboard.current.dKey.isPressed) SetDoomKey(KeyCode.D, true);
            if (Keyboard.current.upArrowKey.wasPressedThisFrame) StartCoroutine(TapKeyRoutine(KeyCode.UpArrow));
            if (Keyboard.current.downArrowKey.wasPressedThisFrame) StartCoroutine(TapKeyRoutine(KeyCode.DownArrow));
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame) StartCoroutine(TapKeyRoutine(KeyCode.LeftArrow));
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame) StartCoroutine(TapKeyRoutine(KeyCode.RightArrow));
            if (Keyboard.current.enterKey.wasPressedThisFrame) StartCoroutine(TapKeyRoutine(KeyCode.Return));
            if (Keyboard.current.spaceKey.isPressed || Keyboard.current.eKey.isPressed) SetDoomKey(KeyCode.Space, true);
            if (Keyboard.current.leftCtrlKey.isPressed || (Mouse.current != null && Mouse.current.leftButton.isPressed)) SetDoomKey(KeyCode.LeftControl, true);
            if (Keyboard.current.tabKey.wasPressedThisFrame) StartCoroutine(TapKeyRoutine(KeyCode.Tab));
        }
#endif
    }

    private void CycleWeapon()
    {
        currentWeaponSlot = (currentWeaponSlot % 7) + 1;
        KeyCode key = weaponKeyCodes[currentWeaponSlot - 1];
        StartCoroutine(TapKeyRoutine(key));
    }

    private IEnumerator TapKeyRoutine(KeyCode key)
    {
        SetDoomKey(key, true);
        yield return new WaitForSeconds(0.08f);
        SetDoomKey(key, false);
    }

    private void SetDoomKey(KeyCode key, bool pressed)
    {
        if (pressed)
        {
            if (!keysPressed.Contains(key))
            {
                keysPressed.Add(key);
                doom?.KeyDown(key);
            }
        }
        else
        {
            if (keysPressed.Contains(key))
            {
                keysPressed.Remove(key);
                doom?.KeyUp(key);
            }
        }
    }

    #endregion

    #region Input Helpers

    private bool IsActionPressed(InputActionReference actionRef, float threshold)
    {
        if (actionRef == null || actionRef.action == null) return false;
        var action = actionRef.action;
        if (action.type == InputActionType.Button)
        {
            return action.IsPressed();
        }
        return action.ReadValue<float>() >= threshold;
    }

    private bool WasActionJustPressed(InputActionReference actionRef)
    {
        if (actionRef == null || actionRef.action == null) return false;
        return actionRef.action.WasPressedThisFrame();
    }

    private bool WasActionJustReleased(InputActionReference actionRef)
    {
        if (actionRef == null || actionRef.action == null) return false;
        return actionRef.action.WasReleasedThisFrame();
    }

    private Vector2 ReadVector2(InputActionReference actionRef)
    {
        if (actionRef == null || actionRef.action == null) return Vector2.zero;
        return actionRef.action.ReadValue<Vector2>();
    }

    #endregion
}

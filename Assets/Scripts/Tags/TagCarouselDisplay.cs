using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Pie menu de tags : les options sont disposees en cercle autour d'un element central.
/// Au depart, le centre affiche le tag vide (par exemple "selectionner un polluant").
/// Survol d'une option : les autres retrecissent.
/// Maintien sur une option : le fill central se remplit, a la fin l'option est confirmee.
/// Reponse correcte : le tag est affiche au centre, les options disparaissent.
/// Reponse incorrecte : le tag est affiche en rouge au centre pendant quelques secondes, puis retour a l'etat initial.
///
/// Detection du pointeur : chaque option a une zone de detection fixe (un Button avec son Image)
/// distincte de son visuel. Les visuels n'ont aucun raycast, et les zones ne changent jamais de taille ni de position.
/// </summary>
public class TagCarouselDisplay : MonoBehaviour
{
    enum State
    {
        Idle,
        Incorrect,
        Validated
    }

    // Relays standard UI pointer events into actions for each option hit zone.
    class OptionRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Action OnEnter;
        public Action OnExit;
        public Action OnDown;
        public Action OnUp;

        public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke();
        public void OnPointerDown(PointerEventData eventData) => OnDown?.Invoke();
        public void OnPointerUp(PointerEventData eventData) => OnUp?.Invoke();
    }

    class OptionItem
    {
        // Visual part, scaled and faded. Never receives raycasts.
        public RectTransform Rect;
        public CanvasGroup CanvasGroup;
        public Vector2 BasePosition;
        public Vector3 BaseScale = Vector3.one;

        // Detection part, fixed in size and position. The only element that receives raycasts.
        public RectTransform HitRect;
        public Image HitImage;
        public Button HitButton;
    }

    [Header("Layout")]
    [Tooltip("Parent of the option items. Should be centered on the pie menu and contain nothing else.")]
    [SerializeField] RectTransform itemsContainer;
    [Tooltip("Optional parent of the center display. If empty, the center item is created under Items Container.")]
    [SerializeField] RectTransform centerContainer;
    [Tooltip("Prefab used for the options, and for the center display if no Center Prefab is set (needs a TagDisplay or one will be added).")]
    [SerializeField] GameObject itemPrefab;
    [Tooltip("Optional custom prefab used for the center display. If empty, Item Prefab is used. Needs a TagDisplay or one will be added.")]
    [SerializeField] GameObject centerPrefab;
    [Tooltip("Distance between the center and each option.")]
    [SerializeField] float radius = 140f;
    [Tooltip("Angle in degrees of the first option. 90 is the top, options then go clockwise.")]
    [SerializeField] float startAngle = 90f;

    [Header("Hover Styling")]
    [Tooltip("Scale multiplier of the hovered option.")]
    [SerializeField] float hoveredScale = 1.2f;
    [Tooltip("Scale multiplier of the other options while one is hovered.")]
    [SerializeField] float shrunkScale = 0.75f;
    [SerializeField] float hoverScaleDuration = 0.15f;
    [SerializeField] Ease hoverEase = Ease.OutCubic;
    [Tooltip("Alpha of the other options while one is hovered. 1 keeps them fully opaque.")]
    [Range(0f, 1f)]
    [SerializeField] float unhoveredAlpha = 0.5f;
    [SerializeField] float hoverFadeDuration = 0.15f;

    [Header("Hold To Validate")]
    [Tooltip("Time in seconds the option must stay pressed to be validated.")]
    [SerializeField] float holdDuration = 0.8f;
    [Tooltip("Time in seconds for the center fill to empty when the press is released early.")]
    [SerializeField] float fillReleaseDuration = 0.25f;
    [Tooltip("Time in seconds the hold survives when the pointer leaves the option, so a shaky VR ray does not cancel it. Set to 0 to cancel immediately.")]
    [SerializeField] float holdExitGraceDuration = 0.25f;

    [Header("Pointer Hit Zones")]
    [Tooltip("Extra size in canvas units added around each option for pointer detection.")]
    [SerializeField] float hitAreaPadding = 8f;
    [Tooltip("Color of the hit zones. Keep a tiny alpha so they stay invisible, or raise the alpha to see them while debugging.")]
    [SerializeField] Color hitZoneColor = new Color(1f, 1f, 1f, 0.01f);
    [Tooltip("Size used for the hit zones if the item prefab has no readable size.")]
    [SerializeField] Vector2 fallbackHitZoneSize = new Vector2(100f, 100f);

    [Header("Options Show / Hide Animation")]
    [SerializeField] float optionsShowDuration = 0.3f;
    [SerializeField] float optionsShowStagger = 0.03f;
    [SerializeField] float optionsHideDuration = 0.2f;

    [Header("Validation Result Feedback")]
    [Tooltip("Tint applied to the center tag when the answer is correct.")]
    [SerializeField] Color correctFeedbackColor = new Color(0.2f, 0.85f, 0.3f, 1f);
    [Tooltip("Tint applied to the center tag when the answer is incorrect.")]
    [SerializeField] Color incorrectFeedbackColor = new Color(0.9f, 0.25f, 0.2f, 1f);
    [Tooltip("If true, the center tag is also tinted when the answer is correct.")]
    [SerializeField] bool tintCenterOnCorrect = true;
    [Tooltip("Tint the text of the center element. Uncheck to keep the text color of the prefab.")]
    [SerializeField] bool tintCenterText = true;
    [Tooltip("Tint the other graphics of the center element (background, decorations). Uncheck to keep their colors from the prefab.")]
    [SerializeField] bool tintCenterGraphics = true;
    [Tooltip("How much of the feedback color replaces the original color. 0 keeps the prefab colors, 1 uses the feedback color fully.")]
    [Range(0f, 1f)]
    [SerializeField] float feedbackTintStrength = 1f;
    [Tooltip("Scale punch multiplier applied to the center tag when feedback appears.")]
    [SerializeField] float feedbackPunchScale = 1.25f;
    [SerializeField] float feedbackPunchDuration = 0.3f;
    [Tooltip("Time in seconds the incorrect answer stays in the center before everything resets.")]
    [SerializeField] float incorrectDisplayDuration = 2f;

    [Header("Debug")]
    [Tooltip("Logs pointer events received by the options and checks the XR UI setup once.")]
    [SerializeField] bool debugLogs = false;

    /// <summary>Triggered when an option has been held long enough. Parameter is the index in the available tags array.</summary>
    public event Action<int> OnOptionConfirmed;

    /// <summary>Triggered once the incorrect feedback is over and the pie menu is back to its initial state.</summary>
    public event Action OnIncorrectFeedbackFinished;

    readonly List<OptionItem> options = new List<OptionItem>();
    readonly List<Graphic> centerGraphics = new List<Graphic>();
    readonly List<Color> centerGraphicColors = new List<Color>();

    TagData[] tags;
    TagData emptyData;

    GameObject centerInstance;
    RectTransform centerRect;
    TagDisplay centerDisplay;
    Image holdFillImage;
    Vector3 centerBaseScale = Vector3.one;

    State state = State.Idle;
    int hoveredIndex = -1;
    int holdingIndex = -1;
    float holdProgress = 0f;
    float holdGraceTimer = 0f;
    Tween incorrectTimer;

    bool interactionSetupChecked = false;
    bool missingFillWarned = false;

    void OnEnable()
    {
        if (debugLogs)
        {
            CheckInteractionSetup();
        }
    }

    void OnDisable()
    {
        holdingIndex = -1;
        hoveredIndex = -1;
        ResetHoldProgress();

        for (int i = 0; i < options.Count; i++)
        {
            OptionItem option = options[i];
            if (option.Rect == null)
                continue;

            if (state == State.Idle)
            {
                option.Rect.DOKill();
                option.Rect.localScale = option.BaseScale;

                if (option.CanvasGroup != null)
                {
                    option.CanvasGroup.DOKill();
                    option.CanvasGroup.alpha = 1f;
                }
            }
        }
    }

    void OnDestroy()
    {
        KillIncorrectTimer();

        for (int i = 0; i < options.Count; i++)
        {
            OptionItem option = options[i];
            if (option.Rect != null) option.Rect.DOKill();
            if (option.CanvasGroup != null) option.CanvasGroup.DOKill();
        }

        if (centerRect != null)
        {
            centerRect.DOKill();
        }
    }

    void Update()
    {
        if (state != State.Idle || options.Count == 0)
            return;

        float dt = Time.deltaTime;

        // The hold survives short moments where the pointer is outside the option
        if (holdingIndex >= 0)
        {
            if (hoveredIndex == holdingIndex)
            {
                holdGraceTimer = holdExitGraceDuration;
            }
            else
            {
                holdGraceTimer -= dt;

                if (holdGraceTimer <= 0f)
                {
                    holdingIndex = -1;
                }
            }
        }

        float previous = holdProgress;

        if (holdingIndex >= 0)
        {
            holdProgress = holdDuration <= 0f
                ? 1f
                : Mathf.Min(1f, previous + dt / holdDuration);
        }
        else if (previous > 0f)
        {
            holdProgress = fillReleaseDuration <= 0f
                ? 0f
                : Mathf.MoveTowards(previous, 0f, dt / fillReleaseDuration);
        }

        if (!Mathf.Approximately(previous, holdProgress))
        {
            SetCenterFill(holdProgress);
        }

        if (holdingIndex >= 0 && holdProgress >= 1f)
        {
            ConfirmOption(holdingIndex);
        }
    }

    // ---------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------

    /// <summary>
    /// Builds the pie menu: the empty tag placeholder in the center and one option per available tag around it.
    /// </summary>
    public void Initialize(TagData[] availableTags, TagData emptyTagData)
    {
        if (itemsContainer == null || itemPrefab == null)
        {
            Debug.LogWarning("TagCarouselDisplay: Items Container or Item Prefab is not assigned, the pie menu cannot be built.", this);
            return;
        }

        KillIncorrectTimer();
        ClearAll();

        tags = availableTags;
        emptyData = emptyTagData;

        state = State.Idle;
        hoveredIndex = -1;
        holdingIndex = -1;
        holdProgress = 0f;

        BuildCenter();
        SetupHoldFill();
        BuildOptions();
        ShowOptions(false);
    }

    /// <summary>
    /// Shows the given tag in the center, hides the options and locks the pie menu.
    /// </summary>
    public void ShowCorrectFeedback(int availableIndex)
    {
        KillIncorrectTimer();

        state = State.Validated;
        holdingIndex = -1;
        hoveredIndex = -1;
        ResetHoldProgress();

        ShowSelectedInCenter(availableIndex, tintCenterOnCorrect, correctFeedbackColor);
        HideOptions(true);
    }

    /// <summary>
    /// Shows the given tag in red in the center, hides the options, then restores the initial state after the configured delay.
    /// </summary>
    public void ShowIncorrectFeedback(int availableIndex)
    {
        KillIncorrectTimer();

        state = State.Incorrect;
        holdingIndex = -1;
        hoveredIndex = -1;
        ResetHoldProgress();

        ShowSelectedInCenter(availableIndex, true, incorrectFeedbackColor);
        HideOptions(true);

        incorrectTimer = DOVirtual.DelayedCall(incorrectDisplayDuration, HandleIncorrectFinished);
    }

    /// <summary>
    /// Locks the pie menu on the validated tag, or unlocks it and goes back to the initial state.
    /// </summary>
    public void SetValidated(bool validated, int availableIndex = -1)
    {
        if (validated)
        {
            ShowCorrectFeedback(availableIndex);
        }
        else
        {
            if (state == State.Idle)
                return;

            ResetToInitial(true);
        }
    }

    // ---------------------------------------------------------------------
    // Setup checks (debug only, nothing is modified)
    // ---------------------------------------------------------------------

    void CheckInteractionSetup()
    {
        if (interactionSetupChecked)
            return;

        interactionSetupChecked = true;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("TagCarouselDisplay: no parent Canvas found, the menu cannot receive pointer events.", this);
        }
        else
        {
            Canvas rootCanvas = canvas.rootCanvas;

            if (rootCanvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            {
                Debug.LogWarning("TagCarouselDisplay: the root Canvas has no TrackedDeviceGraphicRaycaster, XR rays will not hit the options.", rootCanvas);
            }

            if (rootCanvas.renderMode != RenderMode.WorldSpace)
            {
                Debug.LogWarning("TagCarouselDisplay: the root Canvas is not in World Space render mode.", rootCanvas);
            }
        }

        if (EventSystem.current == null)
        {
            Debug.LogWarning("TagCarouselDisplay: no EventSystem found in the scene.", this);
        }
        else if (EventSystem.current.GetComponent<XRUIInputModule>() == null)
        {
            Debug.LogWarning("TagCarouselDisplay: the EventSystem has no XRUIInputModule.", EventSystem.current);
        }
    }

    // ---------------------------------------------------------------------
    // Build
    // ---------------------------------------------------------------------

    void ClearAll()
    {
        for (int i = 0; i < options.Count; i++)
        {
            OptionItem option = options[i];
            if (option.CanvasGroup != null) option.CanvasGroup.DOKill();

            if (option.Rect != null)
            {
                option.Rect.DOKill();
                Destroy(option.Rect.gameObject);
            }

            if (option.HitRect != null)
            {
                Destroy(option.HitRect.gameObject);
            }
        }

        options.Clear();

        if (centerRect != null) centerRect.DOKill();
        if (centerInstance != null) Destroy(centerInstance);

        centerInstance = null;
        centerRect = null;
        centerDisplay = null;
        holdFillImage = null;
        centerGraphics.Clear();
        centerGraphicColors.Clear();
    }

    // Prepares the fill image of the center prefab so it can be driven by Fill Amount.
    void SetupHoldFill()
    {
        if (holdFillImage == null)
        {
            if (!missingFillWarned)
            {
                missingFillWarned = true;
                Debug.LogWarning("TagCarouselDisplay: no Hold Fill assigned on the TagDisplay of the center prefab, the hold will work but without visual fill.", this);
            }

            return;
        }

        holdFillImage.raycastTarget = false;

        if (holdFillImage.type != Image.Type.Filled)
        {
            holdFillImage.type = Image.Type.Filled;
        }

        holdFillImage.fillAmount = 0f;
    }

    void BuildCenter()
    {
        Transform parent = centerContainer != null ? centerContainer : itemsContainer;
        GameObject prefab = centerPrefab != null ? centerPrefab : itemPrefab;

        centerInstance = Instantiate(prefab, parent);
        centerRect = centerInstance.GetComponent<RectTransform>();
        EnsureCenteredAnchors(centerRect);
        centerRect.anchoredPosition = Vector2.zero;
        centerBaseScale = centerRect.localScale;

        centerDisplay = centerInstance.GetComponent<TagDisplay>();
        if (centerDisplay == null)
        {
            centerDisplay = centerInstance.AddComponent<TagDisplay>();
        }

        // The fill is assigned on the TagDisplay of the center prefab
        holdFillImage = centerDisplay.HoldFill;

        // The center display is never interactive
        DisableAllRaycasts(centerInstance);

        if (emptyData != null)
        {
            SetCenterData(emptyData);
            centerInstance.SetActive(true);
        }
        else
        {
            CacheCenterColors();
            centerInstance.SetActive(false);
        }
    }

    void BuildOptions()
    {
        int count = tags != null ? tags.Length : 0;

        // First the visuals, which never receive raycasts
        for (int i = 0; i < count; i++)
        {
            GameObject instance = Instantiate(itemPrefab, itemsContainer);
            instance.SetActive(true);

            RectTransform rect = instance.GetComponent<RectTransform>();
            EnsureCenteredAnchors(rect);

            TagDisplay tagDisplay = instance.GetComponent<TagDisplay>();
            if (tagDisplay == null)
            {
                tagDisplay = instance.AddComponent<TagDisplay>();
            }

            if (tags[i] != null)
            {
                tagDisplay.SetData(tags[i]);
            }

            // Raycast is disabled on all item visuals so the pointer reaches the hit zone
            DisableAllRaycasts(instance);

            CanvasGroup canvasGroup = instance.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = instance.AddComponent<CanvasGroup>();
            }

            OptionItem option = new OptionItem
            {
                Rect = rect,
                CanvasGroup = canvasGroup,
                BasePosition = GetOptionPosition(i, count),
                BaseScale = rect.localScale
            };

            options.Add(option);
        }

        // Then the hit zones, created last so they sit on top of every visual
        for (int i = 0; i < options.Count; i++)
        {
            CreateHitZone(options[i], i);
        }
    }

    // A fixed Button with an Image, never scaled or moved, which is the only raycast target of the option.
    void CreateHitZone(OptionItem option, int index)
    {
        GameObject zone = new GameObject("HitZone_" + index, typeof(RectTransform), typeof(Image), typeof(Button));
        zone.transform.SetParent(itemsContainer, false);

        Vector2 itemSize = option.Rect.rect.size;
        if (itemSize.x <= 0f || itemSize.y <= 0f)
        {
            itemSize = fallbackHitZoneSize;
        }

        // Position of the visual center, in case the prefab pivot is not centered
        Vector2 pivotOffset = Vector2.Scale(new Vector2(0.5f, 0.5f) - option.Rect.pivot, itemSize);

        RectTransform zoneRect = zone.GetComponent<RectTransform>();
        zoneRect.anchorMin = new Vector2(0.5f, 0.5f);
        zoneRect.anchorMax = new Vector2(0.5f, 0.5f);
        zoneRect.pivot = new Vector2(0.5f, 0.5f);
        zoneRect.sizeDelta = itemSize + Vector2.one * (Mathf.Abs(hitAreaPadding) * 2f);
        zoneRect.anchoredPosition = option.BasePosition + pivotOffset;
        zoneRect.localScale = Vector3.one;
        zoneRect.localRotation = Quaternion.identity;

        Image zoneImage = zone.GetComponent<Image>();
        zoneImage.color = hitZoneColor;
        zoneImage.raycastTarget = true;

        // Keeps the zone registered for raycasts even with a very low alpha
        zoneImage.canvasRenderer.cullTransparentMesh = false;

        Button zoneButton = zone.GetComponent<Button>();
        zoneButton.transition = Selectable.Transition.None;
        zoneButton.targetGraphic = zoneImage;

        int capturedIndex = index;
        OptionRelay relay = zone.AddComponent<OptionRelay>();
        relay.OnEnter = () => HandlePointerEnter(capturedIndex);
        relay.OnExit = () => HandlePointerExit(capturedIndex);
        relay.OnDown = () => HandlePointerDown(capturedIndex);
        relay.OnUp = () => HandlePointerUp(capturedIndex);

        option.HitRect = zoneRect;
        option.HitImage = zoneImage;
        option.HitButton = zoneButton;
    }

    static void DisableAllRaycasts(GameObject root)
    {
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }

    Vector2 GetOptionPosition(int index, int count)
    {
        if (count <= 0)
            return Vector2.zero;

        float step = 360f / count;
        float angle = (startAngle - index * step) * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    // Only touches the anchors when they are not already centered, so the size of the prefab is never overwritten needlessly.
    static void EnsureCenteredAnchors(RectTransform rect)
    {
        if (rect == null)
            return;

        Vector2 center = new Vector2(0.5f, 0.5f);

        if (rect.anchorMin == center && rect.anchorMax == center)
            return;

        Vector2 size = rect.rect.size;
        rect.anchorMin = center;
        rect.anchorMax = center;
        rect.sizeDelta = size;
    }

    // ---------------------------------------------------------------------
    // Pointer handling
    // ---------------------------------------------------------------------

    void HandlePointerEnter(int index)
    {
        if (debugLogs) Debug.Log("TagCarouselDisplay: pointer enter option " + index, this);

        if (state != State.Idle)
            return;

        hoveredIndex = index;
        ApplyHoverStyles(true);
    }

    void HandlePointerExit(int index)
    {
        if (debugLogs) Debug.Log("TagCarouselDisplay: pointer exit option " + index, this);

        if (hoveredIndex == index)
        {
            hoveredIndex = -1;

            if (state == State.Idle)
            {
                ApplyHoverStyles(true);
            }
        }

        // A hold in progress is cancelled by Update once the grace duration is over
        if (holdExitGraceDuration <= 0f && holdingIndex == index)
        {
            holdingIndex = -1;
        }
    }

    void HandlePointerDown(int index)
    {
        if (debugLogs) Debug.Log("TagCarouselDisplay: pointer down option " + index, this);

        if (state != State.Idle)
            return;

        // A press on an option means the pointer is on it, even if the enter event was missed
        if (hoveredIndex != index)
        {
            hoveredIndex = index;
            ApplyHoverStyles(true);
        }

        // Every new press restarts the center fill from zero
        ResetHoldProgress();

        holdingIndex = index;
        holdGraceTimer = holdExitGraceDuration;
    }

    void HandlePointerUp(int index)
    {
        if (debugLogs) Debug.Log("TagCarouselDisplay: pointer up option " + index, this);

        if (holdingIndex == index)
        {
            holdingIndex = -1;
        }
    }

    void ConfirmOption(int index)
    {
        if (debugLogs) Debug.Log("TagCarouselDisplay: option " + index + " confirmed", this);

        holdingIndex = -1;
        OnOptionConfirmed?.Invoke(index);
    }

    // ---------------------------------------------------------------------
    // Visuals
    // ---------------------------------------------------------------------

    void ApplyHoverStyles(bool animate)
    {
        for (int i = 0; i < options.Count; i++)
        {
            OptionItem option = options[i];
            if (option.Rect == null)
                continue;

            float multiplier = 1f;
            if (hoveredIndex >= 0)
            {
                multiplier = i == hoveredIndex ? hoveredScale : shrunkScale;
            }

            Vector3 targetScale = option.BaseScale * multiplier;

            // The hovered option stays opaque, the others become more transparent
            float targetAlpha = 1f;
            if (hoveredIndex >= 0 && i != hoveredIndex)
            {
                targetAlpha = unhoveredAlpha;
            }

            option.Rect.DOKill();
            option.CanvasGroup.DOKill();

            if (animate)
            {
                option.Rect.DOScale(targetScale, hoverScaleDuration).SetEase(hoverEase);
                option.CanvasGroup.DOFade(targetAlpha, hoverFadeDuration).SetEase(hoverEase);
            }
            else
            {
                option.Rect.localScale = targetScale;
                option.CanvasGroup.alpha = targetAlpha;
            }
        }
    }

    void SetCenterFill(float progress)
    {
        if (holdFillImage == null)
            return;

        holdFillImage.fillAmount = progress;
    }

    void ResetHoldProgress()
    {
        holdProgress = 0f;
        SetCenterFill(0f);
    }

    // Only the hit zone is toggled, the visuals never receive raycasts.
    void SetOptionInteractable(OptionItem option, bool interactable)
    {
        if (option.HitImage != null)
        {
            option.HitImage.raycastTarget = interactable;
        }

        if (option.HitButton != null)
        {
            option.HitButton.interactable = interactable;
        }
    }

    void ShowOptions(bool animate)
    {
        for (int i = 0; i < options.Count; i++)
        {
            OptionItem option = options[i];
            if (option.Rect == null)
                continue;

            option.Rect.DOKill();
            option.CanvasGroup.DOKill();

            option.Rect.anchoredPosition = option.BasePosition;
            SetOptionInteractable(option, true);

            if (animate)
            {
                float delay = i * optionsShowStagger;

                option.Rect.localScale = Vector3.zero;
                option.CanvasGroup.alpha = 0f;

                option.Rect.DOScale(option.BaseScale, optionsShowDuration)
                    .SetEase(Ease.OutBack)
                    .SetDelay(delay);

                option.CanvasGroup.DOFade(1f, optionsShowDuration)
                    .SetDelay(delay);
            }
            else
            {
                option.Rect.localScale = option.BaseScale;
                option.CanvasGroup.alpha = 1f;
            }
        }
    }

    void HideOptions(bool animate)
    {
        for (int i = 0; i < options.Count; i++)
        {
            OptionItem option = options[i];
            if (option.Rect == null)
                continue;

            option.Rect.DOKill();
            option.CanvasGroup.DOKill();

            SetOptionInteractable(option, false);

            if (animate)
            {
                option.Rect.DOScale(Vector3.zero, optionsHideDuration).SetEase(Ease.InBack);
                option.CanvasGroup.DOFade(0f, optionsHideDuration);
            }
            else
            {
                option.Rect.localScale = Vector3.zero;
                option.CanvasGroup.alpha = 0f;
            }
        }
    }

    // ---------------------------------------------------------------------
    // Center display
    // ---------------------------------------------------------------------

    void SetCenterData(TagData data)
    {
        if (centerDisplay == null)
            return;

        RestoreCenterColors();
        centerDisplay.SetData(data);
        CacheCenterColors();
    }

    void CacheCenterColors()
    {
        centerGraphics.Clear();
        centerGraphicColors.Clear();

        if (centerInstance == null)
            return;

        foreach (Graphic graphic in centerInstance.GetComponentsInChildren<Graphic>(true))
        {
            // The fill keeps its own color and is never tinted
            if (holdFillImage != null && graphic == holdFillImage)
                continue;

            centerGraphics.Add(graphic);
            centerGraphicColors.Add(graphic.color);
        }
    }

    void RestoreCenterColors()
    {
        for (int i = 0; i < centerGraphics.Count; i++)
        {
            if (centerGraphics[i] != null)
            {
                centerGraphics[i].color = centerGraphicColors[i];
            }
        }
    }

    // Tints the center according to the Tint Center Text, Tint Center Graphics and Feedback Tint Strength settings.
    void TintCenter(Color tint)
    {
        for (int i = 0; i < centerGraphics.Count; i++)
        {
            Graphic graphic = centerGraphics[i];
            if (graphic == null)
                continue;

            bool isText = graphic is TMP_Text;

            if (isText && !tintCenterText)
                continue;

            if (!isText && !tintCenterGraphics)
                continue;

            Color original = centerGraphicColors[i];
            Color target = new Color(tint.r, tint.g, tint.b, original.a);

            graphic.color = Color.Lerp(original, target, feedbackTintStrength);
        }
    }

    void ShowSelectedInCenter(int availableIndex, bool tint, Color tintColor)
    {
        if (centerInstance == null)
            return;

        bool validIndex = tags != null
            && availableIndex >= 0
            && availableIndex < tags.Length
            && tags[availableIndex] != null;

        if (validIndex)
        {
            SetCenterData(tags[availableIndex]);
        }
        else
        {
            RestoreCenterColors();
        }

        centerInstance.SetActive(true);

        if (tint)
        {
            TintCenter(tintColor);
        }

        PunchCenter();
    }

    void PunchCenter()
    {
        if (centerRect == null)
            return;

        centerRect.DOKill();
        centerRect.localScale = centerBaseScale;

        if (feedbackPunchDuration <= 0f)
            return;

        centerRect.DOPunchScale(
            centerBaseScale * (feedbackPunchScale - 1f),
            feedbackPunchDuration,
            vibrato: 1,
            elasticity: 0.5f
        );
    }

    // ---------------------------------------------------------------------
    // State changes
    // ---------------------------------------------------------------------

    void HandleIncorrectFinished()
    {
        incorrectTimer = null;

        if (state != State.Incorrect)
            return;

        ResetToInitial(true);
        OnIncorrectFeedbackFinished?.Invoke();
    }

    void ResetToInitial(bool animate)
    {
        KillIncorrectTimer();

        state = State.Idle;
        holdingIndex = -1;
        hoveredIndex = -1;
        ResetHoldProgress();

        if (centerRect != null)
        {
            centerRect.DOKill();
            centerRect.localScale = centerBaseScale;

            if (emptyData != null)
            {
                SetCenterData(emptyData);
                centerInstance.SetActive(true);
            }
            else
            {
                RestoreCenterColors();
                centerInstance.SetActive(false);
            }
        }

        ShowOptions(animate);
    }

    void KillIncorrectTimer()
    {
        if (incorrectTimer != null && incorrectTimer.IsActive())
        {
            incorrectTimer.Kill();
        }

        incorrectTimer = null;
    }
}

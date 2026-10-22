using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Pie menu of tags. The options are laid out in a circle around a center element that shows the empty tag.
/// Hovering an option shrinks, fades and pulls the other options toward the center.
/// Holding the press on an option fills the center fill, and the option is confirmed once the fill is complete.
/// A correct answer is shown in the center and the options disappear.
/// An incorrect answer is shown in the center for a few seconds, then everything returns to its initial state.
/// Pointer detection uses fixed hit zones (Button + Image) separate from the option visuals, which never receive raycasts.
/// </summary>
public class TagCarouselDisplay : MonoBehaviour
{
    enum State { Idle, Incorrect, Validated }

    // Remembers the colors of a group of graphics so they can be tinted and restored later.
    class GraphicSet
    {
        readonly List<Graphic> graphics = new List<Graphic>();
        readonly List<Color> baseColors = new List<Color>();

        // Stores the current colors of every graphic under root, except the excluded one.
        public void Cache(Transform root, Graphic exclude = null)
        {
            graphics.Clear();
            baseColors.Clear();

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == exclude)
                    continue;

                graphics.Add(graphic);
                baseColors.Add(graphic.color);
            }
        }

        // Moves the graphics toward the tint (RGB only, alpha is kept). A null tint moves them back to their stored colors.
        public void Apply(Color? tint, bool tintText, bool tintOthers, float strength, float duration)
        {
            for (int i = 0; i < graphics.Count; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null || (graphic is TMP_Text ? !tintText : !tintOthers))
                    continue;

                Color color = baseColors[i];
                if (tint.HasValue)
                {
                    color = Color.Lerp(color, new Color(tint.Value.r, tint.Value.g, tint.Value.b, color.a), strength);
                }

                graphic.DOKill();

                if (duration > 0f) graphic.DOColor(color, duration);
                else graphic.color = color;
            }
        }

        public void Restore(float duration = 0f) => Apply(null, true, true, 1f, duration);

        public void Kill()
        {
            foreach (Graphic graphic in graphics)
            {
                if (graphic != null) graphic.DOKill();
            }
        }
    }

    // Forwards the pointer events of a hit zone to the carousel.
    class OptionRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public TagCarouselDisplay Owner;
        public int Index;

        public void OnPointerEnter(PointerEventData e) => Owner.OnOptionEnter(Index);
        public void OnPointerExit(PointerEventData e) => Owner.OnOptionExit(Index);
        public void OnPointerDown(PointerEventData e) => Owner.OnOptionDown(Index);
        public void OnPointerUp(PointerEventData e) => Owner.OnOptionUp(Index);
    }

    class OptionItem
    {
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector2 BasePos;
        public Vector3 BaseScale;
        public readonly GraphicSet Colors = new GraphicSet();

        // Fixed detection zone, the only raycast target of the option
        public Image HitImage;
        public Button HitButton;
    }

    [Header("Layout")]
    [Tooltip("Parent of the options. Should be centered on the pie menu and empty.")]
    [SerializeField] RectTransform itemsContainer;
    [Tooltip("Optional parent of the center display. If empty, the center is created under Items Container.")]
    [SerializeField] RectTransform centerContainer;
    [Tooltip("Prefab of the options, also used for the center if no Center Prefab is set. Needs a TagDisplay or one is added.")]
    [SerializeField] GameObject itemPrefab;
    [Tooltip("Optional custom prefab of the center display. Its TagDisplay can hold the Hold Fill image.")]
    [SerializeField] GameObject centerPrefab;
    [Tooltip("Distance between the center and each option.")]
    [SerializeField] float radius = 140f;
    [Tooltip("Angle in degrees of the first option. 90 is the top, the other options follow clockwise.")]
    [SerializeField] float startAngle = 90f;

    [Header("Hover Styling")]
    [Tooltip("Scale multiplier of the hovered option.")]
    [SerializeField] float hoveredScale = 1.2f;
    [Tooltip("Scale multiplier of the other options while one is hovered.")]
    [SerializeField] float shrunkScale = 0.75f;
    [Tooltip("Alpha of the other options while one is hovered.")]
    [Range(0f, 1f)]
    [SerializeField] float unhoveredAlpha = 0.5f;
    [Tooltip("Distance multiplier of the other options while one is hovered. Below 1 they move toward the center, 1 keeps them in place.")]
    [Range(0f, 1f)]
    [SerializeField] float unhoveredRadiusScale = 0.85f;
    [Tooltip("Duration in seconds of the scale, fade and move animations.")]
    [SerializeField] float hoverDuration = 0.15f;
    [SerializeField] Ease hoverEase = Ease.OutCubic;

    [Header("Hold To Validate")]
    [Tooltip("Time in seconds the option must stay pressed to be validated.")]
    [SerializeField] float holdDuration = 0.8f;
    [Tooltip("Time in seconds for the fill to empty when the press is released early.")]
    [SerializeField] float fillReleaseDuration = 0.25f;
    [Tooltip("Time in seconds the hold survives when the pointer leaves the option, so a shaky VR ray does not cancel it. 0 cancels immediately.")]
    [SerializeField] float holdExitGraceDuration = 0.25f;

    [Header("Held Option Color")]
    [Tooltip("Color applied to the option while it is held.")]
    [SerializeField] Color heldOptionColor = new Color(1f, 0.85f, 0.2f, 1f);
    [Tooltip("Tint the text of the held option. Uncheck to keep the text color of the prefab.")]
    [SerializeField] bool tintHeldText = true;
    [Tooltip("Tint the other graphics of the held option (background, decorations).")]
    [SerializeField] bool tintHeldGraphics = true;
    [Tooltip("How much of the held color replaces the original color. 0 keeps the prefab colors, 1 uses the held color fully.")]
    [Range(0f, 1f)]
    [SerializeField] float heldTintStrength = 1f;
    [Tooltip("Duration in seconds of the color change when an option starts or stops being held.")]
    [SerializeField] float heldTintDuration = 0.1f;

    [Header("Audio")]
    [Tooltip("Optional. Source of the hover sound. A 3D source is created on this object if empty.")]
    [SerializeField] AudioSource hoverAudioSource;
    [Tooltip("Optional. Source of the looping hold sound. A 3D source is created on this object if empty. Do not share it, its pitch changes.")]
    [SerializeField] AudioSource holdAudioSource;
    [Tooltip("Played each time the pointer moves onto an option.")]
    [SerializeField] AudioClip hoverSFX;
    [Range(0f, 1f)]
    [SerializeField] float hoverSFXVolume = 1f;
    [Tooltip("Looped while an option is held.")]
    [SerializeField] AudioClip holdLoopSFX;
    [Range(0f, 1f)]
    [SerializeField] float holdSFXVolume = 1f;
    [Tooltip("Pitch of the hold sound when the hold starts.")]
    [SerializeField] float holdPitchStart = 0.8f;
    [Tooltip("Pitch of the hold sound when the hold is about to complete.")]
    [SerializeField] float holdPitchEnd = 1.4f;

    [Header("Pointer Hit Zones")]
    [Tooltip("Extra size in canvas units added around each option for pointer detection.")]
    [SerializeField] float hitAreaPadding = 8f;
    [Tooltip("Color of the hit zones. Keep a tiny alpha to stay invisible, or raise it to see the zones while debugging.")]
    [SerializeField] Color hitZoneColor = new Color(1f, 1f, 1f, 0.01f);
    [Tooltip("Size of the hit zones if the option prefab has no readable size.")]
    [SerializeField] Vector2 fallbackHitZoneSize = new Vector2(100f, 100f);

    [Header("Options Show / Hide Animation")]
    [SerializeField] float optionsShowDuration = 0.3f;
    [SerializeField] float optionsShowStagger = 0.03f;
    [SerializeField] float optionsHideDuration = 0.2f;

    [Header("Validation Result Feedback")]
    [Tooltip("Tint of the center when the answer is correct.")]
    [SerializeField] Color correctFeedbackColor = new Color(0.2f, 0.85f, 0.3f, 1f);
    [Tooltip("Tint of the center when the answer is incorrect.")]
    [SerializeField] Color incorrectFeedbackColor = new Color(0.9f, 0.25f, 0.2f, 1f);
    [Tooltip("If false, the center is not tinted when the answer is correct.")]
    [SerializeField] bool tintCenterOnCorrect = true;
    [Tooltip("Tint the text of the center. Uncheck to keep the text color of the prefab.")]
    [SerializeField] bool tintCenterText = true;
    [Tooltip("Tint the other graphics of the center (background, decorations).")]
    [SerializeField] bool tintCenterGraphics = true;
    [Tooltip("How much of the feedback color replaces the original color. 0 keeps the prefab colors, 1 uses the feedback color fully.")]
    [Range(0f, 1f)]
    [SerializeField] float feedbackTintStrength = 1f;
    [Tooltip("Scale punch multiplier of the center when the feedback appears.")]
    [SerializeField] float feedbackPunchScale = 1.25f;
    [SerializeField] float feedbackPunchDuration = 0.3f;
    [Tooltip("Time in seconds an incorrect answer stays in the center before everything resets.")]
    [SerializeField] float incorrectDisplayDuration = 2f;

    /// <summary>Triggered when an option has been held long enough. The parameter is the index in the available tags.</summary>
    public event Action<int> OnOptionConfirmed;

    /// <summary>Triggered once the incorrect feedback is over and the pie menu is back to its initial state.</summary>
    public event Action OnIncorrectFeedbackFinished;

    readonly List<OptionItem> options = new List<OptionItem>();
    readonly GraphicSet centerColors = new GraphicSet();

    TagData[] tags;
    TagData emptyData;

    GameObject centerInstance;
    RectTransform centerRect;
    TagDisplay centerDisplay;
    Image holdFill;
    Vector3 centerBaseScale = Vector3.one;

    State state = State.Idle;
    int hoveredIndex = -1;
    int holdingIndex = -1;
    int heldVisualIndex = -1;
    float holdProgress;
    float graceTimer;
    Tween incorrectTimer;

    void OnDisable()
    {
        holdingIndex = hoveredIndex = -1;
        SetHoldProgress(0f);
        StopHoldFeedback();

        if (state == State.Idle)
        {
            ApplyHoverStyles(false);
        }
    }

    void OnDestroy()
    {
        KillIncorrectTimer();
        ClearAll();
    }

    void Update()
    {
        if (state != State.Idle || options.Count == 0)
            return;

        float dt = Time.deltaTime;

        // The hold survives short moments where the pointer is outside the option
        if (holdingIndex >= 0)
        {
            if (hoveredIndex == holdingIndex) graceTimer = holdExitGraceDuration;
            else if ((graceTimer -= dt) <= 0f) holdingIndex = -1;
        }

        SetHoldProgress(holdingIndex >= 0
            ? (holdDuration <= 0f ? 1f : Mathf.Min(1f, holdProgress + dt / holdDuration))
            : (fillReleaseDuration <= 0f ? 0f : Mathf.MoveTowards(holdProgress, 0f, dt / fillReleaseDuration)));

        SyncHoldFeedback();

        if (holdingIndex >= 0 && holdProgress >= 1f)
        {
            int index = holdingIndex;
            holdingIndex = -1;
            OnOptionConfirmed?.Invoke(index);
        }
    }

    // ---------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------

    /// <summary>Builds the pie menu: the empty tag in the center and one option per available tag around it.</summary>
    public void Initialize(TagData[] availableTags, TagData emptyTagData)
    {
        if (itemsContainer == null || itemPrefab == null)
        {
            Debug.LogWarning("TagCarouselDisplay: Items Container or Item Prefab is not assigned.", this);
            return;
        }

        KillIncorrectTimer();
        ClearAll();

        tags = availableTags;
        emptyData = emptyTagData;
        state = State.Idle;
        holdingIndex = hoveredIndex = -1;

        BuildCenter();
        BuildOptions();
        ShowOptions(false);
    }

    /// <summary>Shows the given tag in the center, hides the options and locks the pie menu.</summary>
    public void ShowCorrectFeedback(int availableIndex)
    {
        EnterResult(State.Validated, availableIndex, tintCenterOnCorrect, correctFeedbackColor);
    }

    /// <summary>Shows the given tag in red in the center, hides the options, then resets the pie menu after a delay.</summary>
    public void ShowIncorrectFeedback(int availableIndex)
    {
        EnterResult(State.Incorrect, availableIndex, true, incorrectFeedbackColor);
        incorrectTimer = DOVirtual.DelayedCall(incorrectDisplayDuration, OnIncorrectFinished);
    }

    /// <summary>Locks the pie menu on the validated tag, or unlocks it and returns to the initial state.</summary>
    public void SetValidated(bool validated, int availableIndex = -1)
    {
        if (validated) ShowCorrectFeedback(availableIndex);
        else if (state != State.Idle) ResetToInitial(true);
    }

    // ---------------------------------------------------------------------
    // Build
    // ---------------------------------------------------------------------

    void ClearAll()
    {
        foreach (OptionItem option in options)
        {
            if (option.Rect != null)
            {
                option.Rect.DOKill();
                option.Group.DOKill();
                option.Colors.Kill();
                Destroy(option.Rect.gameObject);
            }

            if (option.HitImage != null) Destroy(option.HitImage.gameObject);
        }

        options.Clear();

        if (centerRect != null)
        {
            centerRect.DOKill();
            Destroy(centerInstance);
        }

        centerInstance = null;
        centerRect = null;
        centerDisplay = null;
        holdFill = null;
        heldVisualIndex = -1;
    }

    // Creates an item that never receives raycasts, with centered anchors and a TagDisplay.
    RectTransform CreateItem(GameObject prefab, Transform parent, out TagDisplay display)
    {
        GameObject instance = Instantiate(prefab, parent);
        RectTransform rect = instance.GetComponent<RectTransform>();
        Vector2 center = new Vector2(0.5f, 0.5f);

        if (rect.anchorMin != center || rect.anchorMax != center)
        {
            Vector2 size = rect.rect.size;
            rect.anchorMin = rect.anchorMax = center;
            rect.sizeDelta = size;
        }

        display = instance.GetComponent<TagDisplay>();
        if (display == null) display = instance.AddComponent<TagDisplay>();

        foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }

        return rect;
    }

    void BuildCenter()
    {
        centerRect = CreateItem(centerPrefab != null ? centerPrefab : itemPrefab,
            centerContainer != null ? centerContainer : itemsContainer, out centerDisplay);
        centerInstance = centerRect.gameObject;
        centerRect.anchoredPosition = Vector2.zero;
        centerBaseScale = centerRect.localScale;

        // The fill is assigned on the TagDisplay of the center prefab
        holdFill = centerDisplay.HoldFill;
        if (holdFill != null) holdFill.type = Image.Type.Filled;
        else Debug.LogWarning("TagCarouselDisplay: no Hold Fill assigned on the TagDisplay of the center prefab.", this);

        SetHoldProgress(0f);
        SetCenter(emptyData);
    }

    void BuildOptions()
    {
        int count = tags != null ? tags.Length : 0;

        for (int i = 0; i < count; i++)
        {
            RectTransform rect = CreateItem(itemPrefab, itemsContainer, out TagDisplay display);
            rect.gameObject.SetActive(true);
            display.SetData(tags[i]);

            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();

            float angle = (startAngle - i * 360f / count) * Mathf.Deg2Rad;

            OptionItem option = new OptionItem
            {
                Rect = rect,
                Group = group,
                BaseScale = rect.localScale,
                BasePos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius
            };

            option.Colors.Cache(rect);
            options.Add(option);
        }

        // The hit zones are created last so they sit on top of every visual
        for (int i = 0; i < options.Count; i++)
        {
            CreateHitZone(options[i], i);
        }
    }

    // A fixed Button + Image that never scales or moves, so detection stays stable while the visuals animate.
    void CreateHitZone(OptionItem option, int index)
    {
        GameObject zone = new GameObject("HitZone_" + index, typeof(RectTransform), typeof(Image), typeof(Button), typeof(OptionRelay));
        zone.transform.SetParent(itemsContainer, false);

        Vector2 size = option.Rect.rect.size;
        if (size.x <= 0f || size.y <= 0f) size = fallbackHitZoneSize;

        // Visual center of the option, in case its pivot is not centered
        Vector2 pivotOffset = Vector2.Scale(new Vector2(0.5f, 0.5f) - option.Rect.pivot, size);

        RectTransform rect = zone.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size + Vector2.one * (Mathf.Abs(hitAreaPadding) * 2f);
        rect.anchoredPosition = option.BasePos + pivotOffset;

        option.HitImage = zone.GetComponent<Image>();
        option.HitImage.color = hitZoneColor;

        // Keeps the zone raycastable even with a very low alpha
        option.HitImage.canvasRenderer.cullTransparentMesh = false;

        option.HitButton = zone.GetComponent<Button>();
        option.HitButton.transition = Selectable.Transition.None;
        option.HitButton.targetGraphic = option.HitImage;

        OptionRelay relay = zone.GetComponent<OptionRelay>();
        relay.Owner = this;
        relay.Index = index;
    }

    // ---------------------------------------------------------------------
    // Pointer handling (called by the hit zones)
    // ---------------------------------------------------------------------

    void OnOptionEnter(int index)
    {
        if (state != State.Idle || hoveredIndex == index)
            return;

        hoveredIndex = index;
        ApplyHoverStyles(true);

        if (hoverSFX != null)
        {
            GetSource(ref hoverAudioSource).PlayOneShot(hoverSFX, hoverSFXVolume);
        }
    }

    void OnOptionExit(int index)
    {
        if (hoveredIndex == index)
        {
            hoveredIndex = -1;
            if (state == State.Idle) ApplyHoverStyles(true);
        }

        // Otherwise the hold is cancelled by Update once the grace duration is over
        if (holdExitGraceDuration <= 0f && holdingIndex == index)
        {
            holdingIndex = -1;
        }
    }

    void OnOptionDown(int index)
    {
        if (state != State.Idle)
            return;

        // A press means the pointer is on the option, even if the enter event was missed
        if (hoveredIndex != index)
        {
            hoveredIndex = index;
            ApplyHoverStyles(true);
        }

        // Every new press restarts the fill from zero
        SetHoldProgress(0f);
        holdingIndex = index;
        graceTimer = holdExitGraceDuration;
    }

    void OnOptionUp(int index)
    {
        if (holdingIndex == index) holdingIndex = -1;
    }

    // ---------------------------------------------------------------------
    // Options visuals
    // ---------------------------------------------------------------------

    // The hovered option grows, the others shrink, fade and move toward the center.
    void ApplyHoverStyles(bool animate)
    {
        for (int i = 0; i < options.Count; i++)
        {
            OptionItem option = options[i];
            if (option.Rect == null)
                continue;

            bool hovered = i == hoveredIndex;
            bool dimmed = hoveredIndex >= 0 && !hovered;

            Vector3 scale = option.BaseScale * (hovered ? hoveredScale : dimmed ? shrunkScale : 1f);
            Vector2 position = dimmed ? option.BasePos * unhoveredRadiusScale : option.BasePos;
            float alpha = dimmed ? unhoveredAlpha : 1f;

            option.Rect.DOKill();
            option.Group.DOKill();

            if (animate)
            {
                option.Rect.DOScale(scale, hoverDuration).SetEase(hoverEase);
                option.Rect.DOAnchorPos(position, hoverDuration).SetEase(hoverEase);
                option.Group.DOFade(alpha, hoverDuration).SetEase(hoverEase);
            }
            else
            {
                option.Rect.localScale = scale;
                option.Rect.anchoredPosition = position;
                option.Group.alpha = alpha;
            }
        }
    }

    void SetHitActive(OptionItem option, bool active)
    {
        option.HitImage.raycastTarget = active;
        option.HitButton.interactable = active;
    }

    void ShowOptions(bool animate)
    {
        for (int i = 0; i < options.Count; i++)
        {
            OptionItem option = options[i];
            option.Rect.DOKill();
            option.Group.DOKill();
            option.Rect.anchoredPosition = option.BasePos;
            SetHitActive(option, true);

            if (animate)
            {
                float delay = i * optionsShowStagger;
                option.Rect.localScale = Vector3.zero;
                option.Group.alpha = 0f;
                option.Rect.DOScale(option.BaseScale, optionsShowDuration).SetEase(Ease.OutBack).SetDelay(delay);
                option.Group.DOFade(1f, optionsShowDuration).SetDelay(delay);
            }
            else
            {
                option.Rect.localScale = option.BaseScale;
                option.Group.alpha = 1f;
            }
        }
    }

    void HideOptions()
    {
        foreach (OptionItem option in options)
        {
            option.Rect.DOKill();
            option.Group.DOKill();
            SetHitActive(option, false);
            option.Rect.DOScale(0f, optionsHideDuration).SetEase(Ease.InBack);
            option.Group.DOFade(0f, optionsHideDuration);
        }
    }

    // ---------------------------------------------------------------------
    // Hold feedback: fill, held option color and sound
    // ---------------------------------------------------------------------

    void SetHoldProgress(float progress)
    {
        holdProgress = progress;
        if (holdFill != null) holdFill.fillAmount = progress;
    }

    // Keeps the held option color and the hold sound in sync with the current hold.
    void SyncHoldFeedback()
    {
        if (holdingIndex != heldVisualIndex)
        {
            TintHeldOption(heldVisualIndex, false, heldTintDuration);
            heldVisualIndex = holdingIndex;
            TintHeldOption(heldVisualIndex, true, heldTintDuration);
        }

        if (holdLoopSFX == null)
            return;

        if (holdingIndex < 0)
        {
            StopHoldSound();
            return;
        }

        AudioSource source = GetSource(ref holdAudioSource);

        if (!source.isPlaying)
        {
            source.clip = holdLoopSFX;
            source.loop = true;
            source.volume = holdSFXVolume;
            source.Play();
        }

        // The pitch rises as the fill progresses
        source.pitch = Mathf.Lerp(holdPitchStart, holdPitchEnd, holdProgress);
    }

    void StopHoldFeedback()
    {
        TintHeldOption(heldVisualIndex, false, 0f);
        heldVisualIndex = -1;
        StopHoldSound();
    }

    void StopHoldSound()
    {
        if (holdAudioSource != null && holdAudioSource.isPlaying) holdAudioSource.Stop();
    }

    void TintHeldOption(int index, bool held, float duration)
    {
        if (index < 0 || index >= options.Count)
            return;

        GraphicSet colors = options[index].Colors;

        if (held) colors.Apply(heldOptionColor, tintHeldText, tintHeldGraphics, heldTintStrength, duration);
        else colors.Restore(duration);
    }

    AudioSource GetSource(ref AudioSource source)
    {
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
        }

        return source;
    }

    // ---------------------------------------------------------------------
    // Center display and results
    // ---------------------------------------------------------------------

    // Shows the tag in the center with its original colors. A null tag hides the center.
    void SetCenter(TagData data)
    {
        centerColors.Restore();
        centerDisplay.SetData(data);
        centerColors.Cache(centerRect, holdFill);
        centerInstance.SetActive(data != null);
    }

    void ShowInCenter(int index, bool tint, Color color)
    {
        if (index >= 0 && tags != null && index < tags.Length && tags[index] != null) SetCenter(tags[index]);
        else centerColors.Restore();

        centerInstance.SetActive(true);

        if (tint)
        {
            centerColors.Apply(color, tintCenterText, tintCenterGraphics, feedbackTintStrength, 0f);
        }

        centerRect.DOKill();
        centerRect.localScale = centerBaseScale;

        if (feedbackPunchDuration > 0f)
        {
            centerRect.DOPunchScale(centerBaseScale * (feedbackPunchScale - 1f), feedbackPunchDuration, 1, 0.5f);
        }
    }

    void EnterResult(State newState, int index, bool tint, Color color)
    {
        KillIncorrectTimer();

        state = newState;
        holdingIndex = hoveredIndex = -1;
        SetHoldProgress(0f);
        StopHoldFeedback();

        if (centerInstance != null) ShowInCenter(index, tint, color);
        HideOptions();
    }

    void OnIncorrectFinished()
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
        holdingIndex = hoveredIndex = -1;
        SetHoldProgress(0f);
        StopHoldFeedback();

        if (centerRect != null)
        {
            centerRect.DOKill();
            centerRect.localScale = centerBaseScale;
            SetCenter(emptyData);
        }

        ShowOptions(animate);
    }

    void KillIncorrectTimer()
    {
        incorrectTimer?.Kill();
        incorrectTimer = null;
    }
}

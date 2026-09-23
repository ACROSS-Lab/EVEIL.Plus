using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;

public class TagCarouselDisplay : MonoBehaviour
{
    // Relays standard UI pointer events into hover actions for buttons.
    class ButtonHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action OnEnter;
        public Action OnExit;

        public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke();
    }

    class CarouselItem
    {
        public RectTransform Rect;
        public Image Icon;
        public CanvasGroup CanvasGroup;
    }

    [Header("Layout")]
    [SerializeField] RectTransform itemsContainer;
    [SerializeField] GameObject itemPrefab;

    [Tooltip("Vertical distance between previous, current, and next tags.")]
    [SerializeField] float itemSpacing = 120f;

    [Header("Slide Animation")]
    [SerializeField] float slideDuration = 0.25f;
    [SerializeField] Ease slideEase = Ease.OutCubic;

    [Header("Focus Styling")]
    [Tooltip("Scale applied to the centered (currently selected) item.")]
    [SerializeField] float focusedScale = 1.2f;

    [Tooltip("Scale applied to items above and below the center.")]
    [SerializeField] float unfocusedScale = 0.8f;

    [Tooltip("Alpha applied to items above and below the center.")]
    [SerializeField] float unfocusedAlpha = 0.5f;

    [Header("Tag Cycling Button")]
    [Tooltip("Button on the items container used to cycle tags.")]
    [SerializeField] Button itemsContainerButton;
    [Tooltip("Background image of the items container button to tint on hover.")]
    [SerializeField] Image itemsContainerBackground;
    [Tooltip("Hover color tint applied to the items container button.")]
    [SerializeField] Color itemsContainerHoverColor = new Color(1f, 1f, 0.8f, 1f);
    [Tooltip("Punch scale animation played on the items container button when clicked.")]
    [SerializeField] float itemsContainerPunchScale = 1.05f;
    [SerializeField] float itemsContainerPunchDuration = 0.15f;

    [Header("Validation")]
    [SerializeField] GameObject validateButtonRoot;
    [SerializeField] Image validateBackground;
    [Tooltip("Hidden element in prefab, enabled once tag is validated.")]
    [SerializeField] GameObject validatedFeedbackRoot;

    [Header("Validation Localization")]
    [SerializeField] LocalizedKey validateButtonLocalizedKey;
    [SerializeField] string validateLocalizationKey = "tag_validate";
    [SerializeField] string changeLocalizationKey = "tag_change";

    [Header("Validation Colors")]
    [SerializeField] Color validateColor = new Color(0.35f, 1f, 0.5f, 0.9f);
    [SerializeField] Color changeColor = new Color(1f, 0.85f, 0.35f, 0.9f);
    [Tooltip("Color applied while hovering the button; takes precedence over Validate/Change colors.")]
    [SerializeField] Color validateHoverColor = new Color(1f, 1f, 1f, 1f);

    [Header("Validation Animation")]
    [Tooltip("Pop duration played when validation feedback appears. Set to 0 to disable.")]
    [SerializeField] float validatedPopDuration = 0.25f;

    [Tooltip("Punch scale animation played on the button when clicked.")]
    [SerializeField] float validatePunchScale = 1.15f;
    [SerializeField] float validatePunchDuration = 0.15f;

    /// <summary>Triggered whenever the validate/unlock button is pressed.</summary>
    public event Action OnValidatePressed;

    /// <summary>Triggered whenever the items container button is clicked to cycle tags.</summary>
    public event Action OnCyclePressed;

    readonly List<CarouselItem> items = new List<CarouselItem>();
    int currentIndex = -1;
    bool hasActiveTagList = false;

    Button validateUIButton;
    Vector3 validatedFeedbackBaseScale = Vector3.one;
    Vector3 validateButtonBaseScale = Vector3.one;
    bool isValidated = false;
    bool isValidateButtonHovered = false;

    Vector3 itemsContainerBaseScale = Vector3.one;
    Color itemsContainerNormalColor = Color.white;
    bool isItemContainerHovered = false;

    void Awake()
    {
        SetupValidationUI();
        SetupCycleButton();
    }

    void OnDestroy()
    {
        validateUIButton.onClick.RemoveListener(HandleValidateClicked);
        itemsContainerButton.onClick.RemoveListener(HandleItemsContainerClicked);
    }

    void SetupCycleButton()
    {
        itemsContainerBaseScale = itemsContainerButton.transform.localScale;
        itemsContainerNormalColor = itemsContainerBackground.color;

        itemsContainerButton.onClick.AddListener(HandleItemsContainerClicked);

        ButtonHoverRelay hoverRelay = itemsContainerButton.gameObject.AddComponent<ButtonHoverRelay>();

        hoverRelay.OnEnter = () => SetItemContainerHovered(true);
        hoverRelay.OnExit = () => SetItemContainerHovered(false);
    }

    void HandleItemsContainerClicked()
    {
        PlayItemsContainerPunch();
        OnCyclePressed?.Invoke();
    }

    void PlayItemsContainerPunch()
    {
        if (itemsContainerButton == null || itemsContainerPunchDuration <= 0f)
            return;

        Transform buttonTransform = itemsContainerButton.transform;
        buttonTransform.DOKill();
        buttonTransform.localScale = itemsContainerBaseScale;

        buttonTransform.DOPunchScale(
            itemsContainerBaseScale * (itemsContainerPunchScale - 1f),
            itemsContainerPunchDuration,
            vibrato: 1,
            elasticity: 0.4f
        );
    }

    void SetItemContainerHovered(bool hovered)
    {
        isItemContainerHovered = hovered;
        RefreshItemContainerVisuals();
    }

    void RefreshItemContainerVisuals()
    {
        if (itemsContainerBackground != null)
        {
            itemsContainerBackground.color = isItemContainerHovered
                ? itemsContainerHoverColor
                : itemsContainerNormalColor;
        }
    }

    void SetupValidationUI()
    {
        if (validatedFeedbackRoot != null)
        {
            validatedFeedbackBaseScale = validatedFeedbackRoot.transform.localScale;
            validatedFeedbackRoot.SetActive(false);
        }

        validateButtonBaseScale = validateButtonRoot.transform.localScale;

        validateUIButton = validateButtonRoot.GetComponent<Button>();
        if (validateUIButton != null)
        {
            validateUIButton.onClick.AddListener(HandleValidateClicked);
            ButtonHoverRelay hoverRelay = validateButtonRoot.AddComponent<ButtonHoverRelay>();

            hoverRelay.OnEnter = () => SetValidateButtonHovered(true);
            hoverRelay.OnExit = () => SetValidateButtonHovered(false);
        }

        validateButtonRoot.SetActive(false);
        RefreshValidateButton();
    }

    void SetValidateButtonHovered(bool hovered)
    {
        isValidateButtonHovered = hovered;
        RefreshValidateButton();
    }

    void HandleValidateClicked()
    {
        PlayValidatePunch();
        OnValidatePressed?.Invoke();
    }

    void PlayValidatePunch()
    {
        if (validateButtonRoot == null || validatePunchDuration <= 0f)
            return;

        Transform buttonTransform = validateButtonRoot.transform;
        buttonTransform.DOKill();
        buttonTransform.localScale = validateButtonBaseScale;

        buttonTransform.DOPunchScale(
            validateButtonBaseScale * (validatePunchScale - 1f),
            validatePunchDuration,
            vibrato: 1,
            elasticity: 0.4f
        );
    }

    /// <summary>
    /// Shows or hides the validate button.
    /// </summary>
    public void SetValidateButtonVisible(bool visible)
    {
        if (validateButtonRoot == null)
            return;

        if (validateButtonRoot.activeSelf != visible)
        {
            validateButtonRoot.SetActive(visible);
        }
    }

    /// <summary>
    /// Toggles the button between "Validate" and "Change tag", and updates validation feedback.
    /// </summary>
    public void SetValidated(bool validated)
    {
        isValidated = validated;

        RefreshValidateButton();
        RefreshValidatedFeedback();
    }

    void RefreshValidateButton()
    {
        if (validateButtonLocalizedKey != null)
        {
            validateButtonLocalizedKey.localizationKey = isValidated
                ? changeLocalizationKey
                : validateLocalizationKey;

            if (validateButtonLocalizedKey.textComponent != null)
            {
                validateButtonLocalizedKey.UpdateText();
            }
        }

        if (validateBackground != null)
        {
            Color targetColor = isValidated ? changeColor : validateColor;

            if (isValidateButtonHovered)
            {
                targetColor = validateHoverColor;
            }

            validateBackground.color = targetColor;
        }
    }

    void RefreshValidatedFeedback()
    {
        if (validatedFeedbackRoot == null)
            return;

        validatedFeedbackRoot.transform.DOKill();

        if (!isValidated)
        {
            validatedFeedbackRoot.transform.localScale = validatedFeedbackBaseScale;
            validatedFeedbackRoot.SetActive(false);
            return;
        }

        validatedFeedbackRoot.SetActive(true);

        if (validatedPopDuration > 0f)
        {
            validatedFeedbackRoot.transform.localScale = Vector3.zero;

            validatedFeedbackRoot.transform
                .DOScale(validatedFeedbackBaseScale, validatedPopDuration)
                .SetEase(Ease.OutBack);
        }
        else
        {
            validatedFeedbackRoot.transform.localScale = validatedFeedbackBaseScale;
        }
    }

    /// <summary>
    /// Initializes the carousel with an empty tag placeholder and all available tags.
    /// </summary>
    public void Initialize(TagData[] availableTags, TagData emptyTagData)
    {
        if (itemsContainer == null || itemPrefab == null)
            return;

        for (int i = itemsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(itemsContainer.GetChild(i).gameObject);
        }

        items.Clear();
        currentIndex = -1;
        hasActiveTagList = false;

        SetValidateButtonVisible(false);
        SetValidated(false);

        List<TagData> allTags = new List<TagData>();

        if (emptyTagData != null)
        {
            allTags.Add(emptyTagData);
        }

        if (availableTags != null)
        {
            allTags.AddRange(availableTags);
        }

        if (allTags.Count == 0)
            return;

        for (int i = 0; i < allTags.Count; i++)
        {
            GameObject itemInstance = Instantiate(itemPrefab, itemsContainer);
            RectTransform itemRect = itemInstance.GetComponent<RectTransform>();
            TagData data = allTags[i];

            TagDisplay tagDisplay = itemInstance.GetComponent<TagDisplay>();
            if (tagDisplay == null)
            {
                tagDisplay = itemInstance.AddComponent<TagDisplay>();
            }

            tagDisplay.SetData(data);

            // Disable raycast target on child item visuals so clicks pass through to itemsContainerButton
            foreach (Graphic graphic in itemInstance.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            items.Add(new CarouselItem
            {
                Rect = itemRect,
                Icon = itemInstance.GetComponentInChildren<Image>(true),
                CanvasGroup = itemInstance.GetComponent<CanvasGroup>()
            });
        }

        SetIndex(0, animate: false);
    }

    public void SetIndex(int index, bool animate = true)
    {
        if (itemsContainer == null || items.Count == 0)
            return;

        if (hasActiveTagList && items.Count > 1)
        {
            int availableCount = items.Count - 1;
            int wrappedAvailableIndex = WrapIndex(index, availableCount);
            currentIndex = 1 + wrappedAvailableIndex;
        }
        else
        {
            currentIndex = WrapIndex(index, items.Count);
        }

        UpdateItemPositions(animate);
    }

    public void EnableAvailableTagsMode()
    {
        hasActiveTagList = true;
    }

    void UpdateItemPositions(bool animate)
    {
        for (int i = 0; i < items.Count; i++)
        {
            CarouselItem item = items[i];
            if (item.Rect == null) continue;

            if (hasActiveTagList && i == 0)
            {
                item.Rect.gameObject.SetActive(false);
                continue;
            }

            item.Rect.gameObject.SetActive(true);

            int relativeIndex;
            if (hasActiveTagList && items.Count > 1)
            {
                int itemOffset = i - 1;
                int centerOffset = currentIndex - 1;
                relativeIndex = GetCircularDistance(itemOffset, centerOffset, items.Count - 1);
            }
            else
            {
                relativeIndex = GetCircularDistance(i, currentIndex, items.Count);
            }

            bool isFocused = relativeIndex == 0;
            bool isAdjacent = relativeIndex == -1 || relativeIndex == 1;

            float targetY = -relativeIndex * itemSpacing;
            float targetScale = isFocused ? focusedScale : unfocusedScale;
            float targetAlpha = isFocused ? 1f : (isAdjacent ? unfocusedAlpha : 0f);

            item.Rect.DOKill();

            if (animate)
            {
                item.Rect.DOAnchorPosY(targetY, slideDuration).SetEase(slideEase);
                item.Rect.DOScale(targetScale, slideDuration).SetEase(slideEase);
            }
            else
            {
                item.Rect.anchoredPosition = new Vector2(item.Rect.anchoredPosition.x, targetY);
                item.Rect.localScale = Vector3.one * targetScale;
            }

            UpdateItemAlpha(item, targetAlpha, animate);
        }
    }

    int GetCircularDistance(int index, int centerIndex, int count)
    {
        int distance = index - centerIndex;
        if (distance > count / 2) distance -= count;
        else if (distance < -count / 2) distance += count;
        return distance;
    }

    int WrapIndex(int index, int count)
    {
        if (count <= 0) return -1;
        return ((index % count) + count) % count;
    }

    void UpdateItemAlpha(CarouselItem item, float targetAlpha, bool animate)
    {
        if (item.CanvasGroup != null)
        {
            item.CanvasGroup.DOKill();
            if (animate) item.CanvasGroup.DOFade(targetAlpha, slideDuration).SetEase(slideEase);
            else item.CanvasGroup.alpha = targetAlpha;
            return;
        }

        if (item.Icon != null)
        {
            item.Icon.DOKill();
            if (animate) item.Icon.DOFade(targetAlpha, slideDuration).SetEase(slideEase);
            else
            {
                Color color = item.Icon.color;
                color.a = targetAlpha;
                item.Icon.color = color;
            }
        }
    }
}

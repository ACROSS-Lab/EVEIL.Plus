using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.EventSystems;

public class TagCarouselDisplay : MonoBehaviour
{
    // Le Button UI ne déclenche pas hoverEntered/hoverExited comme un XRSimpleInteractable.
    // Ce petit relais traduit les événements de pointeur standard en survol pour le bouton.
    private class ButtonHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action OnEnter;
        public Action OnExit;

        public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke();
    }

    private class CarouselItem
    {
        public RectTransform Rect;
        public Image Icon;
        public CanvasGroup CanvasGroup;
    }

    [Header("Layout")]
    [SerializeField] RectTransform itemsContainer;
    [SerializeField] GameObject itemPrefab;

    [Tooltip("Vertical distance between the previous, current and next tags.")]
    [SerializeField] float itemSpacing = 120f;

    [Header("Slide Animation")]
    [SerializeField] float slideDuration = 0.25f;
    [SerializeField] Ease slideEase = Ease.OutCubic;

    [Header("Focus Styling")]
    [Tooltip("Scale applied to the centered (currently selected) item.")]
    [SerializeField] float focusedScale = 1.2f;

    [Tooltip("Scale applied to the items above/below the center.")]
    [SerializeField] float unfocusedScale = 0.8f;

    [Tooltip("Alpha applied to the items above/below the center.")]
    [SerializeField] float unfocusedAlpha = 0.5f;

    [Header("Validation")]
    [Tooltip("Bouton placé à côté du carrousel. Désactivé tant qu'aucun tag n'a été choisi.")]
    [SerializeField] GameObject validateButtonRoot;

    [Tooltip("Label du bouton. Si vide, le premier TextMeshProUGUI du bouton est utilisé.")]
    [SerializeField] TextMeshProUGUI validateLabel;

    [Tooltip("Fond du bouton, teinté selon l'état. Optionnel.")]
    [SerializeField] Image validateBackground;

    [Tooltip("Élément caché du prefab, activé une fois le tag validé.")]
    [SerializeField] GameObject validatedFeedbackRoot;

    [Header("Validation Localization")]
    [SerializeField] string validateLocalizationKey = "tag_validate";
    [SerializeField] string changeLocalizationKey = "tag_change";

    [Header("Validation Colors")]
    [SerializeField] Color validateColor = new Color(0.35f, 1f, 0.5f, 0.9f);
    [SerializeField] Color changeColor = new Color(1f, 0.85f, 0.35f, 0.9f);
    [Tooltip("Couleur appliquée pendant le survol du bouton, prioritaire sur Valider/Changer.")]
    [SerializeField] Color validateHoverColor = new Color(1f, 1f, 1f, 1f);

    [Header("Validation Animation")]
    [Tooltip("Rebond joué à l'apparition du feedback de validation. Mettre 0 pour désactiver.")]
    [SerializeField] float validatedPopDuration = 0.25f;

    [Tooltip("Rebond joué sur le bouton lui-même à chaque pression, pour confirmer le clic.")]
    [SerializeField] float validatePunchScale = 1.15f;
    [SerializeField] float validatePunchDuration = 0.15f;

    /// <summary>Déclenché à chaque pression du bouton, pour valider comme pour déverrouiller.</summary>
    public event Action OnValidatePressed;

    private readonly List<CarouselItem> items = new List<CarouselItem>();
    private int currentIndex = -1;
    private bool hasActiveTagList = false;

    private LocalizedKey validateLocalizedKey;
    private XRSimpleInteractable validateInteractable;
    private Button validateUIButton;
    private Vector3 validatedFeedbackBaseScale = Vector3.one;
    private Vector3 validateButtonBaseScale = Vector3.one;
    private bool isValidated = false;
    private bool isButtonHovered = false;

    private void Awake()
    {
        SetupValidationUI();
    }

    private void OnDestroy()
    {
        if (validateInteractable != null)
        {
            validateInteractable.selectEntered.RemoveListener(HandleValidateSelected);
            validateInteractable.hoverEntered.RemoveListener(HandleValidateHoverEntered);
            validateInteractable.hoverExited.RemoveListener(HandleValidateHoverExited);
        }

        if (validateUIButton != null)
        {
            validateUIButton.onClick.RemoveListener(HandleValidateClicked);
        }
    }

    // Le bouton et le feedback vivent dans ce prefab, on ne fait que les activer et les désactiver.
    private void SetupValidationUI()
    {
        if (validatedFeedbackRoot != null)
        {
            validatedFeedbackBaseScale = validatedFeedbackRoot.transform.localScale;
            validatedFeedbackRoot.SetActive(false);
        }

        if (validateButtonRoot == null)
            return;

        if (validateLabel == null)
        {
            validateLabel = validateButtonRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (validateBackground == null)
        {
            validateBackground = validateButtonRoot.GetComponent<Image>();
        }

        validateLocalizedKey = validateButtonRoot.GetComponent<LocalizedKey>();

        if (validateLocalizedKey == null)
        {
            validateLocalizedKey = validateButtonRoot.AddComponent<LocalizedKey>();
        }

        validateLocalizedKey.textComponent = validateLabel;
        validateButtonBaseScale = validateButtonRoot.transform.localScale;

        validateInteractable = validateButtonRoot.GetComponent<XRSimpleInteractable>();

        if (validateInteractable != null)
        {
            validateInteractable.selectEntered.AddListener(HandleValidateSelected);
            validateInteractable.hoverEntered.AddListener(HandleValidateHoverEntered);
            validateInteractable.hoverExited.AddListener(HandleValidateHoverExited);
        }

        validateUIButton = validateButtonRoot.GetComponent<Button>();

        if (validateUIButton != null)
        {
            validateUIButton.onClick.AddListener(HandleValidateClicked);

            ButtonHoverRelay hoverRelay = validateButtonRoot.GetComponent<ButtonHoverRelay>();

            if (hoverRelay == null)
            {
                hoverRelay = validateButtonRoot.AddComponent<ButtonHoverRelay>();
            }

            hoverRelay.OnEnter = () => SetButtonHovered(true);
            hoverRelay.OnExit = () => SetButtonHovered(false);
        }

        validateButtonRoot.SetActive(false);
        RefreshValidateButton();
    }

    private void HandleValidateSelected(SelectEnterEventArgs args)
    {
        HandleValidateClicked();
    }

    private void HandleValidateHoverEntered(HoverEnterEventArgs args)
    {
        SetButtonHovered(true);
    }

    private void HandleValidateHoverExited(HoverExitEventArgs args)
    {
        SetButtonHovered(false);
    }

    private void SetButtonHovered(bool hovered)
    {
        isButtonHovered = hovered;
        RefreshValidateButton();
    }

    private void HandleValidateClicked()
    {
        PlayValidatePunch();
        OnValidatePressed?.Invoke();
    }

    // Petit rebond sur le bouton lui-même, pour confirmer que le clic a bien été pris en compte.
    private void PlayValidatePunch()
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
    /// Affiche ou masque le bouton. À appeler dès que le joueur a choisi un premier tag.
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
    /// Bascule le bouton entre "Valider" et "Changer de tag",
    /// et active ou désactive l'élément de feedback du prefab.
    /// </summary>
    public void SetValidated(bool validated)
    {
        isValidated = validated;

        RefreshValidateButton();
        RefreshValidatedFeedback();
    }

    private void RefreshValidateButton()
    {
        if (validateLocalizedKey != null)
        {
            validateLocalizedKey.localizationKey = isValidated
                ? changeLocalizationKey
                : validateLocalizationKey;

            if (validateLocalizedKey.textComponent != null)
            {
                validateLocalizedKey.UpdateText();
            }
        }

        if (validateBackground != null)
        {
            Color targetColor = isValidated ? changeColor : validateColor;

            // Le survol prime toujours, pour que le joueur voie ce qu'il cible
            if (isButtonHovered)
            {
                targetColor = validateHoverColor;
            }

            validateBackground.color = targetColor;
        }
    }

    private void RefreshValidatedFeedback()
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

    // Initialise le carrousel avec le empty tag et tous les tags disponibles.
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

            items.Add(new CarouselItem
            {
                Rect = itemRect,
                Icon = itemInstance.GetComponentInChildren<Image>(true),
                CanvasGroup = itemInstance.GetComponent<CanvasGroup>()
            });
        }

        // Affiche le premier élément (Empty Tag) immédiatement sans animation
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

    private void UpdateItemPositions(bool animate)
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

    private int GetCircularDistance(int index, int centerIndex, int count)
    {
        int distance = index - centerIndex;
        if (distance > count / 2) distance -= count;
        else if (distance < -count / 2) distance += count;
        return distance;
    }

    private int WrapIndex(int index, int count)
    {
        if (count <= 0) return -1;
        return ((index % count) + count) % count;
    }

    private void UpdateItemAlpha(CarouselItem item, float targetAlpha, bool animate)
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class TagCarouselDisplay : MonoBehaviour
{
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

    private readonly List<CarouselItem> items = new List<CarouselItem>();
    private int currentIndex = -1;
    private bool hasActiveTagList = false;

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
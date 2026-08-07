using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class PollutionGameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private LocalizedKey remainingText;

    [Header("Validation")]
    [SerializeField] private GameObject validationButton;
    [SerializeField] private GameObject validationPanel;
    [SerializeField] private LocalizedKey errorText;

    [Header("Events")]
    public UnityEvent onAllTagged;
    public UnityEvent onCompleted;

    private PointOfInterest[] points;

    private void Awake()
    {
        points = FindObjectsByType<PointOfInterest>(
            FindObjectsSortMode.None
        );

        foreach (PointOfInterest point in points)
        {
            point.OnTagChanged += OnPointChanged;
        }

        // Hide the validation panel and button at the beginning.
        if (validationPanel != null)
            validationPanel.SetActive(false);

        if (validationButton != null)
            validationButton.SetActive(false);

        UpdateRemainingUI();
    }

    private void OnDestroy()
    {
        foreach (PointOfInterest point in points)
        {
            point.OnTagChanged -= OnPointChanged;
        }
    }

    private void OnPointChanged(PointOfInterest point)
    {
        UpdateRemainingUI();

        // The validation button only becomes available
        // once every source has been tagged at least once.
        if (RemainingCount() == 0)
        {
            if (validationButton != null)
                validationButton.SetActive(true);

            onAllTagged?.Invoke();
        }
    }

    private void UpdateRemainingUI()
    {
        if (remainingText != null)
        {
            remainingText.SetFormatArguments(
                RemainingCount()
            );
        }
    }

    public int RemainingCount()
    {
        int remaining = 0;

        foreach (PointOfInterest point in points)
        {
            if (!point.HasTag)
                remaining++;
        }

        return remaining;
    }

    public int ErrorCount()
    {
        int errors = 0;

        foreach (PointOfInterest point in points)
        {
            if (!point.IsCorrect)
                errors++;
        }

        return errors;
    }

    public void ValidateAnswers()
    {
        // Do not allow validation before every source has been tagged.
        if (RemainingCount() > 0)
            return;

        int errors = ErrorCount();

        // Show the validation result only after the player
        // explicitly presses the validation button.
        if (validationPanel != null)
            validationPanel.SetActive(true);

        if (errorText != null)
        {
            errorText.SetFormatArguments(errors);
        }

        // All answers are correct.
        if (errors == 0)
        {
            onCompleted?.Invoke();
        }
    }

    public void CloseValidationPanel()
    {
        if (validationPanel != null)
            validationPanel.SetActive(false);
    }
}
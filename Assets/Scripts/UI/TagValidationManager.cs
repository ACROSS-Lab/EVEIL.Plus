using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Suit l'avancement des tags de la scène.
/// Tant que tous les tags ne sont pas validés, le joueur reste libre de les modifier
/// via le bouton "Changer de tag" de chaque Point Of Interest.
/// </summary>
public class TagValidationManager : MonoBehaviour
{
    [Tooltip("Laisser vide pour récupérer automatiquement tous les POI de la scène au démarrage.")]
    [SerializeField] List<PointOfInterest> pointsOfInterest = new List<PointOfInterest>();

    /// <summary>Nombre de tags validés, nombre total attendu.</summary>
    public event Action<int, int> OnProgressChanged;

    /// <summary>Tous les tags sont posés et validés.</summary>
    public event Action<int, int> OnAllTagsValidated;

    public int TotalCount => pointsOfInterest.Count;

    public int ValidatedCount
    {
        get
        {
            int count = 0;

            foreach (PointOfInterest poi in pointsOfInterest)
            {
                if (poi != null && poi.IsTagValidated)
                    count++;
            }

            return count;
        }
    }

    public int CorrectCount
    {
        get
        {
            int count = 0;

            foreach (PointOfInterest poi in pointsOfInterest)
            {
                if (poi != null && poi.IsCorrect)
                    count++;
            }

            return count;
        }
    }

    private void Awake()
    {
        if (pointsOfInterest.Count == 0)
        {
            pointsOfInterest.AddRange(FindObjectsByType<PointOfInterest>(FindObjectsSortMode.None));
        }
    }

    private void OnEnable()
    {
        foreach (PointOfInterest poi in pointsOfInterest)
        {
            if (poi == null)
                continue;

            poi.OnTagValidated += HandleValidationChanged;
            poi.OnTagValidationCancelled += HandleValidationChanged;
        }
    }

    private void OnDisable()
    {
        foreach (PointOfInterest poi in pointsOfInterest)
        {
            if (poi == null)
                continue;

            poi.OnTagValidated -= HandleValidationChanged;
            poi.OnTagValidationCancelled -= HandleValidationChanged;
        }
    }

    private void HandleValidationChanged(PointOfInterest poi)
    {
        int validated = ValidatedCount;

        OnProgressChanged?.Invoke(validated, TotalCount);

        if (validated >= TotalCount && TotalCount > 0)
        {
            OnAllTagsValidated?.Invoke(CorrectCount, TotalCount);
        }
    }
}

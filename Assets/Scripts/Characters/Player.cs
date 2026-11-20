using System.Collections;
using DG.Tweening;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("Fading Setup")]
    [SerializeField] CanvasGroup fadeCanvasGroup;
    [SerializeField] float fadeDuration = 0.5f;
    [SerializeField] bool hasFadeOnStart = true;

    [Header("Raycast Line Setup")]
    [SerializeField] Transform raycastLine;
    [SerializeField] Vector3 defaultPosition;
    [SerializeField] Vector3 detectorPosition; 

    void Start()
    {
        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = FindFirstObjectByType<SceneTransition>().GetComponent<CanvasGroup>();
        }

        raycastLine.localPosition = detectorPosition;
    }

    public void MovePlayer(Vector3 position, bool hasRotation, Vector3 rotation)
    {
        StartCoroutine(MovePlayerCoroutine(position, hasRotation, rotation));
    }

    IEnumerator MovePlayerCoroutine(Vector3 position, bool hasRotation, Vector3 rotation)
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true; 
            yield return fadeCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();
        }

        transform.position = position;
        if (hasRotation) transform.rotation = Quaternion.Euler(rotation);

        yield return new WaitForSeconds(1f);
        
        if (fadeCanvasGroup != null)
        {
            yield return fadeCanvasGroup.DOFade(0f, fadeDuration).WaitForCompletion();
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    public void SetRaycastLinePositionToDetector(bool isDetector)
    {
        raycastLine.localPosition = isDetector ? detectorPosition : defaultPosition;
    }
}
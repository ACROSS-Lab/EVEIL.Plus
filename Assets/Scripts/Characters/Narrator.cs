using DG.Tweening;
using TMPro;
using UnityEngine;

public class Narrator : MonoBehaviour 
{
    [SerializeField] Animator animator;
    [SerializeField] LocalizedKey localizedKey;
    [SerializeField] AudioSource audioSource;

    [Header("Dialogue Options")]
    [SerializeField] GameObject dialogueBoxes;
    [SerializeField] GameObject overlayCanvas, nestedCanvas;
    [SerializeField] TextMeshProUGUI overlayText, nestedText;
    // [SerializeField] float smoothTurn = 10f;
    
    public bool IsTalking { get; private set; }

    public Tween Move(Vector3 targetPosition, Vector3 offsetAtCenter, bool hasRotation, Vector3 targetRotation, float targetScale, float flyDuration)
    {
        Vector3 midPoint = Vector3.Lerp(transform.position, targetPosition, 0.5f) + offsetAtCenter;
        Vector3[] path = {transform.position, midPoint, targetPosition};

        Sequence action = DOTween.Sequence();

        action.Join(transform.DOPath(path, flyDuration, PathType.CatmullRom).SetEase(Ease.InOutSine));
        action.Join(transform.DOScale(targetScale, flyDuration).SetEase(Ease.InOutSine));
        if (hasRotation) action.Join(transform.DORotate(targetRotation, flyDuration).SetEase(Ease.InOutSine));

        return action;
    }

    public void StopMovement()
    {
        DOTween.Kill(transform);
    }

    public void SnapMovement(Vector3 targetPosition, bool hasRotation, Vector3 targetRotation, float targetScale)
    {
        DOTween.Kill(transform);
        transform.position = targetPosition;
        if (hasRotation)
        {
            transform.eulerAngles = targetRotation;
        }
        if (targetScale > 0f)
        {
            transform.localScale = Vector3.one * targetScale;
        }
    }

    public float StartTalking(string key, int bodyStartState, int eyesState, int mouthStartState, bool isUsingOverlay)
    {
        IsTalking = true;
        
        if (dialogueBoxes != null) dialogueBoxes.SetActive(true);

        localizedKey.localizationKey = key;

        if (isUsingOverlay)
        {
            if (overlayCanvas != null) overlayCanvas.SetActive(true);
            if (nestedCanvas != null) nestedCanvas.SetActive(false);
            localizedKey.textComponent = overlayText;
        }
        else
        {
            if (overlayCanvas != null) overlayCanvas.SetActive(false);
            if (nestedCanvas != null)
            {
                nestedCanvas.SetActive(true);
                nestedCanvas.GetComponent<TrajectoryCanvasFollower>()?.SetPosition();
            }
            localizedKey.textComponent = nestedText;
        }

        localizedKey.UpdateText();
        localizedKey.UpdateAudioClip();

        if (animator != null)
        {
            animator.SetInteger("BodyState", bodyStartState);
            animator.SetInteger("EyesState", eyesState);
            animator.SetInteger("MouthState", mouthStartState);
        }

        if (audioSource != null)
        {
            audioSource.Play();
        }

        float talkingTime = (audioSource != null && audioSource.clip != null) ? audioSource.clip.length : 0f;
        return talkingTime;
    }

    public void FinishDialogue(int bodyEndState, int mouthEndState)
    {
        if (animator != null)
        {
            animator.SetInteger("BodyState", bodyEndState);
            animator.SetInteger("MouthState", mouthEndState);
        }
    }

    public void StopDialogue(int bodyEndState = 0, int mouthEndState = 0)
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        DisableDialogueBox();

        if (animator != null)
        {
            animator.SetInteger("BodyState", bodyEndState);
            animator.SetInteger("MouthState", mouthEndState);
        }

        IsTalking = false;
    }

    public void DisableDialogueBox()
    {
        if (dialogueBoxes != null) dialogueBoxes.SetActive(false);
        if (overlayCanvas != null) overlayCanvas.SetActive(false);
        if (nestedCanvas != null) nestedCanvas.SetActive(false);
        
        IsTalking = false;
    }
}

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

    public float StartTalking(string key, int bodyStartState, int eyesState, int mouthStartState, bool isUsingOverlay)
    {
        dialogueBoxes.SetActive(true);

        localizedKey.localizationKey = key;

        if (isUsingOverlay)
        {
            overlayCanvas.SetActive(true);
            nestedCanvas.SetActive(false);
            localizedKey.textComponent = overlayText;
        }
        else
        {
            overlayCanvas.SetActive(false);
            nestedCanvas.SetActive(true);
            localizedKey.textComponent = nestedText;
            nestedCanvas.GetComponent<TrajectoryCanvasFollower>().SetPosition();
        }

        localizedKey.UpdateText();
        localizedKey.UpdateAudioClip();

        animator.SetInteger("BodyState", bodyStartState);
        animator.SetInteger("EyesState", eyesState);
        animator.SetInteger("MouthState", mouthStartState);

        audioSource.Play();

        float talkingTime = audioSource.clip.length;
        return talkingTime;
    }

    public void FinishDialogue(int bodyEndState, int mouthEndState)
    {
        animator.SetInteger("BodyState", bodyEndState);
        animator.SetInteger("MouthState", mouthEndState);
       
    }

    public void DisableDialogueBox()
    {
        dialogueBoxes.SetActive(false);
    }
}
using System.Collections;
using DG.Tweening;
using UnityEngine;

public class SequenceDirector : MonoBehaviour
{
    [Header("List of steps")]
    [SerializeField] SequenceStep[] sequenceSteps;

    [Header("Player References")]
    [SerializeField] Player playerTransition;
 
    [Header("Narrator References")]
    [SerializeField] Narrator narrator;

    [Header("Event Management")]
    [SerializeField] EventDirector eventDirector;

    [Header("Debugging")]
    [SerializeField] int debugStepIndex = 0;
    [SerializeField] int currentStepIndex = 0;

    bool hasPerformedAction = false;

    void Start()
    {
        StartCoroutine(ExecuteSequence());
    }

    IEnumerator ExecuteSequence()
    {
        for (int i = 0; i < sequenceSteps.Length; i++)
        {
            SequenceStep step = sequenceSteps[i];
            bool fastForward = false;
            #if UNITY_EDITOR
            fastForward = i < debugStepIndex;
            #endif
            yield return StartCoroutine(ExecuteStep(step, fastForward));
            currentStepIndex = i;
        }
    }

    IEnumerator ExecuteStep(SequenceStep step, bool fastForward)
    {
        #if UNITY_EDITOR
        if (fastForward)
        {
            ApplyFastForward(step);
            yield break;
        }
        #endif

        yield return HandleEventsStart(step);
        HandlePlayer(step);
        yield return HandleNarrator(step);
        yield return HandleDialogue(step);
        yield return HandleInteraction(step);
    }

    void ApplyFastForward(SequenceStep step)
    {
        if (step.hasSequenceEvents)
        {
            eventDirector.SetCurrentSequenceEvent(step.name);
            eventDirector.TriggerEventsForStep();
        }

        if (step.hasNarratorMovement)
        {
            narrator.transform.position = step.targetNarratorPosition;
            narrator.transform.localScale = Vector3.one * step.targetNarratorScale;
        }

        if (step.hasPlayerMovement)
        {
            playerTransition.transform.SetPositionAndRotation(
                step.playerTargetPosition,
                Quaternion.Euler(step.playerTargetRotation)
            );
        }
    }

    IEnumerator HandleEventsStart(SequenceStep step)
    {
        if (!step.hasSequenceEvents) yield break;

        eventDirector.SetCurrentSequenceEvent(step.name);
        eventDirector.TriggerEventsForStep();

        yield break;
    }

    void HandlePlayer(SequenceStep step)
    {
        if (!step.hasPlayerMovement) return;

        playerTransition.MovePlayer(
            step.playerTargetPosition,
            step.hasPlayerRotation,
            step.playerTargetRotation,
            step.hasSceneTransition,
            step.sceneName,
            step.isGoingBackToMainScene
        );
    }

    IEnumerator HandleNarrator(SequenceStep step)
    {
        if (!step.hasNarratorMovement) yield break;

        Tween action = narrator.Move(
            step.targetNarratorPosition,
            step.offsetAtCenter,
            step.hasNarratorRotation,
            step.targetNarratorRotation,
            step.targetNarratorScale,
            step.flyDuration
        );

        yield return action.WaitForCompletion();
    }

    IEnumerator HandleDialogue(SequenceStep step)
    {
        if (!step.hasDialogue) yield break;

        yield return new WaitForSeconds(step.timeWaitBeforeTalking);

        float talkingTime = narrator.StartTalking(
            step.dialogueKey,
            step.bodyStartState,
            step.eyesState,
            step.mouthStartState,
            step.isUsingOverlay
        );

        yield return new WaitForSeconds(talkingTime);

        narrator.FinishDialogue(step.bodyEndState, step.mouthEndState);

        yield return new WaitForSeconds(step.timeWaitAfterTalking);

        narrator.DisableDialogueBox();
    }

    IEnumerator HandleInteraction(SequenceStep step)
    {
        if (!step.hasInteraction) yield break;

        hasPerformedAction = false;

        float timer = 0f;
        bool hasTriggeredSubStep = false;

        while (!hasPerformedAction)
        {
            timer += Time.deltaTime;
            
            if (step.hasSubStep && !hasTriggeredSubStep && timer >= step.timeToWaitBeforeSubStep)
            {
                hasTriggeredSubStep = true;
                yield return StartCoroutine(ExecuteStep(step.subStep, false));
            }

            if (!step.hasInfiniteTimeout && timer >= step.waitTimeout)
            {
                Debug.Log("Timeout reached, performing default action");
                break;
            }

            yield return null;
        }

        hasPerformedAction = true;
    }

    public void PerformAction()
    {
        hasPerformedAction = true;
        Debug.Log("Performed Action");
    }
}

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SequenceDirector : MonoBehaviour
{
    [Header("List of steps")]
    [SerializeField] List<SequenceStep> sequenceSteps;

    [Header("Debugging")]
    [SerializeField] int jumpToStepIndex = 0;

    [ShowNonSerializedField] int currentStepIndex = 0;
    bool hasPerformedAction = false;
    bool isSceneLoading = false;
    bool isReadyToProceed = true;

    // Automatically assign references
    Player player;
    Narrator narrator;
    EventDirector eventDirector;

    public static SequenceDirector Instance { get; private set; }

    readonly HashSet<string> pendingTriggers = new HashSet<string>();

    Coroutine currentSequenceCoroutine;
    SequenceStep currentExecutingStep;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        currentSequenceCoroutine = StartCoroutine(ExecuteSequence());
    }

#if UNITY_EDITOR
    void Update()
    {
        KeyboardPerformAction();
    }
#endif

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isSceneLoading = false;
        StartCoroutine(WaitForSceneLoad());
    }

    IEnumerator WaitForSceneLoad()
    {
        isReadyToProceed = false;
        yield return new WaitUntil(() => (narrator = FindFirstObjectByType<Narrator>()) != null);
        yield return new WaitUntil(() => (player = FindFirstObjectByType<Player>()) != null);
        yield return new WaitUntil(() => (eventDirector = FindFirstObjectByType<EventDirector>()) != null);
        isReadyToProceed = true;
    }

    IEnumerator ExecuteSequence()
    {
        while (true)
        {
            yield return new WaitUntil(() => currentStepIndex < sequenceSteps.Count);
            yield return new WaitUntil(() => isReadyToProceed && !isSceneLoading);

            SequenceStep step = sequenceSteps[currentStepIndex];
          
            if (step != null)
            {
                currentExecutingStep = step;

                bool fastForward = false;
                #if UNITY_EDITOR
                fastForward = currentStepIndex < jumpToStepIndex;
                #endif

                yield return StartCoroutine(ExecuteStep(step, fastForward));

                currentExecutingStep = null;
            } 
            
            currentStepIndex++;
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

        hasPerformedAction = false;

        yield return HandleEventsStart(step);
        HandlePlayer(step);
        yield return HandleNarrator(step);
        yield return HandleDialogue(step);
        yield return HandleInteraction(step);

        hasPerformedAction = false;
    }

    void ApplyFastForward(SequenceStep step)
    {
        if (step.hasSequenceEvents && eventDirector != null)
        {
            eventDirector.SetCurrentSequenceEvent(step.name);
            eventDirector.TriggerEventsForStep();
        }

        if (step.hasNarratorMovement && narrator != null)
        {
            narrator.transform.position = step.targetNarratorPosition;
            narrator.transform.localScale = Vector3.one * step.targetNarratorScale;
        }

        if (step.hasPlayerMovement && player != null)
        {
            player.transform.SetPositionAndRotation(
                step.playerTargetPosition,
                Quaternion.Euler(step.playerTargetRotation)
            );
        }
    }

    IEnumerator HandleEventsStart(SequenceStep step)
    {
        if (!step.hasSequenceEvents || eventDirector == null) yield break;
        eventDirector.SetCurrentSequenceEvent(step.name);
        eventDirector.TriggerEventsForStep();

        yield break;
    }

    void HandlePlayer(SequenceStep step)
    {
        if (!step.hasPlayerMovement || player == null) return;

        player.MovePlayer(
            step.playerTargetPosition,
            step.hasPlayerRotation,
            step.playerTargetRotation
        );
    }

    IEnumerator HandleNarrator(SequenceStep step)
    {
        if (!step.hasNarratorMovement || narrator == null) yield break;

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
        if (!step.hasDialogue || narrator == null) yield break;

        // Check if player already performed action before dialogue wait begins
        if (CanInterruptDialogueWithAction(step) && hasPerformedAction)
        {
            yield break;
        }

        if (step.timeWaitBeforeTalking > 0f)
        {
            yield return WaitForDialogueDuration(step.timeWaitBeforeTalking, step);
            if (CanInterruptDialogueWithAction(step) && hasPerformedAction)
            {
                yield break;
            }
        }

        float talkingTime = narrator.StartTalking(
            step.dialogueKey,
            step.bodyStartState,
            step.eyesState,
            step.mouthStartState,
            step.isUsingOverlay
        );

        yield return WaitForDialogueDuration(talkingTime, step);

        if (CanInterruptDialogueWithAction(step) && hasPerformedAction)
        {
            // Early action during dialogue: immediately halt speech, close UI, update animator
            narrator.StopDialogue(step.bodyEndState, step.mouthEndState);
            yield break;
        }

        narrator.FinishDialogue(step.bodyEndState, step.mouthEndState);

        if (step.timeWaitAfterTalking > 0f)
        {
            yield return WaitForDialogueDuration(step.timeWaitAfterTalking, step);
            if (CanInterruptDialogueWithAction(step) && hasPerformedAction)
            {
                narrator.DisableDialogueBox();
                yield break;
            }
        }

        narrator.DisableDialogueBox();
    }

    IEnumerator WaitForDialogueDuration(float duration, SequenceStep step)
    {
        float timer = 0f;
        while (timer < duration)
        {
            if (CanInterruptDialogueWithAction(step) && hasPerformedAction)
            {
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    bool CanInterruptDialogueWithAction(SequenceStep step)
    {
        return step != null && step.hasInteraction && step.allowEarlyAction;
    }

    IEnumerator HandleInteraction(SequenceStep step)
    {
        if (!step.hasInteraction) yield break;

        // If player already performed action early (e.g. during Phase 2 speech), proceed immediately
        if (hasPerformedAction)
        {
            hasPerformedAction = false;
            pendingTriggers.Clear();

            if (step.isSceneTransition)
            {
                isSceneLoading = true;
                isReadyToProceed = false;
                SceneTransition.Instance.SwitchScene(step.sceneToLoad);
            }
            yield break;
        }

        hasPerformedAction = false;
        float timer = 0f;

        HashSet<SubstepEntry> triggeredSubSteps = new HashSet<SubstepEntry>();
        pendingTriggers.Clear();

        while (!hasPerformedAction)
        {
            timer += Time.deltaTime;
            
            if (step.hasSubStep)
            {
                for (int i = 0; i < step.subStepEntries.Count; i++)
                {
                    SubstepEntry subStepEntry = step.subStepEntries[i];

                    // --- EMPTY SUBSTEP CHECK ---
                    if (subStepEntry == null || subStepEntry.subStep == null) continue;

                    if (triggeredSubSteps.Contains(subStepEntry)) continue;
                    bool triggered = false;

                    if (subStepEntry.triggerType == SubstepTriggerType.Timeout)
                    {
                        if (timer >= subStepEntry.delayTime)
                            triggered = true;
                    }
                    else if (subStepEntry.triggerType == SubstepTriggerType.Trigger)
                    {
                        if (ConsumeTrigger(subStepEntry.triggerKey))
                            triggered = true;
                    }

                    if (triggered)
                    {
                        triggeredSubSteps.Add(subStepEntry);
                        currentExecutingStep = subStepEntry.subStep;
                        yield return StartCoroutine(ExecuteStep(subStepEntry.subStep, false));
                        currentExecutingStep = step;
                        break;
                    }
                }
            }

            if (!step.hasInfiniteTimeout && timer >= step.waitTimeout) break;
            yield return null;
        }

        pendingTriggers.Clear();
        hasPerformedAction = true;

        if (step.isSceneTransition)
        {
            isSceneLoading = true;
            isReadyToProceed = false;
            SceneTransition.Instance.SwitchScene(step.sceneToLoad);
        }
    }

    #region Substep Management
    public void SetTrigger(string triggerName)
    {
        if (!string.IsNullOrEmpty(triggerName))
            pendingTriggers.Add(triggerName);
    }

    bool ConsumeTrigger(string triggerName)
    {
        return pendingTriggers.Remove(triggerName);
    }

    void ClearTriggers()
    {
        pendingTriggers.Clear();
    }
    #endregion

    #region Steps Management
    public void InsertNextSteps(IEnumerable<SequenceStep> newSteps)
    {
        if (newSteps == null) return;
        int insertIndex = Mathf.Clamp(currentStepIndex + 1, 0, sequenceSteps.Count);
        sequenceSteps.InsertRange(insertIndex, newSteps);
    }

    public void EnqueueSteps(IEnumerable<SequenceStep> newSteps)
    {
        if (newSteps == null) return;
        sequenceSteps.AddRange(newSteps);
    }

    public void InterruptAndPlaySteps(IEnumerable<SequenceStep> newSteps, InterruptionDiscardMode discardMode = InterruptionDiscardMode.DiscardCurrentStepOnly)
    {
        if (newSteps == null) return;
        if (isSceneLoading || !isReadyToProceed) return;

        SequenceStep stepToInterrupt = currentExecutingStep != null
            ? currentExecutingStep
            : (currentStepIndex < sequenceSteps.Count ? sequenceSteps[currentStepIndex] : null);

        // Check if the step being interrupted permits immediate interruption
        if (stepToInterrupt != null && !stepToInterrupt.canBeInterrupted)
        {
            InsertNextSteps(newSteps);
            return;
        }

        // 1. Stop all active sequence and step coroutines
        StopAllCoroutines();
        currentSequenceCoroutine = null;

        // 2. Clean up current step visual/audio/state
        CleanupCurrentStep();

        // 3. Mutate the queue based on discardMode
        if (currentStepIndex < sequenceSteps.Count)
        {
            switch (discardMode)
            {
                case InterruptionDiscardMode.DiscardAllRemainingSteps:
                    int countToRemove = sequenceSteps.Count - currentStepIndex;
                    sequenceSteps.RemoveRange(currentStepIndex, countToRemove);
                    break;

                case InterruptionDiscardMode.DiscardCurrentStepOnly:
                    sequenceSteps.RemoveAt(currentStepIndex);
                    break;

                case InterruptionDiscardMode.KeepCurrentStep:
                    // Keep the interrupted step: it will be pushed back by inserting at currentStepIndex
                    break;
            }
        }

        int insertIndex = Mathf.Clamp(currentStepIndex, 0, sequenceSteps.Count);
        sequenceSteps.InsertRange(insertIndex, newSteps);

        // 4. Resume sequence execution immediately
        currentSequenceCoroutine = StartCoroutine(ExecuteSequence());
    }

    public void InterruptAndPlaySteps(IEnumerable<SequenceStep> newSteps, bool discardCurrentStep)
    {
        InterruptAndPlaySteps(newSteps, discardCurrentStep ? InterruptionDiscardMode.DiscardCurrentStepOnly : InterruptionDiscardMode.KeepCurrentStep);
    }

    void CleanupCurrentStep()
    {
        hasPerformedAction = false;
        pendingTriggers.Clear();

        SequenceStep step = currentExecutingStep != null
            ? currentExecutingStep
            : (currentStepIndex < sequenceSteps.Count ? sequenceSteps[currentStepIndex] : null);

        if (narrator != null)
        {
            if (step != null && step.hasDialogue)
            {
                narrator.StopDialogue(step.bodyEndState, step.mouthEndState);
            }
            else
            {
                narrator.StopDialogue();
            }

            if (step != null && step.hasNarratorMovement)
            {
                narrator.SnapMovement(
                    step.targetNarratorPosition,
                    step.hasNarratorRotation,
                    step.targetNarratorRotation,
                    step.targetNarratorScale
                );
            }
            else
            {
                narrator.StopMovement();
            }
        }

        if (eventDirector != null && eventDirector.currentSequenceEvent != null)
        {
            eventDirector.currentSequenceEvent.StopDelayedEvents();
        }

        currentExecutingStep = null;
    }
    #endregion

    public void PerformAction()
    {
        hasPerformedAction = true;
        Debug.Log("Performed Action");
    }

    void KeyboardPerformAction()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            PerformAction();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SequenceDirector : MonoBehaviour
{
    [Header("List of steps")]
    [SerializeField] SequenceStep[] sequenceSteps;

    [Header("Debugging")]
    [SerializeField] int debugStepIndex = 0;
    [SerializeField] int currentStepIndex = 0;

    bool hasPerformedAction = false;
    bool isSceneLoading = false;
    bool isReadyToProceed = true;

    //Automatically assign references
    Player player;
    Narrator narrator;
    EventDirector eventDirector;

    public static SequenceDirector Instance { get; private set; }

    readonly HashSet<string> pendingTriggers = new HashSet<string>();

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
        StartCoroutine(ExecuteSequence());
    }

    void Update()
    {
        KeyboardPerformAction();
        Debug.Log("isSceneLoading: " + isSceneLoading + ", isReadyToProceed: " + isReadyToProceed);
    }

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
        for (int i = 0; i < sequenceSteps.Length; i++)
        {
            yield return new WaitUntil(() => isReadyToProceed && !isSceneLoading);

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
            player.transform.SetPositionAndRotation(
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

        player.MovePlayer(
            step.playerTargetPosition,
            step.hasPlayerRotation,
            step.playerTargetRotation
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

        HashSet<SubstepEntry> triggeredSubSteps = new HashSet<SubstepEntry>();
        pendingTriggers.Clear();

        while (!hasPerformedAction)
        {
            timer += Time.deltaTime;
            
            if (step.hasSubStep)
            {
                foreach (var subStepEntry in step.subStepEntries)
                {
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
                        yield return StartCoroutine(ExecuteStep(subStepEntry.subStep, false));
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

    public void PerformAction()
    {
        hasPerformedAction = true;
        Debug.Log("Performed Action");
    }

    void KeyboardPerformAction()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            PerformAction();
        }
    }
}

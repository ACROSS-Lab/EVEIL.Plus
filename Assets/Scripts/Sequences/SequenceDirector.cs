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

    // void Update()
    // {
    //     KeyboardPerformAction();
    // }

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
                bool fastForward = false;
                #if UNITY_EDITOR
                fastForward = currentStepIndex < jumpToStepIndex;
                #endif

                yield return StartCoroutine(ExecuteStep(step, fastForward));
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

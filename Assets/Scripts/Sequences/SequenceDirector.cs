using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
    [SerializeField] UnityEvent[] debugEvents;

    bool hasPerformedAction = false;
    bool isSceneLoading = false;
    bool isReadyToProceed = true;

    public static SequenceDirector Instance { get; private set; }

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
        isReadyToProceed = true;
    }

    IEnumerator ExecuteSequence()
    {
        for (int i = 0; i < sequenceSteps.Length; i++)
        {
            Debug.Log("isScneLoading: " + isSceneLoading + ", isReadyToProceed: " + isReadyToProceed);
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

    public void StartSceneTransition()
    {
        isSceneLoading = true;
        isReadyToProceed = false;
    }

    void KeyboardPerformAction()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            for (int i = 0; i < debugEvents.Length; i++)
            {
                debugEvents[i].Invoke();
            }
        }
    }
}

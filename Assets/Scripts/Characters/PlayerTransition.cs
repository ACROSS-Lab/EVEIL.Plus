using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("Fading Setup")]
    [SerializeField] CanvasGroup fadeCanvasGroup;
    [SerializeField] float fadeDuration = 0.5f;
    [SerializeField] bool hasFadeOnStart = true;

    [Header("Diorama Transition")]
    [SerializeField] GameObject objectToKeep;
    [SerializeField] SequenceDirector sequenceDirector;
    [SerializeField] GameObject exitDioramaButton;

    List<GameObject> primarySceneRoots = new List<GameObject>();
    Scene mainScene;
    string additiveSceneName;

    void Start()
    {        
        if (hasFadeOnStart) 
        {
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.DOFade(0f, fadeDuration);
        }

        mainScene = SceneManager.GetActiveScene();
    }

    public void MovePlayer(Vector3 position, bool hasRotation, Vector3 rotation, bool hasSceneTransition, string sceneName, bool isGoingBackToMainScene)
    {
        StartCoroutine(MovePlayerCoroutine(position, hasRotation, rotation, hasSceneTransition, sceneName, isGoingBackToMainScene));
    }

    IEnumerator MovePlayerCoroutine(Vector3 position, bool hasRotation, Vector3 rotation, bool hasSceneTransition, string sceneName, bool isGoingBackToMainScene)
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true; 
            yield return fadeCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();
        }

        transform.position = position;
        if (hasRotation) transform.rotation = Quaternion.Euler(rotation);

        if (hasSceneTransition)
        {
            if (isGoingBackToMainScene) yield return TransitionBack(sceneName);
            else yield return TransitionToSideScene(sceneName);
        }

        yield return new WaitForSeconds(1f);
        
        if (fadeCanvasGroup != null)
        {
            yield return fadeCanvasGroup.DOFade(0f, fadeDuration).WaitForCompletion();
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    IEnumerator TransitionToSideScene(string sceneName)
    {
        Scene primaryScene = SceneManager.GetActiveScene();
        additiveSceneName = sceneName;

        primarySceneRoots.Clear();
        primarySceneRoots.AddRange(primaryScene.GetRootGameObjects());
        primarySceneRoots.Remove(objectToKeep);

        LightmapSettings.lightmapsMode = LightmapsMode.CombinedDirectional;
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        yield return null; 

        SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));

        foreach (GameObject rootObj in primarySceneRoots)
        {
            rootObj.SetActive(false);
        }
    }

    IEnumerator TransitionBack(string sceneName)
    {
        SceneManager.SetActiveScene(mainScene);

        LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
        AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(sceneName);
        while (!asyncUnload.isDone)
        {
            yield return null;
        }

        foreach (GameObject rootObj in primarySceneRoots)
        {
            if (rootObj != null)
            {
                rootObj.SetActive(true);
            }
        }

        additiveSceneName = null;
        Debug.Log("Back to main scene");
    }

    public void LoadNormalSceneFadeOut(string sceneName)
    {
        StartCoroutine(LoadNormalScene(sceneName));
        Debug.Log("LoadNormalSceneFadeOut");
    }

    IEnumerator LoadNormalScene(string sceneName)
    {
        if (additiveSceneName != null)
        {
            yield return SceneManager.UnloadSceneAsync(additiveSceneName);
        }

        Debug.Log("Loading new scene...");

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            yield return fadeCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();
        }

        yield return SceneManager.LoadSceneAsync(sceneName);
    }

    public void SetLanguage(string languageName)
    {
        LocalizationManager.Instance.SetLanguage(languageName);
    }

    public void SetupDioramaExitButton()
    {
        if (exitDioramaButton == null) return;

        exitDioramaButton.SetActive(true);
        exitDioramaButton.transform.position = transform.position;
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneLoader : Singleton<SceneLoader>
{
    public bool IsLoading { get; private set; }

    [SerializeField] private Canvas _canvas;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Slider progressBar;

    private string _loadSceneName;
    private event Action _onSceneLoaded;

    protected override void OnAwake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    protected override void OnDestroyed()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (!IsLoading || scene.name != _loadSceneName)
        {
            return;
        }

        StartCoroutine(OnSceneLoadedCoroutine());
    }

    private IEnumerator OnSceneLoadedCoroutine()
    {
        yield return new WaitUntil(() => _loadSceneName == SceneManager.GetActiveScene().name);

        UIManager.Instance().FadeOut(_canvasGroup, 0.4f);
        _onSceneLoaded?.Invoke();
        IsLoading = false;

        yield break;
    }

    public void LoadScene(string sceneName, Action onLoaded = null)
    {
        if (IsLoading)
        {
            return;
        }

        _loadSceneName = sceneName;
        _onSceneLoaded = onLoaded;

        StartCoroutine(LoadSceneInternal());
    }

    private IEnumerator LoadSceneInternal()
    {
        IsLoading = true;
        progressBar.value = 0f;
        UIManager.Instance().FadeIn(_canvasGroup, 0.4f);
        yield return new WaitForSecondsRealtime(0.4f);

        AsyncOperation op = SceneManager.LoadSceneAsync(_loadSceneName);
        op.allowSceneActivation = false;

        float timer = 0.0f;
        while (!op.isDone)
        {
            timer += Time.unscaledDeltaTime;
            if (op.progress < 0.9f)
            {
                progressBar.value = Mathf.Lerp(progressBar.value, op.progress, timer);
                if (progressBar.value >= op.progress)
                {
                    timer = 0f;
                }
            }
            else
            {
                progressBar.value = Mathf.Lerp(progressBar.value, 1f, timer);
                if (progressBar.value == 1.0f)
                {
                    op.allowSceneActivation = true;
                    break;
                }
            }

            yield return null;
        }

        yield break;
    }
}

using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;
using System;


public class UIManager : Singleton<UIManager>
{
    public bool IsInterrupted { get; private set; }

    public void FadeIn(CanvasGroup canvasGroup, float time)
    {
        StartCoroutine(ProcessFadeIn(canvasGroup, time));
    }

    private IEnumerator ProcessFadeIn(CanvasGroup canvasGroup, float time)
    {
        if (canvasGroup == null || IsInterrupted)
        {
            yield break;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.gameObject.SetActive(true);

        float percent = 0f;
        while (percent < 1f)
        {
            if (IsInterrupted)
            {
                break;
            }

            percent += Time.unscaledDeltaTime / time;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, percent);

            yield return null;
        }

        canvasGroup.alpha = 1f;
        yield break;
    }

    public void FadeOut(CanvasGroup canvasGroup, float time)
    {
        StartCoroutine(ProcessFadeOut(canvasGroup, time));
    }

    private IEnumerator ProcessFadeOut(CanvasGroup canvasGroup, float time)
    {
        if (canvasGroup == null || IsInterrupted)
        {
            yield break;
        }

        float percent = 0f;
        while (percent < 1f)
        {
            if (IsInterrupted)
            {
                break;
            }

            percent += Time.unscaledDeltaTime / time;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, percent);

            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.gameObject.SetActive(false);

        yield break;
    }

    public void FadeIn(Image image, float time)
    {
        StartCoroutine(ProcessFadeIn(image, time));
    }

    private IEnumerator ProcessFadeIn(Image image, float time)
    {
        if (image == null || IsInterrupted)
        {
            yield break;
        }

        image.color = image.color.WithA(0f);
        image.gameObject.SetActive(true);

        float percent = 0f;
        while (percent < 1f)
        {
            if (IsInterrupted)
            {
                break;
            }

            percent += Time.unscaledDeltaTime / time;
            image.color = image.color.WithA(Mathf.Lerp(0f, 1f, percent));

            yield return null;
        }

        image.color = image.color.WithA(1f);
        yield break;
    }

    public void FadeOut(Image image, float time)
    {
        StartCoroutine(ProcessFadeOut(image, time));
    }

    private IEnumerator ProcessFadeOut(Image image, float time)
    {
        if (image == null || IsInterrupted)
        {
            yield break;
        }

        float percent = 0f;
        while (percent < 1f)
        {
            if (IsInterrupted)
            {
                break;
            }

            percent += Time.unscaledDeltaTime / time;
            image.color = image.color.WithA(Mathf.Lerp(1f, 0f, percent));

            yield return null;
        }

        image.color = image.color.WithA(0f);
        image.gameObject.SetActive(false);

        yield break;
    }
}

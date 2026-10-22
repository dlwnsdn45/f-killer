using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EndingSystem : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroupScrollView;
    [SerializeField] private CanvasGroup _canvasGroupEndingText;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Button _mainButton;

    [SerializeField] private float _waitTime;
    [SerializeField] private float _scrollSpeed;

    private void Awake()
    {
        _canvasGroupScrollView.interactable = false;
        _canvasGroupScrollView.blocksRaycasts = false;
        _scrollRect.vertical = false;

        _canvasGroupEndingText.alpha = 0f;

        _mainButton.onClick.RemoveAllListeners();
        _mainButton.onClick.AddListener(() => GameManager_S.Instance().GoToMain());
    }

    private void Start()
    {
        StartCoroutine(Ending());
    }

    private IEnumerator Ending()
    {
        yield return new WaitForSecondsRealtime(0.25f);
        UIManager.Instance().FadeIn(_canvasGroupEndingText, 1.5f);
        yield return new WaitForSecondsRealtime(_waitTime);

        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();

        _scrollRect.verticalNormalizedPosition = 1f;
        _scrollRect.vertical = true;

        float scrollable = _scrollRect.content.rect.height - _scrollRect.viewport.rect.height;
        if (scrollable <= 0f)
        {
            yield break;
        }

        UIManager.Instance().FadeOut(_canvasGroupEndingText, 0.5f);
        yield return new WaitForSecondsRealtime(1f);

        float normalizedSpeed = _scrollSpeed / scrollable;
        while (_scrollRect.verticalNormalizedPosition > 0f)
        {
            _scrollRect.verticalNormalizedPosition = Mathf.MoveTowards(_scrollRect.verticalNormalizedPosition, 0f, normalizedSpeed * Time.unscaledDeltaTime);
            yield return null;
        }

        _scrollRect.verticalNormalizedPosition = 0f;
        _canvasGroupScrollView.interactable = true;
        _canvasGroupScrollView.blocksRaycasts = true;
    }
}
using UnityEngine;
using UnityEngine.UI;

public class TitleScene : MonoBehaviour
{
    [SerializeField] private Button _gameStartButton;
    [SerializeField] private Button _settingButton;

    private void Awake()
    {
        _gameStartButton.onClick.RemoveAllListeners();
        _gameStartButton.onClick.AddListener(() => SceneLoader.Instance().LoadScene("SampleScene")); // 씬 이름 변경 시 여기도 바꾸기

        _settingButton.onClick.RemoveAllListeners();
        _settingButton.onClick.AddListener(() => OptionManager.Instance().OpenUI());
    }
}

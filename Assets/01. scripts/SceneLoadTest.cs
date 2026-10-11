using UnityEngine;
using UnityEngine.UI;

public class SceneLoadTest : MonoBehaviour
{
    [SerializeField] private Button _loadButton1;
    [SerializeField] private Button _loadButton2;

    private void Awake()
    {
        if (_loadButton1 != null)
        {
            _loadButton1.onClick.RemoveAllListeners();
            _loadButton1.onClick.AddListener(() => SceneLoader.Instance().LoadScene("TestScene2"));
        }

        if (_loadButton2 != null)
        {
            _loadButton2.onClick.RemoveAllListeners();
            _loadButton2.onClick.AddListener(() => SceneLoader.Instance().LoadScene("TestScene1"));
        }
    }
}

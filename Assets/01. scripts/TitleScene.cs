using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class TitleScene : MonoBehaviour
{
    [SerializeField] private TMP_Text _tapText;
    [SerializeField] private float _blinkSpeed = 1.5f;

    private void Update()
    {
        _tapText.alpha = Mathf.PingPong(Time.time * _blinkSpeed, 1f);

        if (Pointer.current != null && Pointer.current.press.wasReleasedThisFrame && !SceneLoader.Instance().IsLoading)
        {
            if (!OptionManager.Instance().IsMutedSFX)
                AudioSource.PlayClipAtPoint(Resources.Load<AudioClip>("Sounds/start"), Camera.main.transform.position, OptionManager.Instance().SFXVolume);
            SceneLoader.Instance().LoadScene("SampleScene"); // 씬 이름 변경 시 여기도 바꾸기
        }
    }
}

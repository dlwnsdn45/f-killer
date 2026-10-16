using System;
using UnityEngine;

public class GameManager_S : Singleton<GameManager_S>
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeOnLoaded()
    {
        Create(typeof(GameManager_S).Name);
    }

    protected override void OnAwake()
    {
        OptionManager.Instance().LoadData();
    }

    public void GoToMain()
    {
        SceneLoader.Instance().LoadScene("TitleScene");
    }
}

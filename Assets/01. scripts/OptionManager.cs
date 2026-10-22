using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

public class OptionManager : Singleton<OptionManager>
{
    public static readonly (string name, int width, int height)[] RESOLUTIONS =
    {
        ("HD",    1280, 720),
        ("WXGA",  1366, 768),
        ("HD+",   1600, 900),
        ("FHD",   1920, 1080)
    };

    [SerializeField] private CanvasGroup _canvasGroup;

    [SerializeField] private TMP_Dropdown _resolutionDropdown;
    [SerializeField] private Button _fullscreenButton;
    [SerializeField] private Button _windowedButton;

    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Slider _bgmSlider;

    [SerializeField] private Toggle _sfxMuteToggle;
    [SerializeField] private Toggle _bgmMuteToggle;

    public float SFXVolume { get; set; }
    public float BGMVolume { get; set; }
    public bool IsMutedSFX { get; set; }
    public bool IsMutedBGM { get; set; }

    public int CurWidth { get; set; }
    public int CurHeight { get; set; }

    protected override void OnAwake()
    {
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        foreach (var (name, width, height) in RESOLUTIONS)
        {
            options.Add(new TMP_Dropdown.OptionData($"{name} ({width} x {height})"));
        }

        _resolutionDropdown.ClearOptions();
        _resolutionDropdown.AddOptions(options);

        _resolutionDropdown.onValueChanged.RemoveAllListeners();
        _resolutionDropdown.onValueChanged.AddListener(OnValueChangedResolutionOption);

        _fullscreenButton.onClick.RemoveAllListeners();
        _fullscreenButton.onClick.AddListener(() => ApplyResolution(CurWidth, CurHeight, true));

        _windowedButton.onClick.RemoveAllListeners();
        _windowedButton.onClick.AddListener(() => ApplyResolution(CurWidth, CurHeight, false));

        _bgmSlider.onValueChanged.RemoveAllListeners();
        _bgmSlider.onValueChanged.AddListener(OnValueChangedBGMSlider);

        _sfxSlider.onValueChanged.RemoveAllListeners();
        _sfxSlider.onValueChanged.AddListener(OnValueChangedSFXSlider);

        _bgmMuteToggle.onValueChanged.RemoveAllListeners();
        _bgmMuteToggle.onValueChanged.AddListener(OnValueChangedBGMMuteToggle);

        _sfxMuteToggle.onValueChanged.RemoveAllListeners();
        _sfxMuteToggle.onValueChanged.AddListener(OnValueChangedSFXMuteToggle);
    }

    public void InitData()
    {
        if (!PlayerPrefs.HasKey("resolution"))
        {
            PlayerPrefs.SetString("resolution", "FHD");
        }

        if (!PlayerPrefs.HasKey("sfx"))
        {
            PlayerPrefs.SetFloat("sfx", 1f);
        }

        if (!PlayerPrefs.HasKey("bgm"))
        {
            PlayerPrefs.SetFloat("bgm", 1f);
        }

        if (!PlayerPrefs.HasKey("sfx_muted"))
        {
            PlayerPrefs.SetInt("sfx_muted", 0);
        }

        if (!PlayerPrefs.HasKey("bgm_muted"))
        {
            PlayerPrefs.SetInt("bgm_muted", 0);
        }

        PlayerPrefs.Save();
    }

    public void SaveData()
    {
        PlayerPrefs.SetString("resolution", GetResolution(CurWidth, CurHeight));
        PlayerPrefs.SetFloat("sfx", SFXVolume);
        PlayerPrefs.SetFloat("bgm", BGMVolume);
        PlayerPrefs.SetInt("bgm_muted", IsMutedBGM ? 1 : 0);
        PlayerPrefs.SetInt("sfx_muted", IsMutedSFX ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void LoadData()
    {
        InitData();

        _sfxSlider.value = PlayerPrefs.GetFloat("sfx");
        _bgmSlider.value = PlayerPrefs.GetFloat("bgm");

        _sfxMuteToggle.isOn = PlayerPrefs.GetInt("sfx_muted") == 1;
        _bgmMuteToggle.isOn = PlayerPrefs.GetInt("bgm_muted") == 1;

        SetResolution(PlayerPrefs.GetString("resolution"), Screen.fullScreen);
    }

    public void OpenUI()
    {
        UIManager.Instance().FadeIn(_canvasGroup, 0.1f);
    }

    public void CloseUI()
    {
        UIManager.Instance().FadeOut(_canvasGroup, 0.1f);
    }

    public void OnValueChangedResolutionOption(int value)
    {
        string resolution = _resolutionDropdown.options[value].text.Split('(')[0].Trim();
        SetResolution(resolution, Screen.fullScreen);
    }

    public string GetResolution(int width, int height)
    {
        foreach (var r in RESOLUTIONS)
        {
            if (r.width == width && r.height == height)
            {
                return r.name;
            }
        }

        return "";
    }

    public void SetResolution(string resolution, bool fullScreen)
    {
        int index = RESOLUTIONS.FindIndex(r => r.name == resolution);

        if (index < 0)
        {
            //Debug.LogError("Cannot Find Resolution: " + resolution);
            index = RESOLUTIONS.FindIndex(r => r.name == "FHD"); // fallback
        }

        var (_, width, height) = RESOLUTIONS[index];
        CurWidth = width;
        CurHeight = height;
        _resolutionDropdown.SetValueWithoutNotify(index);

        ApplyResolution(width, height, fullScreen);
        SaveData();
    }

    public void SetResolution(int setWidth, int setHeight, bool fullScreen)
    {
        string resolution = GetResolution(setWidth, setHeight);
        int index = RESOLUTIONS.FindIndex(r => r.name == resolution);

        if (index < 0)
        {
            //Debug.LogError("Cannot Find Resolution: " + resolution);
            index = RESOLUTIONS.FindIndex(r => r.name == "FHD"); // fallback
        }

        var (_, width, height) = RESOLUTIONS[index];
        CurWidth = width;
        CurHeight = height;
        _resolutionDropdown.SetValueWithoutNotify(index);

        ApplyResolution(width, height, fullScreen);
        SaveData();
    }

    private void ApplyResolution(int setWidth, int setHeight, bool fullScreen)
    {
        int deviceWidth = Screen.width;
        int deviceHeight = Screen.height;

        Screen.SetResolution(setWidth, (int)(((float)deviceHeight / deviceWidth) * setWidth), fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    public void OnValueChangedBGMSlider(float value)
    {
        BGMVolume = value;
        SaveData();
    }

    public void OnValueChangedSFXSlider(float value)
    {
        SFXVolume = value;
        SaveData();
    }

    public void OnValueChangedBGMMuteToggle(bool isOn)
    {
        IsMutedBGM = isOn;
        SaveData();
    }

    public void OnValueChangedSFXMuteToggle(bool isOn)
    {
        IsMutedSFX = isOn;
        SaveData();
    }
}

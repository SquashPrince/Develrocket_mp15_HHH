using System;
using UnityEngine;

// 플레이어가 설정 화면에서 바꾸는 값 묶음. JSON 파일로 저장하고 불러온다.
// 항목이 늘면 이 묶음에 칸을 더한다. 저장 파일에 없는 칸은 불러올 때 여기 적은 기본값을 쓴다.
[Serializable]
public class SettingsData
{
    [Tooltip("마우스 가로(x) · 세로(y) 감도 배율. 1 이면 기본 감도")]
    [SerializeField] private Vector2 _mouseSensitivity = Vector2.one;
    [Tooltip("전체 볼륨(0 ~ 1). 배경음 · 효과음에 함께 걸린다")]
    [SerializeField] private float _masterVolume = 1f;
    [Tooltip("배경음 볼륨(0 ~ 1)")]
    [SerializeField] private float _bgmVolume = 1f;
    [Tooltip("효과음 볼륨(0 ~ 1)")]
    [SerializeField] private float _sfxVolume = 1f;

    public Vector2 MouseSensitivity
    {
        get => _mouseSensitivity;
        set => _mouseSensitivity = value;
    }
    // 값을 바꿀 때 항상 0 ~ 1 사이로 맞춘다.
    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Mathf.Clamp01(value);
    }
    public float BgmVolume
    {
        get => _bgmVolume;
        set => _bgmVolume = Mathf.Clamp01(value);
    }
    public float SfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = Mathf.Clamp01(value);
    }
}

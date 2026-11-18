using System;
using System.IO;
using UnityEngine;

// 설정값을 들고 있고, JSON 파일로 불러오고 저장한다. 씬이 바뀌어도 같은 값을 쓰도록 정적 클래스로 둔다.
// 처음 값을 읽을 때 파일에서 불러온다. 파일이 없거나 읽을 수 없으면 기본값으로 시작한다.
// 값을 바꾸면 바로 알린다. 감도 · 볼륨을 적용하는 기능이 알림을 받아 다시 반영한다.
// 저장은 설정 화면을 닫을 때 한다. 슬라이더를 움직일 때마다 파일에 쓰지 않기 위해서다.
// 저장 위치는 실행 환경마다 정해진 사용자 데이터 폴더(Application.persistentDataPath)다.
public static class SettingsStore
{
    private const string FILE_NAME = "settings.json";

    private static SettingsData _current;

    private static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    public static SettingsData Current
    {
        get
        {
            if(_current == null) Load();

            return _current;
        }
    }

    // 설정값이 바뀌면 알린다. 구독하는 쪽은 꺼질 때 반드시 해제한다.
    public static event Action Changed;

    public static void SetMouseSensitivity(Vector2 sensitivity)
    {
        Current.MouseSensitivity = sensitivity;
        Changed?.Invoke();
    }

    public static void SetMasterVolume(float volume)
    {
        Current.MasterVolume = volume;
        Changed?.Invoke();
    }

    public static void SetBgmVolume(float volume)
    {
        Current.BgmVolume = volume;
        Changed?.Invoke();
    }

    public static void SetSfxVolume(float volume)
    {
        Current.SfxVolume = volume;
        Changed?.Invoke();
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
        }
        catch(Exception exception)
        {
            Debug.LogWarning($"[설정] 저장하지 못했습니다 : {exception.Message}");
        }
    }

    // 파일이 깨졌거나 읽을 수 없어도 게임은 기본값으로 계속한다.
    private static void Load()
    {
        _current = new SettingsData();

        if(!File.Exists(FilePath)) return;

        try
        {
            JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), _current);
        }
        catch(Exception exception)
        {
            Debug.LogWarning($"[설정] 불러오지 못해 기본값을 씁니다 : {exception.Message}");
        }
    }

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 값 · 구독이 남지 않게 한다. 다음에 읽을 때 파일에서 다시 불러온다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        _current = null;
        Changed = null;
    }
}

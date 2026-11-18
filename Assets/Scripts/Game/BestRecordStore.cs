using System;
using System.IO;
using UnityEngine;

// 최고 기록 하나를 JSON 파일로 불러오고 저장한다. 설정 파일과 따로 둔다.
// 결과 화면에서 타이틀로 갈 때 저장을 시도하고, 기존 최고 점수보다 높을 때만 바꾼다. 다른 경로(Esc 메뉴 · 웨이브 전멸)의 타이틀 이동은 저장하지 않는다.
// 처음 읽을 때 파일에서 불러온다. 파일이 없으면 기록 없음(null)이고, 읽을 수 없으면 경고를 남기고 기록 없음으로 둔다.
// 저장 위치는 실행 환경마다 정해진 사용자 데이터 폴더(Application.persistentDataPath)다.
public static class BestRecordStore
{
    private const string FILE_NAME = "best_record.json";

    private static BestRecord _current;
    private static bool _isLoaded;

    private static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    // 저장된 최고 기록. 없으면 null 이다.
    public static BestRecord Current
    {
        get
        {
            if(!_isLoaded) Load();

            return _current;
        }
    }

    // record 가 기존 최고 점수보다 높으면 최고 기록으로 바꾸고 저장한 뒤 true 를 돌려준다.
    public static bool TrySave(BestRecord record)
    {
        if(Current != null && record.Score <= Current.Score) return false;

        _current = record;

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(record, true));
        }
        catch(Exception exception)
        {
            Debug.LogWarning($"[최고 기록] 저장하지 못했습니다 : {exception.Message}");
        }

        return true;
    }

    private static void Load()
    {
        _isLoaded = true;
        _current = null;

        if(!File.Exists(FilePath)) return;

        try
        {
            _current = JsonUtility.FromJson<BestRecord>(File.ReadAllText(FilePath));
        }
        catch(Exception exception)
        {
            Debug.LogWarning($"[최고 기록] 불러오지 못해 기록 없음으로 둡니다 : {exception.Message}");
        }
    }

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 다음에 읽을 때 파일에서 다시 불러오게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        _current = null;
        _isLoaded = false;
    }
}

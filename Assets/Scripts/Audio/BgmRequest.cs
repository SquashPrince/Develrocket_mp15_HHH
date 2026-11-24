using System;
using UnityEngine;

// Inspector 에 순서 번호로 저장되므로, 새 종류는 항상 맨 뒤에 추가한다. None 은 씬을 옮기는 동안 배경음을 끌 때 쓴다.
public enum BgmTrack
{
    Title,
    Wave,
    Boss,
    Result,
    None,
    Loading
}

// 배경음 곡 변경 요청. 곡을 정하는 쪽은 씬마다, 재생 기능은 공용 UI 에 있어 서로 몰라도 되는 공용 알림으로 둔다.
public static class BgmRequest
{
    // 구독하는 쪽은 꺼질 때 반드시 해제한다.
    public static event Action<BgmTrack> Requested;

    public static void Play(BgmTrack track) => Requested?.Invoke(track);

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 구독이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Requested = null;
    }
}

using System;
using UnityEngine;

// 배경음 곡 종류. 곡 파일은 배경음 재생 기능에서 종류마다 정한다.
// Inspector 에 저장된 값은 순서 번호이므로, 새 종류는 항상 맨 뒤에 추가한다.
public enum BgmTrack
{
    Title,
    Wave,
    Boss,
    Result
}

// 배경음 곡 변경 요청. 화면마다 곡을 정하는 쪽(타이틀 · 게임 흐름)이 "이 곡으로 바꿔 줘" 라고 알리면, 공용 UI 의 배경음 재생 기능이 받아서 바꾼다.
// 곡을 정하는 쪽은 씬마다 있고 재생 기능은 씬 전환에도 남는 공용 UI 에 있어, 서로 몰라도 되는 공용 알림으로 둔다.
public static class BgmRequest
{
    // 바꿀 곡 종류를 알린다. 구독하는 쪽은 꺼질 때 반드시 해제한다.
    public static event Action<BgmTrack> Requested;

    public static void Play(BgmTrack track) => Requested?.Invoke(track);

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 구독이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Requested = null;
    }
}

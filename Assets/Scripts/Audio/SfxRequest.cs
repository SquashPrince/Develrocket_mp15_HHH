using System;
using UnityEngine;

// 효과음 재생 요청. 소리를 내는 쪽이 재생 기능을 몰라도 되고, 공용 UI 가 없는 테스트 씬에서도 오류 없이 소리만 나지 않게 공용 알림으로 둔다.
public static class SfxRequest
{
    // 구독하는 쪽은 꺼질 때 반드시 해제한다.
    public static event Action<SoundGroup> Requested;

    // 목록이 비어 있는 소리 묶음은 알리지 않는다.
    public static void Play(SoundGroup sound)
    {
        if(!sound.HasClip) return;

        Requested?.Invoke(sound);
    }

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 구독이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Requested = null;
    }
}

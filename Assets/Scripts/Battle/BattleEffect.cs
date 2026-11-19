using System;
using UnityEngine;

// 전투 이펙트 재생 요청. 이펙트 Prefab 을 들고 있는 쪽(액션 · 공통 설정 등)이 "이 위치에 이 이펙트를 띄워 줘" 라고 알리면, 씬의 이펙트 풀이 받아서 띄운다.
// 띄우는 쪽이 풀을 몰라도 되게 하고, 풀이 없는 테스트 씬에서도 오류 없이 이펙트만 나오지 않게 하기 위해서다.
// 효과음 재생 요청과 같은 공용 알림 방식이다.
public static class BattleEffect
{
    // 띄울 이펙트 Prefab · 월드 위치 · 월드 회전을 알린다. 구독하는 쪽은 꺼질 때 반드시 해제한다.
    public static event Action<GameObject, Vector3, Quaternion> Requested;

    // 비워 둔(null) 이펙트는 알리지 않는다.
    public static void Play(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if(prefab == null) return;

        Requested?.Invoke(prefab, position, rotation);
    }

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 구독이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Requested = null;
    }
}

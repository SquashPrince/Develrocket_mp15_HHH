using System;
using UnityEngine;

// 전투 이펙트 재생 요청. 띄우는 쪽이 풀을 몰라도 되고, 풀이 없는 테스트 씬에서도 오류 없이 이펙트만 나오지 않게 공용 알림으로 둔다.
public static class BattleEffect
{
    // 구독하는 쪽은 꺼질 때 반드시 해제한다.
    public static event Action<GameObject, Vector3, Quaternion> Requested;
    public static event Action<GameObject, Transform, Vector3, Quaternion, Vector3> AttachedRequested;
    public static event Action<Transform> LoopStopRequested;

    // 비워 둔(null) 이펙트는 알리지 않는다.
    public static void Play(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if(prefab == null) return;

        Requested?.Invoke(prefab, position, rotation);
    }

    // 베기처럼 캐릭터를 따라가야 하는 이펙트에 쓴다.
    public static void PlayAttached(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        if(prefab == null) return;

        AttachedRequested?.Invoke(prefab, parent, localPosition, localRotation, localScale);
    }

    // 새 파티클 생성만 멈추고, 이미 나온 파티클은 수명대로 사라진 뒤 회수된다.
    public static void StopLoop(Transform parent)
    {
        LoopStopRequested?.Invoke(parent);
    }

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 구독이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Requested = null;
        AttachedRequested = null;
        LoopStopRequested = null;
    }
}

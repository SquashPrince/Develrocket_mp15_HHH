using System;
using UnityEngine;

// 전투 연출이 반응할 순간을 알린다. 플레이어 쪽은 순간이 일어났다는 것만 알리고, 카메라 · 화면 등 반응하는 쪽이 각자 구독한다.
// 반응하는 쪽(카메라 등)은 씬 오브젝트이고 알리는 쪽은 캐릭터마다 있어, 서로 몰라도 되는 공용 알림으로 둔다.
public static class BattleFeedback
{
    public enum Moment
    {
        // 강타가 적에게 맞음. 한 번의 판정에서 여러 적을 맞혀도 한 번만 알린다.
        ImpactHit,
        ParrySuccess,
        PerfectDodge,
        // 조작 중인 캐릭터가 넉백됨
        Knockback
    }

    // 순간이 일어날 때 알린다. 구독하는 쪽은 꺼질 때 반드시 해제한다.
    public static event Action<Moment> Occurred;

    public static void Raise(Moment moment)
    {
#if UNITY_EDITOR
        Debug.Log($"[전투 연출] {moment}");
#endif

        Occurred?.Invoke(moment);
    }

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 구독이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Occurred = null;
    }
}

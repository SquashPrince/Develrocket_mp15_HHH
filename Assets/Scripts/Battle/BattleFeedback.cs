using System;
using UnityEngine;

// 전투 연출이 반응할 순간을 알린다. 알리는 쪽은 캐릭터마다, 반응하는 쪽(카메라 · 히트스톱 · 효과음)은 씬에 있어 서로 몰라도 되는 공용 알림으로 둔다.
// 순간마다 반응할지는 반응하는 쪽이 정한다.
// 자세한 흐름 : Docs/Technical/Battle/BattleFeedback.md
public static class BattleFeedback
{
    // 새 순간은 맨 뒤에 더한다. 효과음 설정 에셋 등이 순간을 번호로 저장하므로, 중간에 끼우면 저장된 값이 다른 순간을 가리킨다.
    public enum Moment
    {
        // 강타가 적에게 맞음. 한 번의 판정에서 여러 적을 맞혀도 한 번만 알린다.
        ImpactHit,
        // 패링 준비를 시작함 (적 앞으로 순간 이동해 자세를 잡는 순간)
        ParryStart,
        ParrySuccess,
        PerfectDodge,
        // 조작 중인 캐릭터가 넉백됨
        Knockback,
        // 강타가 아닌 공격이 적에게 맞음. 강타와 같이 한 번의 판정에 한 번만 알린다.
        NormalHit,
        // 조작 중인 캐릭터가 넉백이 아닌 피격(피격 · 큰 피격)을 당함
        Hurt,
        // 적이 처치됨
        EnemyKilled,
        // 조작 중인 캐릭터가 대시를 시작함 (방향 입력 있음)
        Dash,
        // 조작 중인 캐릭터가 회피를 시작함 (방향 입력 없이 뒤로)
        Dodge,
        // 조작 중인 캐릭터의 발이 땅에 닿음. 걷기 · 달리기 클립의 이벤트가 알린다
        Footstep,
        // 콤보 스킬 캐릭터 선택 화면이 열림 (연계마다)
        ComboSelectOpened,
        // 콤보 스킬 캐릭터 선택 화면이 닫힘 (고르기 · 취소 · 시간 초과 · 꺼짐)
        ComboSelectClosed,
        // 적의 패링할 수 있는 동작이 예고됨 (동작마다 패링 가능 시간이 열릴 때. 연속 패링 중에도 동작마다)
        ParryCue,
        // 웨이브의 마지막 적이 처치됨 (게임 전체가 잠깐 느려짐)
        WaveFinish,
        // 보스전의 보스가 처치됨 (게임 전체가 멈춘 뒤 클리어)
        BossFinish,
        // 판이 실패로 끝남 (파티 전멸 · 보스전 시간 초과)
        RunFailed
    }

    // 순간과 그 순간의 충격 방향을 알린다. 구독하는 쪽은 꺼질 때 반드시 해제한다.
    // 충격 방향 : 조작 캐릭터가 밀리는 월드 방향(적 → 캐릭터). 방향이 없는 순간은 Vector3.zero 다.
    public static event Action<Moment, Vector3> Occurred;

    // 플레이어 공격이 적에게 대미지를 줄 때마다 알린다. 대미지 숫자 표시가 쓴다.
    // 맞은 적 · 맞은 위치(월드, 타격 이펙트와 같은 점) · 대미지 · 강타 여부 · 크리티컬 여부. 한 번의 판정에서 여러 적을 맞히면 적마다 알린다.
    public static event Action<Transform, Vector3, float, bool, bool> DamageDealt;

    public static void Raise(Moment moment) => Raise(moment, Vector3.zero);

    public static void RaiseDamage(Transform target, Vector3 hitPoint, float amount, bool isImpact, bool isCritical) => DamageDealt?.Invoke(target, hitPoint, amount, isImpact, isCritical);

    public static void Raise(Moment moment, Vector3 direction)
    {
#if UNITY_EDITOR
        // 발자국은 아주 자주 와서 로그를 남기지 않는다.
        if(moment != Moment.Footstep) Debug.Log($"[전투 연출] {moment}");
#endif

        Occurred?.Invoke(moment, direction);
    }

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 구독이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Occurred = null;
        DamageDealt = null;
    }
}

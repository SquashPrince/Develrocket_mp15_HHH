using System;
using System.Collections.Generic;
using UnityEngine;

// 지금 패링 가능 시간을 열어 둔 적 목록. 적은 패링 공격의 예고를 시작할 때 열고, 판정 · 경직 때 닫는다.
// 플레이어는 교체 키를 누를 때 록온 사거리 안에 연 적이 있으면 교체된 캐릭터가 패링하고(지원 공격), 그중 한 적 앞으로 순간 이동한다.
// 열고 닫힐 때 알려서, 플레이어 쪽이 적을 알지 않아도 예고 표시를 띄우고 지울 수 있게 한다.
// 연속 패링 : 이어가기 동작이 있는 공격(연속 패링 패턴)을 하는 동안 적이 시작 · 끝을 알린다.
//   패턴 중에는 플레이어가 대기 캐릭터가 없어도 누른 방향의 파티원으로 다시 패링할 수 있고, 예고도 패링 가능으로 뜬다.
//   공격이 끝나면(마지막 패링 성공 경직 · 그로기 · 처치 · 공격 끝) 적이 끝을 알려 교체 규칙이 원래대로 돌아간다.
//   나중에 전투 관리 기능이 생기면 그쪽으로 옮길 수 있다.
public static class ParryWindow
{
    private static readonly List<IEnemyParryable> _openAttackers = new List<IEnemyParryable>();
    // 지금 연속 패링 패턴을 하는 적 목록. 여러 적이 동시에 할 수 있어 목록으로 센다.
    private static readonly List<IEnemyParryable> _chainAttackers = new List<IEnemyParryable>();

    // 연속 패링 패턴을 하는 적이 하나라도 있는지
    public static bool IsChainActive => _chainAttackers.Count > 0;

    // 적이 패링 가능 시간을 열 때 · 닫을 때 알린다. 구독하는 쪽은 꺼질 때 반드시 해제한다.
    public static event Action<IEnemyParryable> Opened;
    public static event Action<IEnemyParryable> Closed;

    // position 에서 range 안에 있는 적 중 canParry 를 만족하며 가장 최근에 패링 가능 시간을 연 적. 없으면 null 이다.
    // 나중에 연 적이 목록 뒤에 있으므로 뒤에서부터 찾는다.
    // canParry : 플레이어 쪽이 그 적을 패링할 수 있는지 (예고가 뜬 순간 패링 불가였던 적은 뺀다)
    public static IEnemyParryable FindLatestWithin(Vector3 position, float range, Predicate<IEnemyParryable> canParry)
    {
        float sqrRange = range * range;

        for(int i = _openAttackers.Count - 1; i >= 0; i--)
        {
            IEnemyParryable attacker = _openAttackers[i];

            if((attacker.transform.position - position).sqrMagnitude <= sqrRange && canParry(attacker)) return attacker;
        }

        return null;
    }

    public static void Open(IEnemyParryable attacker)
    {
        if(_openAttackers.Contains(attacker)) return;

        _openAttackers.Add(attacker);
        Opened?.Invoke(attacker);
    }

    public static void Close(IEnemyParryable attacker)
    {
        if(!_openAttackers.Remove(attacker)) return;

        Closed?.Invoke(attacker);
    }

    // 적이 연속 패링 패턴 공격을 시작할 때 알린다.
    public static void BeginChain(IEnemyParryable attacker)
    {
        if(!_chainAttackers.Contains(attacker)) _chainAttackers.Add(attacker);
    }

    // 적의 연속 패링 패턴 공격이 끝나거나 끊길 때 알린다.
    public static void EndChain(IEnemyParryable attacker) => _chainAttackers.Remove(attacker);

    // 씬을 바꾸기 전에 목록을 비운다. 사라질 적이 목록에 남아 다음 씬에서 파괴된 적을 찾지 않게 한다.
    // 구독은 각 오브젝트가 꺼질 때 스스로 해제하므로 건드리지 않는다.
    public static void ClearAttackers()
    {
        _openAttackers.Clear();
        _chainAttackers.Clear();
    }

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 목록 · 구독이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _openAttackers.Clear();
        _chainAttackers.Clear();
        Opened = null;
        Closed = null;
    }
}

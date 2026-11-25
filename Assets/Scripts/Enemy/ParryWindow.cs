using System;
using System.Collections.Generic;
using UnityEngine;

// 지금 패링 가능 시간을 열어 둔 적과 연속 패링 패턴 중인 적 목록.
// 열고 닫힐 때 알려서, 플레이어 쪽이 적을 몰라도 예고 표시를 띄우고 지울 수 있게 한다.
public static class ParryWindow
{
    private static readonly List<IEnemyParryable> _openAttackers = new List<IEnemyParryable>();
    // 여러 적이 동시에 할 수 있어 목록으로 센다.
    private static readonly List<IEnemyParryable> _chainAttackers = new List<IEnemyParryable>();

    // 패턴 중에는 대기 캐릭터가 없어도 필드의 파티원으로 다시 패링할 수 있다.
    public static bool IsChainActive => _chainAttackers.Count > 0;

    // 구독하는 쪽은 꺼질 때 반드시 해제한다.
    public static event Action<IEnemyParryable> Opened;
    public static event Action<IEnemyParryable> Closed;

    // 나중에 연 적이 목록 뒤에 있으므로 뒤에서부터 찾는다.
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

    public static void BeginChain(IEnemyParryable attacker)
    {
        if(!_chainAttackers.Contains(attacker)) _chainAttackers.Add(attacker);
    }

    public static void EndChain(IEnemyParryable attacker) => _chainAttackers.Remove(attacker);

    // 다음 씬에서 파괴된 적을 찾지 않게 씬을 바꾸기 전에 비운다. 구독은 각 오브젝트가 스스로 해제하므로 건드리지 않는다.
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

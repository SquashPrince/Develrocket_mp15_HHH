using System.Collections.Generic;
using UnityEngine;

// 지금 패링 가능 시간을 열어 둔 적 목록. 적은 패링 공격의 예고를 시작할 때 열고, 판정 · 경직 때 닫는다.
// 플레이어는 패링 키를 누를 때 하나라도 열려 있으면 패링을 시작하고, 가장 최근에 연 적 앞으로 순간 이동한다.
public static class ParryWindow
{
    private static readonly List<DummyEnemyAttacker> _openAttackers = new List<DummyEnemyAttacker>();

    public static bool IsOpen => _openAttackers.Count > 0;

    // 가장 최근에 패링 가능 시간을 연 적. 열린 적이 없으면 null 이다.
    public static DummyEnemyAttacker LatestAttacker => IsOpen ? _openAttackers[_openAttackers.Count - 1] : null;

    public static bool IsOpenBy(DummyEnemyAttacker attacker) => _openAttackers.Contains(attacker);

    public static void Open(DummyEnemyAttacker attacker)
    {
        if(!_openAttackers.Contains(attacker)) _openAttackers.Add(attacker);
    }

    public static void Close(DummyEnemyAttacker attacker) => _openAttackers.Remove(attacker);

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 목록이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => _openAttackers.Clear();
}

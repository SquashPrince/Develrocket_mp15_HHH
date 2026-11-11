using System.Collections.Generic;
using UnityEngine;

// 지금 패링 가능 시간을 열어 둔 적 목록. 적은 패링 공격의 예고를 시작할 때 열고, 판정 · 경직 때 닫는다.
// 플레이어는 패링 키를 누를 때 록온 사거리 안에 연 적이 있으면 패링을 시작하고, 그중 한 적 앞으로 순간 이동한다.
public static class ParryWindow
{
    private static readonly List<IEnemyParryable> _openAttackers = new List<IEnemyParryable>();

    // position 에서 range 안에 있는 적 중 가장 최근에 패링 가능 시간을 연 적. 없으면 null 이다.
    // 나중에 연 적이 목록 뒤에 있으므로 뒤에서부터 찾는다.
    public static IEnemyParryable FindLatestWithin(Vector3 position, float range)
    {
        float sqrRange = range * range;

        for(int i = _openAttackers.Count - 1; i >= 0; i--)
        {
            IEnemyParryable attacker = _openAttackers[i];

            if((attacker.transform.position - position).sqrMagnitude <= sqrRange) return attacker;
        }

        return null;
    }

    public static void Open(IEnemyParryable attacker)
    {
        if(!_openAttackers.Contains(attacker)) _openAttackers.Add(attacker);
    }

    public static void Close(IEnemyParryable attacker) => _openAttackers.Remove(attacker);

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 목록이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => _openAttackers.Clear();
}

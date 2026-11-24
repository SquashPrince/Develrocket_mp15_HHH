using UnityEngine;

// 대상 몸의 수평 반지름. 순간 이동 위치와 공격 정지 거리를 몸 표면 기준으로 정할 때 쓴다.
public static class BodyRadius
{
    // 적은 시작할 때 재 둔 반지름을 쓴다. 연출 · 도약 중 콜라이더가 꺼져도 0 이 되지 않게 하기 위해서다.
    public static float GetHorizontal(Transform target)
    {
        if(target.TryGetComponent(out IEnemyTargetable enemy)) return enemy.Radius;

        return target.TryGetComponent(out Collider targetCollider) ? Measure(targetCollider) : 0f;
    }

    // 모양과 상관없이 경계 상자의 가로 · 세로 중 큰 쪽 절반. 콜라이더가 꺼져 있으면 0 이다.
    public static float Measure(Collider bodyCollider)
    {
        Vector3 extents = bodyCollider.bounds.extents;

        return Mathf.Max(extents.x, extents.z);
    }
}

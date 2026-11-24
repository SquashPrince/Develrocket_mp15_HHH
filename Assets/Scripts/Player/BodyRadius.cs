using UnityEngine;

// 대상 몸의 수평 반지름을 재는 공통 규칙. 순간 이동 위치와 공격 정지 거리를 몸 표면 기준으로 정할 때 함께 쓴다.
// 적은 시작할 때 재 둔 자기 반지름을 알려주므로(IEnemyTargetable) 그 값을 먼저 쓴다. 연출 · 도약 중 콜라이더가 꺼져도 0 이 되지 않는다.
// 적이 아닌 대상은 지금 콜라이더의 경계 상자(bounds)에서 구한다.
public static class BodyRadius
{
    public static float GetHorizontal(Transform target)
    {
        if(target.TryGetComponent(out IEnemyTargetable enemy)) return enemy.Radius;

        return target.TryGetComponent(out Collider targetCollider) ? Measure(targetCollider) : 0f;
    }

    // 콜라이더의 수평 반지름. 콜라이더 모양(캡슐 · 박스 등)과 상관없이 월드 기준 경계 상자의 가로 · 세로 중 큰 쪽 절반을 쓴다.
    // 콜라이더가 꺼져 있으면 bounds 크기가 0 이라 0 이 된다.
    public static float Measure(Collider bodyCollider)
    {
        Vector3 extents = bodyCollider.bounds.extents;

        return Mathf.Max(extents.x, extents.z);
    }
}

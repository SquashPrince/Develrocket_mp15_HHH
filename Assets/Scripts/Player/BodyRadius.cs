using UnityEngine;

// 대상 몸의 수평 반지름을 재는 공통 규칙. 순간 이동 위치와 공격 정지 거리를 몸 표면 기준으로 정할 때 함께 쓴다.
// 지금은 대상 콜라이더의 경계 상자(bounds)에서 구한다.
// 나중에 적이 자기 반지름을 알려주게 되면(적 계약), 그 값을 먼저 쓰도록 이 클래스 안에서만 바꾼다. 부르는 쪽은 그대로 둔다.
public static class BodyRadius
{
    // 대상 콜라이더의 수평 반지름. 콜라이더 모양(캡슐 · 박스 등)과 상관없이 월드 기준 경계 상자의 가로 · 세로 중 큰 쪽 절반을 쓴다.
    // 콜라이더가 없거나 꺼져 있으면(도약 중인 보스 등) bounds 크기가 0 이라 0 이 된다.
    public static float GetHorizontal(Transform target)
    {
        if(!target.TryGetComponent(out Collider targetCollider)) return 0f;

        Vector3 extents = targetCollider.bounds.extents;

        return Mathf.Max(extents.x, extents.z);
    }
}

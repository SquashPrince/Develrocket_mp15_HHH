using UnityEngine;

// 판정 범위 하나의 값과 범위 안의 콜라이더 찾기 · Scene 창 그리기. 플레이어 · 적 동작이 함께 쓴다.
// 위치 · 방향은 공격하는 쪽(origin) 기준이다. 상자 · 캡슐은 모양 자체에 높이가 있어 높이 제한은 부채꼴에만 있다.
public readonly struct HitRange
{
    // 부채꼴 Gizmo 의 호를 나누는 칸 수
    private const int FAN_GIZMO_SEGMENTS = 16;

    private readonly HitShape _shape;
    private readonly Vector3 _center;
    private readonly Vector3 _size;
    private readonly float _radius;
    private readonly float _length;
    private readonly float _angle;
    private readonly float _innerRadius;
    private readonly float _height;

    public HitRange(HitShape shape, Vector3 center, Vector3 size, float radius, float length, float angle, float innerRadius, float height)
    {
        _shape = shape;
        _center = center;
        _size = size;
        _radius = radius;
        _length = length;
        _angle = angle;
        _innerRadius = innerRadius;
        _height = height;
    }

    // 판정 범위의 중심 (공격하는 쪽 기준 위치). 적중 이펙트가 휘두르는 높이를 읽는다.
    public Vector3 Center => _center;

    // 범위 안의 콜라이더를 results 에 담고 찾은 수를 돌려준다.
    public int Overlap(Transform origin, Collider[] results, int layers)
    {
        Vector3 worldCenter = origin.TransformPoint(_center);

        switch(_shape)
        {
            case HitShape.Sphere:
                return Physics.OverlapSphereNonAlloc(worldCenter, _radius, results, layers, QueryTriggerInteraction.Ignore);
            case HitShape.Capsule:
                GetCapsulePoints(origin, out Vector3 start, out Vector3 end);
                return Physics.OverlapCapsuleNonAlloc(start, end, _radius, results, layers, QueryTriggerInteraction.Ignore);
            case HitShape.Fan:
                return FilterFan(origin, worldCenter, results, Physics.OverlapSphereNonAlloc(worldCenter, _radius, results, layers, QueryTriggerInteraction.Ignore));
            default:
                return Physics.OverlapBoxNonAlloc(worldCenter, _size * 0.5f, results, origin.rotation, layers, QueryTriggerInteraction.Ignore);
        }
    }

    // 한 점이 범위 안인지 본다. 패링 순간 이동이 적 공격 범위 안의 자리를 고를 때 쓴다.
    // 콜라이더 찾기와 같은 모양 · 방향 기준이다. 부채꼴은 점 하나라 경계 대신 점의 높이 · 거리 · 각도를 본다.
    public bool Contains(Transform origin, Vector3 point)
    {
        Vector3 worldCenter = origin.TransformPoint(_center);

        switch(_shape)
        {
            case HitShape.Sphere:
                return (point - worldCenter).sqrMagnitude <= _radius * _radius;
            case HitShape.Capsule:
                GetCapsulePoints(origin, out Vector3 start, out Vector3 end);
                return GetSqrDistanceToSegment(point, start, end) <= _radius * _radius;
            case HitShape.Fan:
                return IsInFan(origin, worldCenter, point);
            default:
                Vector3 local = Quaternion.Inverse(origin.rotation) * (point - worldCenter);
                Vector3 half = _size * 0.5f;
                return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
    }

    // Scene 창에 범위를 그린다. 색은 부르는 쪽이 정한다.
    public void DrawGizmos(Transform origin)
    {
        Vector3 worldCenter = origin.TransformPoint(_center);

        switch(_shape)
        {
            case HitShape.Sphere:
                Gizmos.DrawWireSphere(worldCenter, _radius);
                break;
            case HitShape.Capsule:
                DrawCapsule(origin);
                break;
            case HitShape.Fan:
                DrawFan(origin, worldCenter);
                break;
            default:
                Gizmos.matrix = Matrix4x4.TRS(worldCenter, origin.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, _size);
                Gizmos.matrix = Matrix4x4.identity;
                break;
        }
    }

    // 부채꼴 조건에 맞는 콜라이더만 results 앞쪽에 남기고 남은 수를 돌려준다.
    private int FilterFan(Transform origin, Vector3 worldCenter, Collider[] results, int count)
    {
        Vector3 forward = HorizontalVector.Flatten(origin.forward);
        float halfAngle = _angle * 0.5f;
        float bottom = worldCenter.y - _height * 0.5f;
        float top = worldCenter.y + _height * 0.5f;
        int kept = 0;

        for(int i = 0; i < count; i++)
        {
            Bounds bounds = results[i].bounds;
            Vector3 toTarget = HorizontalVector.Flatten(bounds.center - worldCenter);

            if(bounds.max.y < bottom || bounds.min.y > top) continue;
            if(toTarget.magnitude < _innerRadius) continue;
            if(toTarget != Vector3.zero && Vector3.Angle(forward, toTarget) > halfAngle) continue;

            results[kept] = results[i];
            kept++;
        }

        return kept;
    }

    // 찾기용 구(바깥 반지름) 안이면서, 높이 범위 · 안쪽 반지름 · 정면 기준 수평 각도 조건을 콜라이더 거르기와 같게 본다.
    private bool IsInFan(Transform origin, Vector3 worldCenter, Vector3 point)
    {
        if((point - worldCenter).sqrMagnitude > _radius * _radius) return false;
        if(Mathf.Abs(point.y - worldCenter.y) > _height * 0.5f) return false;

        Vector3 toPoint = HorizontalVector.Flatten(point - worldCenter);

        if(toPoint.magnitude < _innerRadius) return false;

        return toPoint == Vector3.zero || Vector3.Angle(HorizontalVector.Flatten(origin.forward), toPoint) <= _angle * 0.5f;
    }

    private static float GetSqrDistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float sqrLength = segment.sqrMagnitude;
        float t = sqrLength > 0f ? Mathf.Clamp01(Vector3.Dot(point - start, segment) / sqrLength) : 0f;

        return (point - (start + segment * t)).sqrMagnitude;
    }

    // 캡슐의 양 끝 구 중심. 앞쪽 방향으로 전체 길이만큼 뻗고, 길이가 반지름의 두 배보다 짧으면 두 점이 겹쳐 구가 된다.
    private void GetCapsulePoints(Transform origin, out Vector3 start, out Vector3 end)
    {
        Vector3 center = origin.TransformPoint(_center);
        float halfSegment = Mathf.Max(0f, _length * 0.5f - _radius);

        start = center - origin.forward * halfSegment;
        end = center + origin.forward * halfSegment;
    }

    // 캡슐은 양 끝 구와 그 사이를 잇는 선으로 그린다.
    private void DrawCapsule(Transform origin)
    {
        GetCapsulePoints(origin, out Vector3 start, out Vector3 end);

        Gizmos.DrawWireSphere(start, _radius);
        Gizmos.DrawWireSphere(end, _radius);
        Gizmos.DrawLine(start + origin.right * _radius, end + origin.right * _radius);
        Gizmos.DrawLine(start - origin.right * _radius, end - origin.right * _radius);
        Gizmos.DrawLine(start + origin.up * _radius, end + origin.up * _radius);
        Gizmos.DrawLine(start - origin.up * _radius, end - origin.up * _radius);
    }

    // 부채꼴은 위 · 아래 면의 바깥 호 · 안쪽 호 · 양옆 선과, 네 모서리를 잇는 세로선으로 그린다.
    private void DrawFan(Transform origin, Vector3 worldCenter)
    {
        Vector3 forward = HorizontalVector.Flatten(origin.forward);
        Vector3 up = Vector3.up * (_height * 0.5f);

        DrawFanFace(worldCenter + up, forward);
        DrawFanFace(worldCenter - up, forward);

        foreach(float side in new[] { -0.5f, 0.5f })
        {
            Vector3 direction = Quaternion.AngleAxis(_angle * side, Vector3.up) * forward;

            Gizmos.DrawLine(worldCenter - up + direction * _radius, worldCenter + up + direction * _radius);
            Gizmos.DrawLine(worldCenter - up + direction * _innerRadius, worldCenter + up + direction * _innerRadius);
        }
    }

    private void DrawFanFace(Vector3 center, Vector3 forward)
    {
        Vector3 previousOuter = Vector3.zero;
        Vector3 previousInner = Vector3.zero;

        for(int i = 0; i <= FAN_GIZMO_SEGMENTS; i++)
        {
            float angle = Mathf.Lerp(-_angle * 0.5f, _angle * 0.5f, (float)i / FAN_GIZMO_SEGMENTS);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            Vector3 outer = center + direction * _radius;
            Vector3 inner = center + direction * _innerRadius;

            if(i > 0)
            {
                Gizmos.DrawLine(previousOuter, outer);
                Gizmos.DrawLine(previousInner, inner);
            }

            // 양 끝에서 안쪽과 바깥을 잇는다.
            if(i == 0 || i == FAN_GIZMO_SEGMENTS) Gizmos.DrawLine(inner, outer);

            previousOuter = outer;
            previousInner = inner;
        }
    }
}

using UnityEngine;

// 대상 앞으로 순간 이동하는 공통 규칙. 필살기 · 콤보 스킬 · 빠른 지원이 같은 계산과 간격을 쓴다.
// 교체 패링은 공격이 들어와야 성립하므로 따로 적의 공격 범위 안에서 자리를 고른다.
// 실제로 다가가면 벽 등에 걸리므로 위치를 바로 옮기고, 서려던 자리가 막혀 있으면 같은 거리의 둘레에서 빈자리를 찾는다.
// 기억하는 값이 없는 계산이라 static 클래스로 둔다.
public static class TeleportManager
{
    // 최대 각도면 대상의 반대편까지 본다.
    private const float SEARCH_ANGLE_STEP = 20f;
    private const float SEARCH_ANGLE_MAX = 180f;

    // 교체 패링 후보의 좌우 간격 (몸 지름 배수). 가운데부터 좌우로 넓힌다.
    private static readonly float[] PARRY_SIDE_OFFSETS = { 0f, 1f, -1f, 2f, -2f };

    // 옵션이 비어 있으면(순간 이동하지 않는 액션) false 를 돌려준다.
    public static bool TryTeleportTo(PlayerController character, Transform target, TeleportOption option)
    {
        if(option == null) return false;

        return TryTeleportTo(character, target, option.StandSide, option.PickSideAngle());
    }

    // 간격은 몸 표면 사이 기준이다. 중심 기준이면 큰 적(보스)의 몸 안에 서서 겹침을 풀며 바닥 아래로 밀려난다.
    // sideAngle : 위에서 볼 때 시계 방향이 +
    private static bool TryTeleportTo(PlayerController character, Transform target, TeleportOption.Side side, float sideAngle)
    {
        CommonCharacterSettings common = character.Common;
        CharacterController body = character.Body;
        Vector3 targetPosition = target.position;
        Vector3 direction = HorizontalVector.Flatten(side == TeleportOption.Side.TargetFront ? target.forward : character.transform.position - targetPosition);
        direction = (Quaternion.Euler(0f, sideAngle, 0f) * direction).normalized;

        float distance = BodyRadius.GetHorizontal(target) + body.radius + common.AttackableDistance;

        // 옆 각도로 비켜 선 쪽으로 먼저 넓혀, 원래 비켜 서려던 쪽에 가까운 자리를 고르게 한다.
        if(!TryFindDestination(common, body, targetPosition, direction, distance, sideAngle >= 0f ? 1f : -1f, out Vector3 destination))
        {
#if UNITY_EDITOR
            LogFailure(common, body, target, targetPosition + direction * distance);
#endif
            return false;
        }

        PlaceAt(character, destination, targetPosition);
        return true;
    }

    // 교체 패링 : 이전 조작 캐릭터(guarded) 바로 앞에서 적 쪽으로 몸 반지름만큼씩 나아가며, 좌우 후보까지 보고 처음으로 맞는 자리에 선다.
    // 조건 : 몸 중심이 적의 지금 공격 범위 안, 바닥이 있음, 막히지 않음, 적 몸과 겹치지 않음.
    // 이전 캐릭터와 적 사이에 서서 막아 주는 모습이 되고, 범위 안이라 공격이 들어와 패링이 성립한다. 맞는 자리가 없으면 이전 캐릭터 위치에 선다.
    public static void TeleportForParry(PlayerController character, Transform guarded, IEnemyParryable attacker)
    {
        Vector3 attackerPosition = attacker.transform.position;
        Vector3 destination = TryFindParrySpot(character, guarded.position, attacker, out Vector3 spot) ? spot : guarded.position;

        PlaceAt(character, destination, attackerPosition);
    }

    private static bool TryFindParrySpot(PlayerController character, Vector3 guardedPosition, IEnemyParryable attacker, out Vector3 spot)
    {
        CommonCharacterSettings common = character.Common;
        CharacterController body = character.Body;
        Vector3 attackerPosition = attacker.transform.position;
        Vector3 toAttacker = HorizontalVector.Between(guardedPosition, attackerPosition);
        float minAttackerDistance = BodyRadius.GetHorizontal(attacker.transform) + body.radius;
        float diameter = body.radius * 2f;

        spot = Vector3.zero;

        if(toAttacker == Vector3.zero || body.radius <= 0f) return false;

        Vector3 forward = toAttacker.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, forward);

        // 이전 캐릭터와 겹치지 않는 바로 앞부터 적 몸에 닿기 전까지 본다.
        for(float distance = diameter; distance <= toAttacker.magnitude - minAttackerDistance; distance += body.radius)
        {
            foreach(float sideOffset in PARRY_SIDE_OFFSETS)
            {
                Vector3 candidate = guardedPosition + forward * distance + side * (sideOffset * diameter);

                if(IsParrySpot(common, body, attacker, attackerPosition, minAttackerDistance, ref candidate))
                {
                    spot = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    // 바닥에 맞춘 뒤의 위치로 범위 · 막힘을 본다.
    private static bool IsParrySpot(CommonCharacterSettings common, CharacterController body, IEnemyParryable attacker, Vector3 attackerPosition, float minAttackerDistance, ref Vector3 candidate)
    {
        if(HorizontalVector.Between(attackerPosition, candidate).sqrMagnitude < minAttackerDistance * minAttackerDistance) return false;
        if(!TrySnapToGround(common, ref candidate)) return false;
        if(!attacker.IsInAttackRange(GetCenter(body, candidate))) return false;

        return !IsBlocked(common, body, attackerPosition, candidate);
    }

    // CharacterController 가 켜져 있으면 위치를 직접 바꿔도 덮어쓰므로 잠깐 끄고 옮긴다.
    private static void PlaceAt(PlayerController character, Vector3 destination, Vector3 lookPosition)
    {
        CharacterController body = character.Body;

        body.enabled = false;
        character.transform.position = destination;
        body.enabled = true;

        character.FaceImmediately(HorizontalVector.Between(destination, lookPosition));
    }

    // 순서 : 0° → +20° → -20° → +40° → -40° … → 180° (firstSign 이 -1 이면 - 쪽 먼저). 180° 는 양쪽이 같은 자리라 한 번만 본다.
    private static bool TryFindDestination(CommonCharacterSettings common, CharacterController body, Vector3 targetPosition, Vector3 direction, float distance, float firstSign, out Vector3 destination)
    {
        for(float offset = 0f; offset <= SEARCH_ANGLE_MAX; offset += SEARCH_ANGLE_STEP)
        {
            if(TryStandAt(common, body, targetPosition, direction, distance, offset * firstSign, out destination)) return true;
            if(offset > 0f && offset < SEARCH_ANGLE_MAX && TryStandAt(common, body, targetPosition, direction, distance, -offset * firstSign, out destination)) return true;
        }

        destination = Vector3.zero;
        return false;
    }

    private static bool TryStandAt(CommonCharacterSettings common, CharacterController body, Vector3 targetPosition, Vector3 direction, float distance, float angle, out Vector3 destination)
    {
        destination = targetPosition + Quaternion.Euler(0f, angle, 0f) * direction * distance;

        return TrySnapToGround(common, ref destination) && !IsBlocked(common, body, targetPosition, destination);
    }

    // 대상 높이를 그대로 쓰면 도착 지점 바닥이 더 높을 때 발이 땅 속에 들어가 바닥을 뚫고 떨어진다.
    private static bool TrySnapToGround(CommonCharacterSettings common, ref Vector3 destination)
    {
        Vector3 origin = destination + Vector3.up * common.GroundSearchHeight;

        if(!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, common.GroundSearchHeight + common.GroundSearchDepth, common.TeleportGroundLayers, QueryTriggerInteraction.Ignore)) return false;

        destination.y = hit.point.y;
        return true;
    }

    // 몸(캡슐)이 들어갈 자리가 있는지, 대상에서 도착 지점까지 벽이 가로막지 않는지 본다.
    private static bool IsBlocked(CommonCharacterSettings common, CharacterController body, Vector3 targetPosition, Vector3 destination)
    {
        GetCapsule(body, destination, out Vector3 bottom, out Vector3 top);

        if(Physics.CheckCapsule(bottom, top, body.radius, common.TeleportBlockLayers, QueryTriggerInteraction.Ignore)) return true;

        return Physics.Linecast(targetPosition + body.center, GetCenter(body, destination), common.TeleportBlockLayers, QueryTriggerInteraction.Ignore);
    }

    // 바닥에 닿은 것을 겹침으로 보지 않도록 피부 두께만큼 띄운다.
    private static Vector3 GetCenter(CharacterController body, Vector3 destination) => destination + body.center + Vector3.up * body.skinWidth;

    private static void GetCapsule(CharacterController body, Vector3 destination, out Vector3 bottom, out Vector3 top)
    {
        Vector3 center = GetCenter(body, destination);
        float halfHeight = Mathf.Max(0f, body.height * 0.5f - body.radius);

        bottom = center - Vector3.up * halfHeight;
        top = center + Vector3.up * halfHeight;
    }

#if UNITY_EDITOR
    // 원인 확인용. 원래 서려던 자리가 막힌 이유를 남긴다.
    private static void LogFailure(CommonCharacterSettings common, CharacterController body, Transform target, Vector3 destination)
    {
        string reason;

        if(!TrySnapToGround(common, ref destination)) reason = "바닥(순간 이동 바닥 Layer)을 찾지 못함";
        else
        {
            GetCapsule(body, destination, out Vector3 bottom, out Vector3 top);
            Collider[] overlaps = Physics.OverlapCapsule(bottom, top, body.radius, common.TeleportBlockLayers, QueryTriggerInteraction.Ignore);

            if(overlaps.Length > 0) reason = $"{DescribeColliders(overlaps)} 와 겹침";
            else if(Physics.Linecast(target.position + body.center, GetCenter(body, destination), out RaycastHit hit, common.TeleportBlockLayers, QueryTriggerInteraction.Ignore)) reason = $"{DescribeColliders(new[] { hit.collider })} 가 가로막음";
            else reason = "알 수 없음";
        }

        Debug.LogWarning($"[순간 이동 실패] {body.name} → {target.name} : 둘레가 모두 막혀 제자리에 둔다. 원래 자리 {destination} : {reason}", body);
    }

    private static string DescribeColliders(Collider[] colliders) => string.Join(", ", System.Array.ConvertAll(colliders, collider => $"'{collider.name}' (Layer {LayerMask.LayerToName(collider.gameObject.layer)})"));
#endif
}

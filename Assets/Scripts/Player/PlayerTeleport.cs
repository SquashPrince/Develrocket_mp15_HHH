using UnityEngine;

// 대상 앞으로 순간 이동한다. 순간 이동 옵션이 붙은 액션(패링 · 필살기 · 콤보 스킬)이 같은 계산과 같은 간격을 함께 쓴다.
// 어느 쪽에 설지 · 옆으로 얼마나 비켜 설지는 액션의 순간 이동 옵션(TeleportOption)이 정한다.
// 옆으로 비켜 서도 적과의 거리는 같게 두어 모든 캐릭터의 사정거리를 하나로 맞춘다.
// 실제로 다가가면 벽 등에 걸리기 때문에 위치를 바로 옮긴다. 도착 지점이 막혀 있으면 이동하지 않는다.
// 도착 높이는 도착 지점에서 찾은 바닥 높이를 쓴다. 바닥은 대상 높이를 기준으로 위 · 아래 정한 범위 안에서 찾는다.
[RequireComponent(typeof(PlayerController), typeof(CharacterController))]
public class PlayerTeleport : MonoBehaviour
{
    private PlayerController _controller;
    private CharacterController _characterController;

    // 간격 · Layer · 바닥 찾는 범위는 모든 캐릭터가 같이 쓰는 공통 설정에서 읽는다.
    private CommonCharacterSettings Common => _controller.Common;

    private void Awake() => CacheComponent();

    // 액션의 순간 이동 옵션대로 대상 앞에 순간 이동한다. 옵션이 비어 있으면(순간 이동하지 않는 액션) 이동하지 않고 false 를 돌려준다.
    // 옆 각도는 부를 때마다 옵션의 범위 안에서 무작위로 정한다.
    // 지금(1인)은 플레이어 쪽의 기준이 자기 위치다. 파티 전투에서는 기준을 남아 있는 캐릭터의 위치로 바꾼다.
    public bool TryTeleportTo(Transform target, TeleportOption option)
    {
        if(option == null) return false;

        return TryTeleportTo(target, option.StandSide, option.PickSideAngle());
    }

    // 대상에서 side 쪽으로 정해진 간격만큼 떨어진 곳으로 순간 이동하고 대상 쪽을 본다.
    // 간격은 대상 몸 표면과 플레이어 몸 표면 사이다. 중심 사이 거리는 대상 반지름 + 플레이어 반지름 + 간격이다.
    //   중심에서 정한 거리만 띄우면 큰 적(보스)의 몸 안에 서게 되어, 겹침을 풀며 바닥 아래로 밀려나기 때문이다.
    // sideAngle : side 방향에서 대상 둘레로 비켜 설 각도(도, 위에서 볼 때 시계 방향이 +). 0 이면 side 방향 그대로다.
    // 도착 지점이 벽 · 지형에 겹치거나, 대상과 도착 지점 사이가 막혀 있으면 이동하지 않고 false 를 돌려준다.
    private bool TryTeleportTo(Transform target, TeleportOption.Side side, float sideAngle)
    {
        Vector3 targetPosition = target.position;
        Vector3 direction = side == TeleportOption.Side.TargetFront ? target.forward : transform.position - targetPosition;
        direction.y = 0f;
        direction = Quaternion.Euler(0f, sideAngle, 0f) * direction;

        float distance = GetHorizontalRadius(target) + _characterController.radius + Common.TeleportDistance;
        Vector3 destination = targetPosition + direction.normalized * distance;

        if(!TrySnapToGround(ref destination) || IsBlocked(targetPosition, destination)) return false;

        // CharacterController 가 켜져 있으면 위치를 직접 바꿔도 덮어쓰므로 잠깐 끄고 옮긴다.
        _characterController.enabled = false;
        transform.position = destination;
        _characterController.enabled = true;

        Vector3 toTarget = targetPosition - destination;
        toTarget.y = 0f;
        _controller.FaceImmediately(toTarget);

        return true;
    }

    // 대상 콜라이더의 수평 반지름. 콜라이더 모양(캡슐 · 박스 등)과 상관없이 월드 기준 경계 상자(bounds)의 가로 · 세로 중 큰 쪽 절반을 쓴다.
    // 콜라이더가 없거나 꺼져 있으면(도약 중인 보스 등) bounds 크기가 0 이라 0 이 된다.
    private static float GetHorizontalRadius(Transform target)
    {
        if(!target.TryGetComponent(out Collider targetCollider)) return 0f;

        Vector3 extents = targetCollider.bounds.extents;

        return Mathf.Max(extents.x, extents.z);
    }

    // 도착 지점 위에서 아래로 바닥을 찾아, 찾으면 도착 높이를 바닥 높이로 맞추고 true 를 돌려준다.
    // 대상 높이를 그대로 쓰면 도착 지점 바닥이 더 높을 때(경사 · 높낮이) 발이 땅 속에 들어가 바닥을 뚫고 떨어지기 때문이다.
    // 찾는 범위는 대상 높이 기준 위 · 아래로 정한 만큼이다. 범위 안에 바닥이 없으면 순간 이동하지 않는다.
    private bool TrySnapToGround(ref Vector3 destination)
    {
        Vector3 origin = destination + Vector3.up * Common.GroundSearchHeight;

        if(!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, Common.GroundSearchHeight + Common.GroundSearchDepth, Common.TeleportGroundLayers, QueryTriggerInteraction.Ignore)) return false;

        destination.y = hit.point.y;
        return true;
    }

    // 도착 지점에 플레이어 몸(캡슐)이 들어갈 자리가 있는지, 대상에서 도착 지점까지 벽이 가로막지 않는지 본다.
    // 캡슐은 바닥에 닿은 것을 겹침으로 보지 않도록 피부 두께만큼 띄워서 검사한다.
    private bool IsBlocked(Vector3 targetPosition, Vector3 destination)
    {
        Vector3 center = destination + _characterController.center + Vector3.up * _characterController.skinWidth;
        float halfHeight = Mathf.Max(0f, _characterController.height * 0.5f - _characterController.radius);
        Vector3 bottom = center - Vector3.up * halfHeight;
        Vector3 top = center + Vector3.up * halfHeight;

        if(Physics.CheckCapsule(bottom, top, _characterController.radius, Common.TeleportBlockLayers, QueryTriggerInteraction.Ignore)) return true;

        return Physics.Linecast(targetPosition + _characterController.center, center, Common.TeleportBlockLayers, QueryTriggerInteraction.Ignore);
    }

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _characterController = GetComponent<CharacterController>();
    }
}

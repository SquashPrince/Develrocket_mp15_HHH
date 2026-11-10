using UnityEngine;

// 대상 앞으로 순간 이동한다. 패링 · 필살기가 함께 쓴다.
// 실제로 다가가면 벽 등에 걸리기 때문에 위치를 바로 옮긴다. 도착 지점이 막혀 있으면 이동하지 않는다.
// 도착 높이는 대상의 기준점 높이를 쓰므로, 대상의 기준점은 플레이어처럼 발바닥에 있어야 한다.
[RequireComponent(typeof(PlayerController), typeof(CharacterController))]
public class PlayerTeleport : MonoBehaviour
{
    [Tooltip("순간 이동할 자리가 막혀 있는지 검사할 Layer. Player · Enemy 는 빼고 벽 · 지형만 넣는다")]
    [SerializeField] private LayerMask _blockLayers = 1;

    private PlayerController _controller;
    private CharacterController _characterController;

    private void Awake() => CacheComponent();

    // 대상 위치에서 direction 쪽으로 distance 만큼 떨어진 곳으로 순간 이동하고 대상 쪽을 본다.
    // 도착 지점이 벽 · 지형에 겹치거나, 대상과 도착 지점 사이가 막혀 있으면 이동하지 않고 false 를 돌려준다.
    public bool TryTeleport(Vector3 targetPosition, Vector3 direction, float distance)
    {
        direction.y = 0f;

        Vector3 destination = targetPosition + direction.normalized * distance;

        if(IsBlocked(targetPosition, destination)) return false;

        // CharacterController 가 켜져 있으면 위치를 직접 바꿔도 덮어쓰므로 잠깐 끄고 옮긴다.
        _characterController.enabled = false;
        transform.position = destination;
        _characterController.enabled = true;

        Vector3 toTarget = targetPosition - destination;
        toTarget.y = 0f;
        _controller.FaceImmediately(toTarget);

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

        if(Physics.CheckCapsule(bottom, top, _characterController.radius, _blockLayers, QueryTriggerInteraction.Ignore)) return true;

        return Physics.Linecast(targetPosition + _characterController.center, center, _blockLayers, QueryTriggerInteraction.Ignore);
    }

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _characterController = GetComponent<CharacterController>();
    }
}

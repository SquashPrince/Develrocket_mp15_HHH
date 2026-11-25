using UnityEngine;

// 액션 데이터에 붙이는 순간 이동 옵션. 어느 쪽에 설지만 정하고, 간격 · 바닥 찾기 계산은 TeleportManager 가 맡는다.
[CreateAssetMenu(fileName = "NewTeleportOption", menuName = "HHH/Teleport Option")]
public class TeleportOption : ScriptableObject
{
    // Inspector 에 순서 번호로 저장되므로, 새 방향은 항상 맨 뒤에 추가한다.
    // 플레이어 쪽 : 공격하지 않는 적은 플레이어를 보고 있지 않을 수 있어 적의 정면 대신 쓴다.
    public enum Side
    {
        TargetFront,
        PlayerSide
    }

    [Tooltip("대상의 어느 쪽에 설지")]
    [SerializeField] private Side _side = Side.PlayerSide;

    [Header("옆 각도")]
    [Tooltip("서는 방향에서 대상 둘레로 비켜 서는 최소 각도(도). 최소 ~ 최대 사이에서 무작위로 정하고 왼쪽 · 오른쪽도 무작위다. 둘 다 0 이면 비켜 서지 않는다. 적과의 거리는 순간 이동 공통 간격을 그대로 쓴다. 파티 전투에서 남아 있는 캐릭터와 교체된 캐릭터가 같은 위치에 서지 않게 하기 위해서다 (콤보 스킬)")]
    [SerializeField, Range(0f, 180f)] private float _sideAngleMin;
    [Tooltip("대상 둘레로 비켜 서는 최대 각도(도). 최소보다 작으면 최소를 쓴다")]
    [SerializeField, Range(0f, 180f)] private float _sideAngleMax;

    public Side StandSide => _side;

    // 최소 ~ 최대 사이 무작위 각도에 무작위 좌우(+ / -)를 붙여 돌려준다.
    public float PickSideAngle()
    {
        float angle = Random.Range(_sideAngleMin, Mathf.Max(_sideAngleMin, _sideAngleMax));

        return Random.value < 0.5f ? angle : -angle;
    }
}

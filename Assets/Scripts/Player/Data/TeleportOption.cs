using UnityEngine;

// 액션 데이터에 붙이는 순간 이동 옵션. 이 옵션이 붙은 액션은 시작할 때 대상 앞으로 순간 이동한다. (패링 · 필살기 · 콤보 스킬)
// 대상과의 간격 · 바닥 찾기 같은 계산은 순간 이동 규칙(TeleportManager)이 맡고, 이 옵션은 어느 쪽에 설지만 정한다.
// 같은 옵션 에셋을 여러 액션이 함께 쓸 수 있다.
[CreateAssetMenu(fileName = "NewTeleportOption", menuName = "HHH/Teleport Option")]
public class TeleportOption : ScriptableObject
{
    // 대상의 어느 쪽에 설지. Inspector 에 저장된 값은 순서 번호이므로, 새 방향은 항상 맨 뒤에 추가한다.
    //   대상 정면 : 공격하는 적을 마주 보도록 적의 정면에 선다. (패링)
    //   플레이어 쪽 : 공격하지 않는 적은 플레이어를 보고 있지 않을 수 있어, 적에서 플레이어가 있던 쪽에 선다. (필살기 · 콤보 스킬)
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

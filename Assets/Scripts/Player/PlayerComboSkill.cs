using UnityEngine;

// 콤보 스킬(체인 스킬) 수치와 발동. 그로기 적에게 강타가 맞으면 타격 판정이 발동을 요청한다. 그로기마다 한 번 발동한다.
// 한 번의 콤보 스킬은 필살기처럼 단계(동작) 여러 개를 순서대로 이어 쓴다. 단계 수와 연계 수는 관계없다.
// 콤보 스킬 중 대상에게 강타가 맞으면 다음 연계를 잇는다. 최대 연계 수는 대상 적이 정한다.
// 코어 전투에서는 1인이라 같은 콤보 스킬을 다시 쓴다. 파티 전투에서는 다음 캐릭터가 이어받도록 확장한다.
[RequireComponent(typeof(PlayerController), typeof(PlayerTeleport))]
public class PlayerComboSkill : MonoBehaviour
{
    [Tooltip("한 번의 콤보 스킬 액션. 동작 목록 순서대로 이어 재생한다. 동작 수와 연계 수는 관계없다. 하체 달리기 · 대시 캔슬 · 입력 버퍼 · 필살기로 끊기 값은 쓰지 않는다")]
    [SerializeField] private ActionData _action;
    [Header("순간 이동 옆 각도")]
    [Tooltip("발동할 때 적 둘레로 비켜 서는 최소 각도(도). 최소 ~ 최대 사이에서 무작위로 정하고 왼쪽 · 오른쪽도 무작위다. 적과의 거리는 순간 이동 공통 간격을 그대로 쓴다. 파티 전투에서 남아 있는 캐릭터와 교체된 캐릭터가 같은 위치에서 콤보 스킬을 쓰지 않게 하기 위해서다")]
    [SerializeField, Range(0f, 180f)] private float _sideAngleMin;
    [Tooltip("발동할 때 적 둘레로 비켜 서는 최대 각도(도). 최소보다 작으면 최소를 쓴다")]
    [SerializeField, Range(0f, 180f)] private float _sideAngleMax;

    private PlayerController _controller;
    private PlayerTeleport _teleport;
    private PlayerComboSkillState _comboSkillState;

    public ActionData Action => _action;
    // 콤보 스킬 중에는 다른 그로기 적에게 강타가 맞아도 다시 발동하지 않는다.
    public bool CanStart => !_comboSkillState.IsActive;

    private void Awake() => CacheComponent();
    private void Start() => Init();


    // 그로기 적을 대상으로 콤보 스킬 상태로 넘긴다. 진행 중이던 공격 · 필살기는 끊긴다.
    // maxChain : 대상 적의 최대 연계 수
    public void Begin(IEnemyTargetable target, int maxChain)
    {
#if UNITY_EDITOR
        Debug.Log($"[콤보 스킬] {target.transform.name} (최대 연계 {maxChain})");
#endif

        _comboSkillState.Setup(target, maxChain);
        _controller.EnterState(_comboSkillState);
    }

    // 콤보 스킬 중 강타가 맞은 적을 알린다. 콤보 스킬 대상이면 다음 연계를 잇는다.
    public void OnImpactHit(IEnemyDamageable enemy) => _comboSkillState.QueueChain(enemy.transform);

    // 대상에서 플레이어 쪽을 기준으로, 무작위 옆 각도 · 무작위 좌우로 비켜 선 곳에 순간 이동한다. 막혀 있으면 제자리에 있는다.
    // 지금(1인)은 기준이 자기 위치다. 파티 전투에서는 기준을 남아 있는 캐릭터의 위치로 바꾼다.
    public void TeleportToTarget(IEnemyTargetable target) => _teleport.TryTeleportTo(target.transform, PlayerTeleport.Side.PlayerSide, PickSideAngle());

    private float PickSideAngle()
    {
        float angle = Random.Range(_sideAngleMin, Mathf.Max(_sideAngleMin, _sideAngleMax));

        return Random.value < 0.5f ? angle : -angle;
    }

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _teleport = GetComponent<PlayerTeleport>();
    }

    private void Init()
    {
        _comboSkillState = new PlayerComboSkillState(_controller, this);
    }
}

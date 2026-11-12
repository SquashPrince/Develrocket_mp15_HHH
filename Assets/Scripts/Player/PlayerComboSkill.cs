using UnityEngine;

// 콤보 스킬(체인 스킬) 수치와 발동. 그로기 적에게 강타가 맞으면 타격 판정이 발동을 요청한다. 그로기마다 한 번 발동한다.
// 한 번의 콤보 스킬은 필살기처럼 단계(동작) 여러 개를 순서대로 이어 쓴다. 단계 수와 연계 수는 관계없다.
// 콤보 스킬 중 대상에게 강타가 맞으면 다음 연계를 잇는다. 최대 연계 수는 대상 적이 정한다.
// 코어 전투에서는 1인이라 같은 콤보 스킬을 다시 쓴다. 파티 전투에서는 다음 캐릭터가 이어받도록 확장한다.
[RequireComponent(typeof(PlayerController), typeof(PlayerTeleport))]
public class PlayerComboSkill : MonoBehaviour
{
    [Tooltip("한 번의 콤보 스킬을 이루는 단계(동작)별 설정. 배열 크기가 단계 수이며, Animator 에 \"ComboSkill 1\" 부터 단계 수만큼 상태가 있어야 한다. 연계 수와는 관계없다. 하체 달리기 · 대시 캔슬 · 입력 버퍼 · 필살기로 끊기 값은 쓰지 않는다")]
    [SerializeField] private PlayerAttackSetting[] _chainSteps = { new PlayerAttackSetting(), new PlayerAttackSetting(), new PlayerAttackSetting() };
    [Tooltip("발동할 때 그로기 적에서 플레이어 쪽으로, 적 몸 표면과 플레이어 몸 표면 사이를 이 간격만큼 띄워 순간 이동한다. 순간 이동 공통 간격과 따로 둔다. 파티 전투에서 남아 있는 캐릭터와 교체된 캐릭터가 같은 위치에서 콤보 스킬을 쓰지 않게 위치를 다르게 정하기 위해서다")]
    [SerializeField, Min(0f)] private float _teleportDistance = 1.5f;

    private PlayerController _controller;
    private PlayerTeleport _teleport;
    private PlayerComboSkillState _comboSkillState;

    public int ChainStepCount => _chainSteps.Length;
    // 콤보 스킬 중에는 다른 그로기 적에게 강타가 맞아도 다시 발동하지 않는다.
    public bool CanStart => !_comboSkillState.IsActive;

    private void Awake() => CacheComponent();
    private void Start() => Init();

    // step : 연계 단계(0부터)
    public PlayerAttackSetting GetChainStep(int step) => _chainSteps[step];

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

    // 대상에서 플레이어 쪽으로 콤보 스킬 전용 간격만큼 떨어진 곳에 순간 이동한다. 막혀 있으면 제자리에 있는다.
    public void TeleportToTarget(IEnemyTargetable target) => _teleport.TryTeleportTo(target.transform, PlayerTeleport.Side.PlayerSide, _teleportDistance);

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

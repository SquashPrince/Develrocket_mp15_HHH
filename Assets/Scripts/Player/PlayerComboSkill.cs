using UnityEngine;

// 콤보 스킬 수치와 발동. 그로기 적에게 강타가 맞으면 타격 판정이 발동을 요청한다. 그로기마다 한 번 발동한다.
// 코어 전투에서는 1인으로 같은 모션을 연계 수만큼 이어 쓰는 틀이다. 파티 전투에서 캐릭터 선택 · 연계로 확장한다.
[RequireComponent(typeof(PlayerController), typeof(PlayerTeleport))]
public class PlayerComboSkill : MonoBehaviour
{
    [Tooltip("연계 단계별 설정. 배열 크기가 연계 수이며, Animator 에 \"ComboSkill 1\" 부터 연계 수만큼 상태가 있어야 한다. 하체 달리기 · 대시 캔슬 · 입력 버퍼 · 필살기로 끊기 값은 쓰지 않는다")]
    [SerializeField] private PlayerAttackSetting[] _chainSteps = { new PlayerAttackSetting(), new PlayerAttackSetting(), new PlayerAttackSetting() };
    [Tooltip("발동할 때 그로기 적에서 플레이어 쪽으로 이 거리만큼 떨어진 곳에 순간 이동한다")]
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
    public void Begin(IEnemyTargetable target)
    {
#if UNITY_EDITOR
        Debug.Log($"[콤보 스킬] {target.transform.name}");
#endif

        _comboSkillState.Setup(target);
        _controller.EnterState(_comboSkillState);
    }

    // 대상에서 플레이어 쪽으로 정해진 거리 앞에 순간 이동한다. 막혀 있으면 제자리에 있는다.
    public void TeleportToTarget(IEnemyTargetable target)
    {
        Vector3 targetPosition = target.transform.position;

        _teleport.TryTeleport(targetPosition, transform.position - targetPosition, _teleportDistance);
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

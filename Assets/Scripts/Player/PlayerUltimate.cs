using UnityEngine;
using UnityEngine.InputSystem;

// 필살기 입력 · 게이지 · 수치. 게이지는 에너지와 별개로 쌓이며, 공격 적중 · 패링 성공 · 완벽 회피로 쌓이고 줄어들지 않는다.
// 공격 적중 획득량은 공격마다 공격 설정에서 정하고, 패링 성공 · 완벽 회피 획득량은 여기서 정한다.
// 게이지가 가득 차 있고 현재 상태가 필살기를 허용할 때 필살기 키를 누르면 게이지를 전부 소모하고 필살기 상태로 넘긴다.
// 현재 게이지는 Inspector 를 Debug 모드로 바꾸면 실시간으로 볼 수 있다.
[RequireComponent(typeof(PlayerController), typeof(PlayerTeleport), typeof(PlayerTargeting))]
public class PlayerUltimate : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] private InputActionReference _ultimateAction;

    [Header("게이지")]
    [SerializeField, Min(1f)] private float _maxGauge = 3000f;
    [Tooltip("패링 성공 한 번에 얻는 게이지")]
    [SerializeField, Min(0f)] private float _parryGaugeGain = 300f;
    [Tooltip("완벽 회피 한 번에 얻는 게이지")]
    [SerializeField, Min(0f)] private float _perfectDodgeGaugeGain = 300f;

    [Header("필살기")]
    [Tooltip("연출 뒤 이어지는 공격 단계별 설정. 배열 크기가 단계 수이며, Animator 에 \"Ultimate 1\" 부터 단계 수만큼 상태가 있어야 한다. 하체 달리기 · 대시 캔슬 · 입력 버퍼 값은 쓰지 않는다")]
    [SerializeField] private PlayerAttackSetting[] _attackSteps = { new PlayerAttackSetting() };
    [Tooltip("발동할 때 이 거리 안의 대상(록온 대상 우선, 없으면 자동 조준 대상)을 찾아 순간 이동한다")]
    [SerializeField, Min(0f)] private float _targetSearchRange = 10f;

    private PlayerController _controller;
    private PlayerTeleport _teleport;
    private PlayerTargeting _targeting;
    private PlayerUltimateState _ultimateState;
    private float _currentGauge;

    // 값을 바꿀 때 항상 0 ~ 최대치 사이로 맞춘다.
    public float CurrentGauge
    {
        get => _currentGauge;
        private set => _currentGauge = Mathf.Clamp(value, 0f, _maxGauge);
    }

    public bool IsGaugeFull => _currentGauge >= _maxGauge;
    public int AttackStepCount => _attackSteps.Length;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update() => TryStartUltimate();

    public void AddGauge(float amount)
    {
        if(amount <= 0f) return;

        CurrentGauge += amount;

#if UNITY_EDITOR
        Debug.Log($"[필살기 게이지] +{amount} → {_currentGauge:F0} / {_maxGauge:F0}");
#endif
    }

    public void AddParryGauge() => AddGauge(_parryGaugeGain);

    public void AddPerfectDodgeGauge() => AddGauge(_perfectDodgeGaugeGain);

    // step : 공격 단계(0부터)
    public PlayerAttackSetting GetAttackStep(int step) => _attackSteps[step];

    // 공격 대상 기준(록온 대상 → 찾는 거리 안의 자동 조준 대상)으로 대상을 고르고, 대상에서 플레이어 쪽에 순간 이동한다.
    // 대상을 돌려준다. 대상이 없으면 null, 도착 지점이 막혀 있으면 이동하지 않고 대상만 돌려준다.
    public IEnemyTargetable TeleportToTarget()
    {
        IEnemyTargetable target = _targeting.FindAttackTarget(_targetSearchRange);

        if(target == null) return null;

        _teleport.TryTeleportTo(target.transform, PlayerTeleport.Side.PlayerSide);
        return target;
    }

    // 진행 중이던 강공격 차지는 상태를 넘길 때 끊긴다.
    private void TryStartUltimate()
    {
        if(!_ultimateAction.action.WasPressedThisFrame()) return;
        if(!IsGaugeFull || !_controller.CanUseUltimate) return;

        CurrentGauge = 0f;

#if UNITY_EDITOR
        Debug.Log("[필살기] 게이지 전부 소모");
#endif

        _controller.EnterState(_ultimateState);
    }

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _teleport = GetComponent<PlayerTeleport>();
        _targeting = GetComponent<PlayerTargeting>();
    }

    private void Init()
    {
        _ultimateState = new PlayerUltimateState(_controller, this);
    }
}

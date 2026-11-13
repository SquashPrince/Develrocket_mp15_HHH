using UnityEngine;

// 필살기 입력과 대상. 게이지는 상태 기능(PlayerStatus)이 가진다.
// 게이지가 가득 차 있고 현재 상태가 필살기를 허용할 때 필살기 키를 누르면 게이지를 전부 소모하고 필살기 상태로 넘긴다.
[RequireComponent(typeof(PlayerController), typeof(PlayerTargeting), typeof(PlayerInputReader))]
public class PlayerUltimate : MonoBehaviour
{
    [Tooltip("발동할 때 이 거리 안의 대상(록온 대상 우선, 없으면 자동 조준 대상)을 찾는다. 필살기 액션에 순간 이동 옵션이 있으면 그 대상 앞으로 순간 이동한다")]
    [SerializeField, Min(0f)] private float _targetSearchRange = 10f;

    private PlayerController _controller;
    private PlayerTargeting _targeting;
    private PlayerInputReader _input;
    private PlayerUltimateState _ultimateState;

    public ActionData Action => _controller.Character.Ultimate;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update() => TryStartUltimate();

    // 공격 대상 기준(록온 대상 → 찾는 거리 안의 자동 조준 대상)으로 필살기 대상을 고른다. 없으면 null 이다.
    public IEnemyTargetable FindTarget() => _targeting.FindAttackTarget(_targetSearchRange);

    // 진행 중이던 강공격 차지는 상태를 넘길 때 끊긴다.
    private void TryStartUltimate()
    {
        if(!_input.WasUltimatePressed) return;
        if(!_controller.Status.IsUltimateGaugeFull || !_controller.CanUseUltimate) return;

        _controller.Status.ConsumeUltimateGauge();
        _controller.EnterState(_ultimateState);
    }

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _targeting = GetComponent<PlayerTargeting>();
        _input = GetComponent<PlayerInputReader>();
    }

    private void Init()
    {
        _ultimateState = new PlayerUltimateState(_controller, this);
    }
}

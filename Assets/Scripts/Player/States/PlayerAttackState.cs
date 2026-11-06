using UnityEngine;

// 기본 공격(콤보)과 강공격 상태.
// 흐름 : 휘두르기 → (공격 동작 끝) → 선입력이 있으면 다음 단계 / 이동 입력이 있으면 이동 / 없으면 후딜 → (후딜 끝) → 대기
// 후딜 중 공격 입력은 콤보를 이어가고, 이동 입력은 후딜을 끊는다. 대시는 언제든 공격을 끊는다.
public class PlayerAttackState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다. 기본 공격은 "BasicAttack 1", "BasicAttack 2" ... 순서다.
    private const string BASIC_ATTACK_ANIMATION_PREFIX = "BasicAttack ";

    private static readonly int HEAVY_ATTACK_ANIMATION = Animator.StringToHash("HeavyAttack");

    private enum Phase
    {
        Swing,
        Recovery
    }

    private Phase _phase;
    private int _step;
    private int _animationHash;
    private bool _isHeavy;
    private bool _isComboInputOpen;
    private bool _hasBufferedInput;

    public PlayerAttackState(PlayerController controller) : base(controller)
    {
    }

    // step : 기본 공격 단계(0부터). 강공격이면 사용하지 않는다.
    public void Setup(int step, bool isHeavy)
    {
        _step = step;
        _isHeavy = isHeavy;
        _animationHash = isHeavy ? HEAVY_ATTACK_ANIMATION : Animator.StringToHash(BASIC_ATTACK_ANIMATION_PREFIX + (step + 1));
    }

    public override void Enter()
    {
        _phase = Phase.Swing;
        _isComboInputOpen = false;
        _hasBufferedInput = false;

        Controller.HorizontalVelocity = Vector3.zero;
        Controller.PlayAnimation(_animationHash);
    }

    public override void Tick()
    {
        Controller.HorizontalVelocity = Vector3.zero;

        Vector3 moveDirection = Controller.ReadMoveDirection();

        if(Controller.WasDashPressed && Controller.TryStartDash(moveDirection)) return;

        if(Controller.WasAttackPressed && HandleAttackInput(moveDirection)) return;

        if(Controller.UpdateCharge()) return;

        // 후딜 중 이동 입력이 들어오면 후딜을 끊고 이동한다.
        if(_phase == Phase.Recovery && moveDirection != Vector3.zero) Controller.EnterLocomotion(false);
    }

    public override void OnAnimationEvent(PlayerAnimationEventType type, int stateHash)
    {
        // 이전 공격 클립과 섞이는 동안 들어오는 이전 클립의 이벤트는 무시한다.
        if(stateHash != _animationHash) return;

        switch(type)
        {
            case PlayerAnimationEventType.ComboInputOpen:
                _isComboInputOpen = true;
                break;
            case PlayerAnimationEventType.Hit:
                Controller.PerformHit();
                break;
            case PlayerAnimationEventType.AttackMotionEnd:
                OnAttackMotionEnd();
                break;
            case PlayerAnimationEventType.RecoveryEnd:
                if(_phase == Phase.Recovery) Controller.EnterLocomotion(false);
                break;
        }
    }

    // 공격 키를 누르면 항상 차지 시간 계산을 시작한다.
    // 휘두르는 중이면 선입력 허용 이후에만 다음 단계를 저장하고, 후딜 중이면 바로 다음 공격을 시작한다.
    // 상태가 바뀌었으면 true 를 돌려준다.
    private bool HandleAttackInput(Vector3 moveDirection)
    {
        Controller.BeginCharge();

        if(_phase == Phase.Swing)
        {
            if(_isComboInputOpen && !_isHeavy) _hasBufferedInput = true;
            return false;
        }

        Controller.StartBasicAttack(moveDirection);
        return true;
    }

    private void OnAttackMotionEnd()
    {
        if(!_isHeavy) Controller.FinishComboStep(_step);

        Vector3 moveDirection = Controller.ReadMoveDirection();

        if(_hasBufferedInput) Controller.StartBasicAttack(moveDirection);
        else if(moveDirection != Vector3.zero) Controller.EnterLocomotion(false);
        else _phase = Phase.Recovery;
    }
}

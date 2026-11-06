using UnityEngine;

// 공격 상태. 기본 공격(콤보) · 강공격 · 스킬 · 특수스킬이 같은 흐름을 쓴다.
// 흐름 : 휘두르기 → (공격 동작 끝) → 선입력이 있으면 다음 단계 / 이동 입력이 있으면 이동 / 없으면 후딜 → (후딜 끝) → 대기
// 후딜 중 공격 입력은 콤보를 이어가고, 스킬 입력은 스킬을 쓰며, 이동 입력은 후딜을 끊는다. 대시는 언제든 공격을 끊는다.
// 휘두르기 시작부터 공격 설정의 전진 시간 동안 바라보는 방향으로 나아간다.
public class PlayerAttackState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다. 기본 공격은 "BasicAttack 1", "BasicAttack 2" ... 순서다.
    private const string BASIC_ATTACK_ANIMATION_PREFIX = "BasicAttack ";

    private static readonly int HEAVY_ATTACK_ANIMATION = Animator.StringToHash("HeavyAttack");
    private static readonly int SKILL_ANIMATION = Animator.StringToHash("Skill");
    private static readonly int SPECIAL_SKILL_ANIMATION = Animator.StringToHash("SpecialSkill");

    public enum AttackType
    {
        Basic,
        Heavy,
        Skill,
        SpecialSkill
    }

    private enum Phase
    {
        Swing,
        Recovery
    }

    private PlayerAttackSetting _setting;
    private AttackType _type;
    private Phase _phase;
    private Vector3 _advanceVelocity;
    private float _advanceTimer;
    private int _step;
    private int _animationHash;
    private bool _isComboInputOpen;
    private bool _hasBufferedInput;

    public PlayerAttackState(PlayerController controller) : base(controller)
    {
    }

    private bool IsSkill => _type == AttackType.Skill || _type == AttackType.SpecialSkill;
    // 콤보의 마지막 단계는 마무리 공격이다. 끝났다는 느낌을 주기 위해 후딜이 끝날 때까지 공격 입력을 받지 않는다.
    private bool IsFinisher => _type == AttackType.Basic && _step == Controller.ComboStepCount - 1;

    // step : 기본 공격 단계(0부터). 기본 공격이 아니면 사용하지 않는다.
    public void Setup(AttackType type, int step, PlayerAttackSetting setting)
    {
        _type = type;
        _step = step;
        _setting = setting;
        _animationHash = GetAnimationHash(type, step);
    }

    public override void Enter()
    {
        _phase = Phase.Swing;
        _isComboInputOpen = false;
        _hasBufferedInput = false;
        _advanceTimer = 0f;
        _advanceVelocity = Controller.transform.forward * (_setting.AdvanceDistance / _setting.AdvanceDuration);

        Controller.HorizontalVelocity = _advanceVelocity;
        Controller.PlayAnimation(_animationHash, _setting.FadeTime);
    }

    public override void Tick()
    {
        Controller.HorizontalVelocity = UpdateAdvance();

        Vector3 moveDirection = Controller.ReadMoveDirection();

        if(Controller.WasDashPressed && Controller.TryStartDash(moveDirection)) return;

        // 스킬은 후딜 중에만 쓸 수 있다. 휘두르는 도중 누른 스킬 입력은 무시한다.
        if(_phase == Phase.Recovery && Controller.WasSkillPressed)
        {
            Controller.StartSkill();
            return;
        }

        if(Controller.WasAttackPressed && HandleAttackInput()) return;

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
                Controller.PerformHit(_type, _setting);
                break;
            case PlayerAnimationEventType.AttackMotionEnd:
                OnAttackMotionEnd();
                break;
            case PlayerAnimationEventType.RecoveryEnd:
                if(_phase == Phase.Recovery) Controller.EnterLocomotion(false);
                break;
        }
    }

    private static int GetAnimationHash(AttackType type, int step)
    {
        switch(type)
        {
            case AttackType.Heavy:
                return HEAVY_ATTACK_ANIMATION;
            case AttackType.Skill:
                return SKILL_ANIMATION;
            case AttackType.SpecialSkill:
                return SPECIAL_SKILL_ANIMATION;
            default:
                return Animator.StringToHash(BASIC_ATTACK_ANIMATION_PREFIX + (step + 1));
        }
    }

    // 전진 시간 동안만 앞으로 나아가고, 그 뒤에는 제자리에 멈춘다.
    private Vector3 UpdateAdvance()
    {
        _advanceTimer += Time.deltaTime;

        return _advanceTimer < _setting.AdvanceDuration ? _advanceVelocity : Vector3.zero;
    }

    // 마무리 공격 중이거나 스킬을 휘두르는 중이면 공격 입력을 받지 않는다. 차지도 시작하지 않는다.
    // 그 외에는 공격 키를 누르면 항상 차지 시간 계산을 시작한다.
    // 휘두르는 중이면 선입력 허용 이후에만 다음 단계를 저장하고, 후딜 중이면 바로 다음 공격을 시작한다.
    // 상태가 바뀌었으면 true 를 돌려준다.
    private bool HandleAttackInput()
    {
        if(IsFinisher || (IsSkill && _phase == Phase.Swing)) return false;

        Controller.BeginCharge();

        if(_phase == Phase.Swing)
        {
            if(_isComboInputOpen && _type == AttackType.Basic) _hasBufferedInput = true;
            return false;
        }

        Controller.StartBasicAttack();
        return true;
    }

    private void OnAttackMotionEnd()
    {
        if(_type == AttackType.Basic) Controller.FinishComboStep(_step);

        Vector3 moveDirection = Controller.ReadMoveDirection();

        if(_hasBufferedInput) Controller.StartBasicAttack();
        else if(moveDirection != Vector3.zero) Controller.EnterLocomotion(false);
        else _phase = Phase.Recovery;
    }
}

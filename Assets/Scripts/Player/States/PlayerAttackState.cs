using UnityEngine;

// 기본 공격 · 강공격 · 스킬 · 대시 공격 · 회피 반격 · 패링 추격 · 빠른 지원이 함께 쓰는 공격 상태.
// 흐름 : 휘두르기 → (마지막 동작 끝) → 선입력이면 다음 단계 / 이동 입력이면 이동 / 없으면 후딜 → 대기
// 자세한 흐름 : Docs/Technical/Player/PlayerAttack.md
public class PlayerAttackState : PlayerState
{
    public enum AttackType
    {
        Basic,
        Heavy,
        Skill,
        SpecialSkill,
        DashAttack,
        DodgeCounter,
        ParryChase,
        QuickSupport
    }

    private enum Phase
    {
        Swing,
        Recovery
    }

    private ActionData _action;
    private ActionStep _motion;
    private IEnemyTargetable _priorityTarget;
    private AttackType _type;
    private Phase _phase;
    private Vector3 _advanceVelocity;
    private float _advanceTimer;
    private int _step;
    private int _motionIndex;
    private int _animationHash;
    private bool _isComboInputOpen;
    private bool _hasBufferedInput;

    public PlayerAttackState(PlayerController controller) : base(controller)
    {
    }

    public override bool CanUseUltimate => _motion.Allows(ActionStep.Interrupt.Ultimate);
    public override bool CanSwap => Controller.ActionTrigger.AllowsSwap(_motion);
    public override ActionData CurrentAction => _action;
    public override ActionStep CurrentStep => _motion;

    private bool IsSkill => _type == AttackType.Skill || _type == AttackType.SpecialSkill;
    // 끝났다는 느낌을 주려고 마무리 공격은 후딜이 끝날 때까지 공격 입력을 받지 않는다.
    private bool IsFinisher => _type == AttackType.Basic && _step == Controller.ComboStepCount - 1;
    // 대시 공격 · 패링 추격 · 빠른 지원 뒤에는 콤보 1단으로 이어진다.
    private bool CanBufferBasicAttack => _type == AttackType.Basic || _type == AttackType.DashAttack || _type == AttackType.ParryChase || _type == AttackType.QuickSupport;
    // 무적 · 후딜이 붙을 동작이라 휘두르는 중 강공격으로 끊지 못하게 한다.
    private bool CanChargeInSwing => _type != AttackType.DashAttack && _type != AttackType.DodgeCounter && _type != AttackType.ParryChase && _type != AttackType.QuickSupport;

    // step : 기본 공격 단계(0부터). 기본 공격이 아니면 쓰지 않는다.
    public void Setup(AttackType type, int step, ActionData action, IEnemyTargetable priorityTarget = null)
    {
        _type = type;
        _step = step;
        _action = action;
        _priorityTarget = priorityTarget;
    }

    public override void Enter()
    {
        _phase = Phase.Swing;
        _isComboInputOpen = false;
        _hasBufferedInput = false;

        // 완벽 회피 슬로모션이 과하지 않게 어떤 공격이든 시작하면 바로 끝낸다.
        Controller.EndSlowMotion();

        // 휘두르는 동안만 무적이고, 후딜에 들어가거나 상태를 나가면 풀린다.
        Controller.Status.SetAttackInvincible(_action.IsInvincibleOnSwing);

        StartMotion(0);
    }

    public override void Exit()
    {
        Controller.AttackJudge.EndHit();
        Controller.Animation.SetLegRun(false);
        Controller.Status.SetAttackInvincible(false);
    }

    public override void Tick()
    {
        Controller.HorizontalVelocity = UpdateAdvance();

        Vector3 moveDirection = Controller.ReadMoveDirection();

        bool canDashCancel = _phase == Phase.Recovery || _motion.Allows(ActionStep.Interrupt.Dash);

        if(canDashCancel && Controller.WasDashPressed && Controller.TryStartDash(moveDirection)) return;

        // 스킬은 후딜 중에만 쓸 수 있다.
        if(_phase == Phase.Recovery && Controller.WasSkillPressed)
        {
            Controller.StartSkill();
            return;
        }

        if(Controller.WasAttackPressed && HandleAttackInput()) return;

        if(Controller.UpdateCharge()) return;

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
            case PlayerAnimationEventType.HitStart:
                Controller.AttackJudge.BeginHit(_action, _motion);
                break;
            case PlayerAnimationEventType.HitEnd:
                Controller.AttackJudge.FinishHit();
                break;
            case PlayerAnimationEventType.ImpactEnd:
                Controller.AttackJudge.PerformImpact(_action, _motion);
                break;
            case PlayerAnimationEventType.AttackMotionEnd:
                if(_motionIndex + 1 < _action.StepCount) StartMotion(_motionIndex + 1);
                else OnAttackMotionEnd();
                break;
            case PlayerAnimationEventType.RecoveryEnd:
                if(_phase == Phase.Recovery) Controller.EnterLocomotion(false);
                break;
        }
    }

    // 동작마다 다시 대상 쪽으로 돌아 다가간다.
    private void StartMotion(int index)
    {
        Controller.AttackJudge.EndHit();
        _motionIndex = index;
        _motion = _action.GetStep(index);
        _animationHash = _motion.AnimationHash;
        _advanceTimer = 0f;

        // 도중에 방향이 휙휙 바뀌면 모션이 어색해서, 입력 방향으로 도는 것은 첫 동작 · 콤보 1단에서만 한다.
        bool canTurnToInput = index == 0 && (_type != AttackType.Basic || _step == 0);
        float advanceDistance = Controller.AimAttack(_motion, canTurnToInput, _priorityTarget);

        _advanceVelocity = Controller.transform.forward * (advanceDistance / _motion.AdvanceDuration);

        // 동작의 전진 거리보다 멀리 다가가면 하체에 달리기 모션을 보여준다.
        bool isFarAdvance = advanceDistance > _motion.AdvanceDistance;
        Controller.Animation.SetLegRun(_action.UseLegRun && (_action.AlwaysLegRun || isFarAdvance) && advanceDistance > 0f);
        Controller.HorizontalVelocity = _advanceVelocity;
        Controller.Animation.Play(_animationHash, _motion.FadeTime);
    }

    private Vector3 UpdateAdvance()
    {
        _advanceTimer += Controller.DeltaTime;

        if(_advanceTimer < _motion.AdvanceDuration) return _advanceVelocity;

        Controller.Animation.SetLegRun(false);
        return Vector3.zero;
    }

    // 휘두르는 중이면 선입력 허용 이후에만 다음 단계를 저장하고, 후딜 중이면 바로 다음 공격을 시작한다. 상태가 바뀌었으면 true.
    private bool HandleAttackInput()
    {
        if(IsFinisher || (IsSkill && _phase == Phase.Swing)) return false;

        if(_phase == Phase.Swing)
        {
            if(CanChargeInSwing) Controller.BeginCharge();
            if(_isComboInputOpen && CanBufferBasicAttack) _hasBufferedInput = true;
            return false;
        }

        Controller.BeginCharge();
        Controller.StartBasicAttack();
        return true;
    }

    private void OnAttackMotionEnd()
    {
        if(_type == AttackType.Basic) Controller.FinishComboStep(_step);

        Vector3 moveDirection = Controller.ReadMoveDirection();

        if(_hasBufferedInput) Controller.StartBasicAttack();
        else if(moveDirection != Vector3.zero) Controller.EnterLocomotion(false);
        else EnterRecovery();
    }

    private void EnterRecovery()
    {
        _phase = Phase.Recovery;
        Controller.Status.SetAttackInvincible(false);
    }
}

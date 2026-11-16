using UnityEngine;

// 공격 상태. 기본 공격(콤보) · 강공격 · 스킬 · 특수스킬 · 대시 공격 · 회피 반격 · 패링 추격이 같은 흐름을 쓴다.
// 흐름 : 휘두르기 → (공격 동작 끝) → 선입력이 있으면 다음 단계 / 이동 입력이 있으면 이동 / 없으면 후딜 → (후딜 끝) → 대기
// 후딜 중 공격 입력은 콤보를 이어가고, 스킬 입력은 스킬을 쓰며, 이동 입력은 후딜을 끊는다.
// 대시는 공격을 끊는다. 단, 공격 설정에서 대시 캔슬을 끈 공격은 휘두르는 동안 끊을 수 없다.
// 시작할 때 대상 · 입력에 따라 방향과 전진 거리를 정하고, 휘두르기 시작부터 동작의 전진 시간 동안 그 방향으로 나아간다.
// 액션의 동작이 여러 개면 동작 끝마다 다음 동작을 재생하고, 동작마다 다시 대상 쪽으로 돌아 다가간다. 마지막 동작이 끝나야 위 흐름의 "공격 동작 끝"이 된다.
// 재생할 모션은 동작에 적힌 Animator 상태 이름을 따른다.
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
    // 지금 재생 중인 동작과 그 번호(0부터)
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

    // 지금 동작의 끼어들기 허용에서 필살기로 끊을 수 있는지 · 교체할 수 있는지 정한다.
    public override bool CanUseUltimate => _motion.Allows(ActionStep.Interrupt.Ultimate);
    public override bool CanSwap => _motion.Allows(ActionStep.Interrupt.Swap);

    private bool IsSkill => _type == AttackType.Skill || _type == AttackType.SpecialSkill;
    // 콤보의 마지막 단계는 마무리 공격이다. 끝났다는 느낌을 주기 위해 후딜이 끝날 때까지 공격 입력을 받지 않는다.
    private bool IsFinisher => _type == AttackType.Basic && _step == Controller.ComboStepCount - 1;
    // 휘두르는 중 선입력을 저장해 공격 동작이 끝나면 기본 공격으로 이어가는 공격. 대시 공격 · 패링 추격 · 빠른 지원 뒤에는 콤보 1단으로 이어진다.
    private bool CanBufferBasicAttack => _type == AttackType.Basic || _type == AttackType.DashAttack || _type == AttackType.ParryChase || _type == AttackType.QuickSupport;
    // 대시 공격 · 회피 반격 · 패링 추격 · 빠른 지원은 휘두르는 중 공격 키를 눌러도 차지하지 않는다. 무적 · 후딜이 붙을 동작이라 강공격으로 끊지 못하게 한다.
    private bool CanChargeInSwing => _type != AttackType.DashAttack && _type != AttackType.DodgeCounter && _type != AttackType.ParryChase && _type != AttackType.QuickSupport;

    // step : 기본 공격 단계(0부터). 기본 공격이 아니면 사용하지 않는다.
    // priorityTarget : 록온 중이 아닐 때 자동 조준보다 먼저 노릴 대상. 없으면 null
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

        // 완벽 회피 슬로모션은 피드백만 주고 과하지 않게, 회피 반격이든 다른 공격이든 공격을 시작하면 바로 끝낸다.
        Controller.EndSlowMotion();

        // 시전 중 무적인 공격은 휘두르는 동안만 무적이고, 후딜에 들어가거나 상태를 나가면 풀린다.
        Controller.Status.SetAttackInvincible(_action.IsInvincibleOnSwing);

        StartMotion(0);
    }

    // 켜 둔 판정 구간은 공격이 끝나거나 끊기면 함께 끈다.
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

    // index 번째 동작을 시작한다. 대상 쪽으로 돌아 방향과 전진 거리를 정하고 그 동작의 모션을 재생한다.
    private void StartMotion(int index)
    {
        Controller.AttackJudge.EndHit();
        _motionIndex = index;
        _motion = _action.GetStep(index);
        _animationHash = _motion.AnimationHash;
        _advanceTimer = 0f;

        // 이동 입력으로 방향을 바꾸는 것은 첫 동작에서만 한다. 콤보 2단부터도 바꾸지 않는다. 도중에 방향이 휙휙 바뀌면 모션이 어색하기 때문이다.
        bool canTurnToInput = index == 0 && (_type != AttackType.Basic || _step == 0);
        float advanceDistance = Controller.AimAttack(_motion, canTurnToInput, _priorityTarget);

        _advanceVelocity = Controller.transform.forward * (advanceDistance / _motion.AdvanceDuration);

        // 동작의 전진 거리보다 멀리 다가가야 하면 다가가는 동안 하체에 달리기 모션을 보여준다. 액션마다 끌 수 있다.
        // 항상 달리기를 켠 액션은 대상이 없거나 가까워도 전진하는 동안 보여준다.
        bool isFarAdvance = advanceDistance > _motion.AdvanceDistance;
        Controller.Animation.SetLegRun(_action.UseLegRun && (_action.AlwaysLegRun || isFarAdvance) && advanceDistance > 0f);
        Controller.HorizontalVelocity = _advanceVelocity;
        Controller.Animation.Play(_animationHash, _motion.FadeTime);
    }

    // 전진 시간 동안만 앞으로 나아가고, 그 뒤에는 제자리에 멈추며 하체 달리기도 끈다.
    private Vector3 UpdateAdvance()
    {
        _advanceTimer += Controller.DeltaTime;

        if(_advanceTimer < _motion.AdvanceDuration) return _advanceVelocity;

        Controller.Animation.SetLegRun(false);
        return Vector3.zero;
    }

    // 마무리 공격 중이거나 스킬을 휘두르는 중이면 공격 입력을 받지 않는다. 차지도 시작하지 않는다.
    // 그 외에는 공격 키를 누르면 차지 시간 계산을 시작한다. 단, 대시 공격 · 회피 반격을 휘두르는 중에는 차지하지 않는다.
    // 휘두르는 중이면 선입력 허용 이후에만 다음 단계를 저장하고, 후딜 중이면 바로 다음 공격을 시작한다.
    // 상태가 바뀌었으면 true 를 돌려준다.
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

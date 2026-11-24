using UnityEngine;

// 콤보 스킬 상태. 그로기 적 둘레로 순간 이동한 뒤 콤보 스킬 액션의 동작을 순서대로 쓰고 이동으로 돌아간다.
// 끝까지 무적이고 입력을 읽지 않는다. 적 시간은 멈추지 않는다.
// 같은 상태를 연속 재생하면 이전 회차의 이벤트와 구분할 수 없어, 같은 클립이라도 단계마다 Animator 상태를 나눈다.
public class PlayerComboSkillState : PlayerState
{
    private readonly PlayerActionTrigger _trigger;
    private IEnemyTargetable _target;
    private Vector3 _advanceVelocity;
    private float _advanceTimer;
    private float _advanceDuration;
    private int _step;
    private int _animationHash;

    public PlayerComboSkillState(PlayerController controller, PlayerActionTrigger trigger) : base(controller)
    {
        _trigger = trigger;
    }

    public bool IsActive { get; private set; }
    // 첫 동작을 시작하기 전(-1)에는 동작이 없다.
    public override bool CanSwap => _step >= 0 && _trigger.AllowsSwap(_trigger.ComboSkillAction.GetStep(_step));
    public override ActionData CurrentAction => _trigger.ComboSkillAction;
    public override ActionStep CurrentStep => _step >= 0 ? _trigger.ComboSkillAction.GetStep(_step) : null;

    public void Setup(IEnemyTargetable target) => _target = target;

    public override void Enter()
    {
        IsActive = true;

        // 남아 있을 수 있는 대시 무적 · 완벽 회피 판정을 지우고 끝까지 공격 무적으로 둔다.
        Controller.Status.EndInvincible();
        Controller.Status.SetAttackInvincible(true);

        // 이전 캐릭터 자리에서 들어오므로 남아 있는 이전 캐릭터 위치를 기준으로 비켜 선다.
        TeleportManager.TryTeleportTo(Controller, _target.transform, _trigger.ComboSkillAction.Teleport);

        _step = -1;
        StartNextStep();
    }

    public override void Exit()
    {
        Controller.AttackJudge.EndHit();
        IsActive = false;
        Controller.Status.SetAttackInvincible(false);
        Controller.Party.ComboSelector.OnComboSkillEnded(Controller);
    }

    public override void Tick() => Controller.HorizontalVelocity = UpdateAdvance();

    public override void OnAnimationEvent(PlayerAnimationEventType type, int stateHash)
    {
        // 이전 단계 클립과 섞이는 동안 들어오는 이전 클립의 이벤트는 무시한다.
        if(stateHash != _animationHash) return;

        switch(type)
        {
            case PlayerAnimationEventType.HitStart:
                Controller.AttackJudge.BeginHit(_trigger.ComboSkillAction, _trigger.ComboSkillAction.GetStep(_step));
                break;
            case PlayerAnimationEventType.HitEnd:
                Controller.AttackJudge.FinishHit();
                break;
            case PlayerAnimationEventType.ImpactEnd:
                Controller.AttackJudge.PerformImpact(_trigger.ComboSkillAction, _trigger.ComboSkillAction.GetStep(_step));
                break;
            case PlayerAnimationEventType.AttackMotionEnd:
                StartNextStep();
                break;
        }
    }

    private void StartNextStep()
    {
        Controller.AttackJudge.EndHit();
        _step++;

        if(_step >= _trigger.ComboSkillAction.StepCount)
        {
            Controller.EnterLocomotion(false);
            return;
        }

        ActionStep step = _trigger.ComboSkillAction.GetStep(_step);

        float advanceDistance = Controller.AimFixedTarget(step, _target);

        _advanceDuration = step.AdvanceDuration;
        _advanceTimer = 0f;
        _advanceVelocity = Controller.transform.forward * (advanceDistance / _advanceDuration);

        _animationHash = step.AnimationHash;
        Controller.Animation.Play(_animationHash, step.FadeTime);
    }

    private Vector3 UpdateAdvance()
    {
        _advanceTimer += Controller.DeltaTime;

        return _advanceTimer < _advanceDuration ? _advanceVelocity : Vector3.zero;
    }
}

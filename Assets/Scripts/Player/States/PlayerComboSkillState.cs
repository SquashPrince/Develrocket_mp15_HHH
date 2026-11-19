using UnityEngine;

// 콤보 스킬 상태. 그로기 적 둘레로 순간 이동한 뒤 콤보 스킬 액션의 동작 목록을 순서대로 한 번 쓰고 이동으로 돌아간다.
// 한 번의 콤보 스킬은 필살기처럼 단계 설정 수만큼의 동작으로 이루어진다. 단계 수와 연계 수는 관계없다.
// 누가 언제 쓸지는 파티의 콤보 스킬 선택이 정한다. 콤보 스킬 중 강타가 맞으면 판정 기능이 콤보 스킬 선택에 알려,
//   연계가 남았으면 다음 캐릭터가 이어받는다. 이 캐릭터는 남은 단계를 마친 뒤 이동 상태로 돌아가며 퇴장한다.
// 각 단계는 모션 끝 이벤트(AttackMotionEnd)를 받으면 다음 단계로 넘어간다.
// 같은 상태를 연속으로 다시 재생하면 처음부터 재생되지 않거나 이전 회차의 이벤트와 구분할 수 없어, 같은 클립이라도 단계마다 상태를 나눈다.
// 단계마다 일반 공격처럼 대상 앞 정지 거리까지 다가간다.
// 발동부터 끝까지 무적이고, 어떤 입력으로도 끊기지 않도록 입력을 읽지 않는다. 적 시간은 멈추지 않는다.
public class PlayerComboSkillState : PlayerState
{
    private readonly PlayerActionTrigger _trigger;
    private IEnemyTargetable _target;
    private Vector3 _advanceVelocity;
    private float _advanceTimer;
    private float _advanceDuration;
    // 지금 몇 번째 단계인지(0부터)
    private int _step;
    private int _animationHash;

    public PlayerComboSkillState(PlayerController controller, PlayerActionTrigger trigger) : base(controller)
    {
        _trigger = trigger;
    }

    public bool IsActive { get; private set; }
    // 지금 동작의 끼어들기 허용을 따른다. 첫 동작을 시작하기 전(-1)에는 교체하지 않는다.
    public override bool CanSwap => _step >= 0 && _trigger.AllowsSwap(_trigger.ComboSkillAction.GetStep(_step));
    public override ActionData CurrentAction => _trigger.ComboSkillAction;

    // target : 콤보 스킬 대상인 그로기 적. 순간 이동과 단계별 접근의 대상이 된다.
    public void Setup(IEnemyTargetable target) => _target = target;

    public override void Enter()
    {
        IsActive = true;

        // 남아 있을 수 있는 대시 무적 · 완벽 회피 판정을 지운 뒤, 콤보 스킬이 끝날 때까지 무적으로 둔다.
        Controller.Status.EndInvincible();
        Controller.Status.SetAttackInvincible(true);

        // 콤보 스킬 액션의 순간 이동 옵션대로 대상 둘레로 비켜 선 곳에 순간 이동한다. 막혀 있으면 제자리에 있는다.
        // 이전 캐릭터 자리에서 들어오므로, 남아 있는 이전 캐릭터 위치를 기준으로 비켜 선다.
        TeleportManager.TryTeleportTo(Controller, _target.transform, _trigger.ComboSkillAction.Teleport);

        _step = -1;
        StartNextStep();
    }

    // 끝나면 콤보 스킬 선택에 알린다. 다음 캐릭터를 고르지 않은 채 끝났고 연계가 남았으면 강타 대기로 넘어간다.
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

    // 다음 단계로 넘어간다. 마지막 단계가 끝났으면 이동 상태로 돌아간다.
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

        // 대상인 그로기 적 쪽으로 돌아 대상 앞 정지 거리까지, 단계의 전진 시간 동안 다가간다. 도중에 록온이 바뀌어도 대상은 그대로다.
        float advanceDistance = Controller.AimFixedTarget(step, _target);

        _advanceDuration = step.AdvanceDuration;
        _advanceTimer = 0f;
        _advanceVelocity = Controller.transform.forward * (advanceDistance / _advanceDuration);

        _animationHash = step.AnimationHash;
        Controller.Animation.Play(_animationHash, step.FadeTime);
    }

    // 전진 시간 동안만 앞으로 나아가고, 그 뒤에는 제자리에 멈춘다.
    private Vector3 UpdateAdvance()
    {
        _advanceTimer += Controller.DeltaTime;

        return _advanceTimer < _advanceDuration ? _advanceVelocity : Vector3.zero;
    }
}

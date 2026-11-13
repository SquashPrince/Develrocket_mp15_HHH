using UnityEngine;

// 콤보 스킬 상태. 그로기 적 앞으로 순간 이동한 뒤 콤보 스킬 액션의 동작 목록을 순서대로 이어 쓰고 이동으로 돌아간다.
// 한 번의 콤보 스킬은 필살기처럼 단계 설정 수만큼의 동작으로 이루어진다. 단계 수와 연계 수는 관계없다.
// 콤보 스킬 중 대상에게 강타(Impact)가 맞으면 다음 연계를 예약하고, 마지막 단계가 끝나면 콤보 스킬을 처음 단계부터 다시 쓴다.
// 연계는 대상 적의 최대 연계 수까지만 이어진다. 강타가 맞지 않았거나 최대 연계 수에 닿으면 이동 상태로 돌아간다.
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
    // 대상 적의 최대 연계 수와, 지금 몇 번째 연계인지(0부터)
    private int _maxChain;
    private int _chain;
    // 지금 연계에서 몇 번째 단계인지(0부터)
    private int _step;
    private int _animationHash;
    // 이번 연계 중 대상에게 강타가 맞아 다음 연계가 예약됐는지
    private bool _isNextChainQueued;

    public PlayerComboSkillState(PlayerController controller, PlayerActionTrigger trigger) : base(controller)
    {
        _trigger = trigger;
    }

    public bool IsActive { get; private set; }

    // target : 콤보 스킬을 발동시킨 그로기 적. 순간 이동과 단계별 접근의 대상이 된다.
    // maxChain : 그 적의 최대 연계 수
    public void Setup(IEnemyTargetable target, int maxChain)
    {
        _target = target;
        _maxChain = maxChain;
    }

    // 콤보 스킬 중 강타가 맞은 적이 대상이면 다음 연계를 예약한다. 최대 연계 수에 닿았으면 예약하지 않는다.
    public void QueueChain(Transform hitEnemy)
    {
        if(!IsActive || hitEnemy != _target.transform || _chain + 1 >= _maxChain) return;

        _isNextChainQueued = true;
    }

    public override void Enter()
    {
        IsActive = true;
        _chain = 0;

        // 남아 있을 수 있는 대시 무적 · 완벽 회피 판정을 지운 뒤, 콤보 스킬이 끝날 때까지 무적으로 둔다.
        // 공격 무적 중에는 패링 입력도 무시되므로 패링 · 교체 키로도 끊기지 않는다.
        Controller.Status.EndInvincible();
        Controller.Status.SetAttackInvincible(true);

        // 콤보 스킬 액션의 순간 이동 옵션대로 대상 둘레로 비켜 선 곳에 순간 이동한다. 막혀 있으면 제자리에 있는다.
        TeleportManager.TryTeleportTo(Controller, _target.transform, _trigger.ComboSkillAction.Teleport);
        StartChain();
    }

    public override void Exit()
    {
        Controller.AttackJudge.EndHit();
        IsActive = false;
        Controller.Status.SetAttackInvincible(false);
    }

    public override void Tick() => Controller.HorizontalVelocity = UpdateAdvance();

    public override void OnAnimationEvent(PlayerAnimationEventType type, int stateHash)
    {
        // 이전 단계 클립과 섞이는 동안 들어오는 이전 클립의 이벤트는 무시한다.
        if(stateHash != _animationHash) return;

        switch(type)
        {
            case PlayerAnimationEventType.HitStart:
                Controller.AttackJudge.BeginHit(PlayerAttackState.AttackType.ComboSkill, _trigger.ComboSkillAction, _trigger.ComboSkillAction.GetStep(_step));
                break;
            case PlayerAnimationEventType.HitEnd:
                Controller.AttackJudge.EndHit();
                break;
            case PlayerAnimationEventType.ImpactEnd:
                Controller.AttackJudge.PerformImpact(PlayerAttackState.AttackType.ComboSkill, _trigger.ComboSkillAction, _trigger.ComboSkillAction.GetStep(_step));
                break;
            case PlayerAnimationEventType.AttackMotionEnd:
                StartNextStep();
                break;
        }
    }

    // 콤보 스킬을 처음 단계부터 쓴다.
    private void StartChain()
    {
#if UNITY_EDITOR
        Debug.Log($"[콤보 스킬] 연계 {_chain + 1} / {_maxChain}");
#endif

        _step = -1;
        _isNextChainQueued = false;
        StartNextStep();
    }

    // 다음 단계로 넘어간다. 마지막 단계가 끝났으면, 다음 연계가 예약됐을 때만 콤보 스킬을 다시 쓰고 아니면 이동 상태로 돌아간다.
    private void StartNextStep()
    {
        Controller.AttackJudge.EndHit();
        _step++;

        if(_step >= _trigger.ComboSkillAction.StepCount)
        {
            if(!_isNextChainQueued)
            {
                Controller.EnterLocomotion(false);
                return;
            }

            _chain++;
            StartChain();
            return;
        }

        ActionStep step = _trigger.ComboSkillAction.GetStep(_step);

        // 발동시킨 그로기 적 쪽으로 돌아 대상 앞 정지 거리까지, 단계의 전진 시간 동안 다가간다. 도중에 록온이 바뀌어도 대상은 그대로다.
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

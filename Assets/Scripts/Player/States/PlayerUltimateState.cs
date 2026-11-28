using UnityEngine;

// 필살기 상태. 연출 → 필살기 액션의 동작 순서대로 → 이동으로 돌아간다.
// 어떤 입력으로도 끊기지 않게 입력을 읽지 않고 끝까지 무적이다. 적 · 다른 파티원은 연출 구간 동안만 멈춘다.
// 자세한 흐름 : Docs/Technical/Player/PlayerUltimate.md
public class PlayerUltimateState : PlayerState
{
    private const int CUTSCENE_STEP = -1;

    private static readonly int CUTSCENE_ANIMATION = Animator.StringToHash("UltimateCutscene");

    private readonly PlayerActionTrigger _trigger;
    private PlayerUltimateCutscene _cutscene;
    private bool _isActive;
    private IEnemyTargetable _target;
    private Vector3 _advanceVelocity;
    private float _advanceTimer;
    private float _advanceDuration;
    private int _step;
    private int _animationHash;

    public PlayerUltimateState(PlayerController controller, PlayerActionTrigger trigger) : base(controller)
    {
        _trigger = trigger;
    }

    // 연출 구간은 액션의 동작이 아니라 끼어들기 값이 없으므로 교체하지 않는다.
    public override bool CanSwap => _step != CUTSCENE_STEP && _trigger.AllowsSwap(_trigger.UltimateAction.GetStep(_step));
    public override ActionStep CurrentStep => _step != CUTSCENE_STEP ? _trigger.UltimateAction.GetStep(_step) : null;

    public override void Enter()
    {
        _isActive = true;
        _step = CUTSCENE_STEP;
        _animationHash = CUTSCENE_ANIMATION;
        _advanceVelocity = Vector3.zero;

        // 슬로모션은 끝날 때 적 배속을 1로 되돌리므로, 먼저 끝낸 뒤 적을 멈춰야 연출 도중 적이 움직이지 않는다.
        Controller.EndSlowMotion();
        EnemyTime.Scale = 0f;
        Controller.Party.SetOthersFrozen(Controller, true);

        // 히트렉으로 멈춘 순간 발동해도 원래 속도로 시작한다.
        Controller.Status.EndHitLag();

        // 대시 직후 남은 대시 무적 · 완벽 회피 판정을 지우고 끝까지 공격 무적으로 둔다. 공격 무적 중에는 패링 입력도 무시된다.
        Controller.Status.EndInvincible();
        Controller.Status.SetAttackInvincible(true);

        // 단계 사이에 자동 조준 대상이 바뀌지 않게 발동 때 정해 둔다.
        _target = _trigger.FindUltimateTarget();

        if(_target != null) TeleportManager.TryTeleportTo(Controller, _target.transform, _trigger.UltimateAction.Teleport);

        Controller.HorizontalVelocity = Vector3.zero;
        _cutscene = _trigger.CreateUltimateCutscene();
        if(_cutscene != null) _cutscene.Begin(Controller.Party.MainCamera);
        else Controller.Animation.Play(CUTSCENE_ANIMATION);
    }

    // 어떤 경우에도 적 · 다른 파티원이 멈춘 채 남지 않도록 나갈 때도 돌린다.
    public override void Exit()
    {
        if(!_isActive) return;
        _isActive = false;
        EndCutscene();
        Controller.AttackJudge.EndHit();
        ResumeOthers();
        Controller.Status.SetAttackInvincible(false);
    }

    public override void Tick()
    {
        if(!_isActive) return;

        if(_cutscene != null && Controller.Party.IsBattleEnded)
        {
            CancelCutscene();
            return;
        }

        if(_cutscene != null && _cutscene.Advance(Time.deltaTime))
        {
            EndCutscene();
            StartNextStep();
            return;
        }

        Controller.HorizontalVelocity = UpdateAdvance();
    }

    public void CancelCutscene()
    {
        if(_cutscene != null) Controller.EnterLocomotion(false);
    }

    private void EndCutscene()
    {
        if(_cutscene == null) return;

        _cutscene.End();
        _cutscene.gameObject.SetActive(false);
        Object.Destroy(_cutscene.gameObject);
        _cutscene = null;
    }

    public override void OnAnimationEvent(PlayerAnimationEventType type, int stateHash)
    {
        // 이전 구간 클립과 섞이는 동안 들어오는 이전 클립의 이벤트는 무시한다.
        if(!_isActive || _cutscene != null || stateHash != _animationHash) return;

        switch(type)
        {
            case PlayerAnimationEventType.HitStart:
                if(_step != CUTSCENE_STEP) Controller.AttackJudge.BeginHit(_trigger.UltimateAction, _trigger.UltimateAction.GetStep(_step));
                break;
            case PlayerAnimationEventType.HitEnd:
                Controller.AttackJudge.FinishHit();
                break;
            case PlayerAnimationEventType.ImpactEnd:
                if(_step != CUTSCENE_STEP) Controller.AttackJudge.PerformImpact(_trigger.UltimateAction, _trigger.UltimateAction.GetStep(_step));
                break;
            case PlayerAnimationEventType.AttackMotionEnd:
                StartNextStep();
                break;
        }
    }

    private void StartNextStep()
    {
        Controller.AttackJudge.EndHit();

        if(_step == CUTSCENE_STEP) ResumeOthers();

        _step++;

        if(_step >= _trigger.UltimateAction.StepCount)
        {
            Controller.EnterLocomotion(false);
            return;
        }

        ActionStep step = _trigger.UltimateAction.GetStep(_step);

        float advanceDistance = Controller.AimFixedTarget(step, _target);

        _advanceDuration = step.AdvanceDuration;
        _advanceTimer = 0f;
        _advanceVelocity = Controller.transform.forward * (advanceDistance / _advanceDuration);

        _animationHash = step.AnimationHash;
        Controller.Animation.Play(_animationHash, step.FadeTime);
    }

    private Vector3 UpdateAdvance()
    {
        if(_step == CUTSCENE_STEP) return Vector3.zero;

        _advanceTimer += Controller.DeltaTime;

        return _advanceTimer < _advanceDuration ? _advanceVelocity : Vector3.zero;
    }

    private void ResumeOthers()
    {
        EnemyTime.Scale = 1f;
        Controller.Party.SetOthersFrozen(Controller, false);
    }
}

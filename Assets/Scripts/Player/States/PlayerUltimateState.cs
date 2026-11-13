using UnityEngine;

// 필살기 상태. 연출(UltimateCutscene) → 필살기 액션의 동작 목록 순서대로 → 이동으로 돌아간다.
// 발동하면 대상 앞으로 순간 이동한다. 대상이 없거나 막혀 있으면 제자리에서 쓴다.
// 각 구간은 모션 끝 이벤트(AttackMotionEnd)를 받으면 다음 구간으로 넘어가고, 마지막 공격 단계가 끝나면 이전 상태가 아닌 이동 상태로 돌아간다.
// 단계 사이는 Animator 전이선 대신 코드가 동작에 적힌 상태를 재생하며, 섞는 시간은 동작의 값을 쓴다.
// 연출 중에는 움직이지 않고, 공격 단계마다 일반 공격처럼 대상 앞 정지 거리까지 다가간다.
// 필살기 중에는 어떤 입력으로도 끊기지 않도록 입력을 읽지 않고, 발동부터 끝까지 무적이다. 적은 연출 구간 동안만 멈춘다.
// 필살기에 맞은 적은 경직하며, 경직은 적이 진행 중이던 공격을 끊는다.
public class PlayerUltimateState : PlayerState
{
    // 연출 구간을 뜻하는 단계 번호
    private const int CUTSCENE_STEP = -1;

    private static readonly int CUTSCENE_ANIMATION = Animator.StringToHash("UltimateCutscene");

    private readonly PlayerUltimate _ultimate;
    private IEnemyTargetable _target;
    private Vector3 _advanceVelocity;
    private float _advanceTimer;
    private float _advanceDuration;
    private int _step;
    private int _animationHash;

    public PlayerUltimateState(PlayerController controller, PlayerUltimate ultimate) : base(controller)
    {
        _ultimate = ultimate;
    }

    public override void Enter()
    {
        _step = CUTSCENE_STEP;
        _animationHash = CUTSCENE_ANIMATION;
        _advanceVelocity = Vector3.zero;

        // 연출 구간 동안만 적을 멈추고, 공격 단계가 시작되면 원래 속도로 돌린다.
        // 슬로모션은 끝날 때 적 배속을 1로 되돌리므로, 먼저 끝낸 뒤 적을 멈춰야 연출 도중 적이 다시 움직이지 않는다.
        Controller.EndSlowMotion();
        EnemyTime.Scale = 0f;

        // 대시 공격 직후 발동하면 대시 무적 · 완벽 회피 판정이 남아 있을 수 있어 지운 뒤, 필살기가 끝날 때까지 무적으로 둔다.
        // 공격 무적 중에는 패링 입력도 무시되므로 패링 · 교체 키로도 끊기지 않는다.
        Controller.Status.EndInvincible();
        Controller.Status.SetAttackInvincible(true);

        // 공격 단계마다 다가갈 대상을 발동 때 정해 두어, 단계 사이에 자동 조준 대상이 바뀌지 않게 한다.
        // 필살기 액션의 순간 이동 옵션대로 대상 앞에 순간 이동한다. 대상이 없거나 막혀 있으면 제자리에서 쓴다.
        _target = _ultimate.FindTarget();

        if(_target != null) Controller.Teleport.TryTeleportTo(_target.transform, _ultimate.Action.Teleport);

        Controller.HorizontalVelocity = Vector3.zero;
        Controller.Animation.Play(CUTSCENE_ANIMATION);
    }

    // 슬로모션 중 발동했더라도 적은 원래 속도로 돌아온다. 공격 단계 시작 때 이미 돌렸지만, 어떤 경우에도 멈춘 채 남지 않도록 나갈 때도 돌린다.
    public override void Exit()
    {
        EnemyTime.Scale = 1f;
        Controller.Status.SetAttackInvincible(false);
    }

    public override void Tick() => Controller.HorizontalVelocity = UpdateAdvance();

    public override void OnAnimationEvent(PlayerAnimationEventType type, int stateHash)
    {
        // 이전 구간 클립과 섞이는 동안 들어오는 이전 클립의 이벤트는 무시한다.
        if(stateHash != _animationHash) return;

        switch(type)
        {
            case PlayerAnimationEventType.Hit:
            case PlayerAnimationEventType.Impact:
                if(_step != CUTSCENE_STEP) Controller.AttackJudge.PerformHit(PlayerAttackState.AttackType.Ultimate, _ultimate.Action, _ultimate.Action.GetStep(_step), type == PlayerAnimationEventType.Impact);
                break;
            case PlayerAnimationEventType.AttackMotionEnd:
                StartNextStep();
                break;
        }
    }

    private void StartNextStep()
    {
        // 연출이 끝나고 첫 공격 단계가 시작되면 적 시간이 다시 흐른다.
        if(_step == CUTSCENE_STEP) EnemyTime.Scale = 1f;

        _step++;

        if(_step >= _ultimate.Action.StepCount)
        {
            Controller.EnterLocomotion(false);
            return;
        }

        ActionStep step = _ultimate.Action.GetStep(_step);

        // 발동 때 정한 대상 쪽으로 돌아 대상 앞 정지 거리까지, 단계의 전진 시간 동안 다가간다. 도중에 록온이 바뀌어도 대상은 그대로다.
        float advanceDistance = Controller.AimFixedTarget(step, _target);

        _advanceDuration = step.AdvanceDuration;
        _advanceTimer = 0f;
        _advanceVelocity = Controller.transform.forward * (advanceDistance / _advanceDuration);

        _animationHash = step.AnimationHash;
        Controller.Animation.Play(_animationHash, step.FadeTime);
    }

    // 전진 시간 동안만 앞으로 나아가고, 그 뒤와 연출 중에는 제자리에 멈춘다.
    private Vector3 UpdateAdvance()
    {
        if(_step == CUTSCENE_STEP) return Vector3.zero;

        _advanceTimer += Time.deltaTime;

        return _advanceTimer < _advanceDuration ? _advanceVelocity : Vector3.zero;
    }
}

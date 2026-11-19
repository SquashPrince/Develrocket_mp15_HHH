using UnityEngine;

// 패링 상태. 준비(ParryReady) → 패링 성공 시 반응(ParryReact) → 추격 공격 또는 이동으로 돌아간다.
// 준비 : 패링 공격을 하는 적 앞으로 순간 이동한 뒤 자세를 유지한다. 적 공격이 끊겨도 정해진 시간까지 유지하고, 그동안 노란 공격이 오지 않으면 이동으로 돌아간다.
// 반응 : 공격한 적 반대 방향으로 점점 느려지며 밀려난다. 밀려나는 동안은 무적이다. 패링 한 번은 노란 공격 한 번만 막는다. 반응 중 닿은 노란 공격은 패링되지 않는다.
//        적이 이어서 패링 공격을 하면(연속 패링 패턴) 다시 패링 입력을 해야 막을 수 있다.
// 추격 : 반응 동안 공격 키를 눌렀으면 반응이 끝난 뒤 추격 공격이 나간다.
//        추격 대상은 록온 중이면 록온 대상, 아니면 패링한 적이다.
// 노란색이 아닌 공격은 패링되지 않고 기존처럼 맞는다.
// 준비 · 반응 모션은 패링 액션의 동작 목록에서 읽는다. 첫 동작이 준비, 두 번째 동작이 반응이다.
public class PlayerParryState : PlayerState
{
    // 패링 액션의 동작 번호
    public const int READY_STEP = 0;
    public const int REACT_STEP = 1;

    private enum Phase
    {
        Ready,
        React
    }

    private readonly PlayerActionTrigger _trigger;
    private IEnemyTargetable _parriedTarget;
    private Vector3 _awayDirection;
    private Phase _phase;
    private float _timer;
    private bool _hasChaseInput;

    public PlayerParryState(PlayerController controller, PlayerActionTrigger trigger) : base(controller)
    {
        _trigger = trigger;
    }

    // 패링 준비 중인지. 이 동안 닿은 노란 공격은 패링된다. 패링에 성공하면 꺼져, 한 번의 패링으로 노란 공격 하나만 막는다.
    public bool IsActive { get; private set; }
    // 준비 중에는 패링 액션의 준비 동작, 성공 반응 중에는 반응 동작의 끼어들기 허용을 따른다.
    public override bool CanSwap => _trigger.AllowsSwap(_trigger.ParryAction.GetStep(_phase == Phase.Ready ? READY_STEP : REACT_STEP));
    public override ActionData CurrentAction => _trigger.ParryAction;

    public override void Enter()
    {
        IsActive = true;
        _phase = Phase.Ready;
        _timer = 0f;

        // 패링 액션의 순간 이동 옵션대로 고른 적 앞에 순간 이동한다. 도착 지점이 막혀 있으면 제자리에서 패링한다.
        TeleportManager.TryTeleportTo(Controller, _trigger.SelectedParryAttacker.transform, _trigger.ParryAction.Teleport);

        // 패링할 적으로 록온을 옮긴다. 패링에 실패해도 록온은 남는다.
        if(_trigger.SelectedParryAttacker.transform.TryGetComponent(out IEnemyTargetable parryTarget)) Controller.Party.LockOn.LockOnTo(parryTarget);
        Controller.HorizontalVelocity = Vector3.zero;
        PlayStep(READY_STEP);
        BattleFeedback.Raise(BattleFeedback.Moment.ParryStart);
    }

    public override void Exit() => IsActive = false;

    // 공격한 적 쪽을 보고, 그 반대 방향으로 밀려나기 시작한다. 이후 닿는 노란 공격은 이 패링으로 막지 않는다.
    // 밀려나는 동안(반응 시간)은 무적이라, 성공 → 추격 사이에 어떤 공격에도 맞지 않는다. 반응 중 온 노란 공격도 흘려보낸다.
    // 패링한 적을 기억해, 록온 중이 아니면 추격 공격이 그 적에게 나가게 한다.
    public void OnParrySuccess(IEnemyTargetable parriedTarget)
    {
        IsActive = false;
        Controller.Status.BeginInvincible(_trigger.ParryReactTime);
        _parriedTarget = parriedTarget;
        _awayDirection = Controller.transform.position - parriedTarget.transform.position;
        _awayDirection.y = 0f;

        // 적과 위치가 겹치면 방향을 알 수 없으므로 뒤로 밀려난다.
        if(_awayDirection == Vector3.zero) _awayDirection = -Controller.transform.forward;

        _awayDirection.Normalize();
        Controller.FaceImmediately(-_awayDirection);

        _phase = Phase.React;
        _timer = 0f;
        _hasChaseInput = false;
        PlayStep(REACT_STEP);
        BattleFeedback.Raise(BattleFeedback.Moment.ParrySuccess, _awayDirection);
    }

    public override void Tick()
    {
        _timer += Controller.DeltaTime;

        if(_phase == Phase.Ready) UpdateReady();
        else UpdateReact();
    }

    private void UpdateReady()
    {
        Controller.HorizontalVelocity = Vector3.zero;

        if(_timer >= _trigger.ParryReadyTime) Controller.EnterLocomotion(false);
    }

    private void UpdateReact()
    {
        if(Controller.WasAttackPressed) _hasChaseInput = true;

        if(_timer < _trigger.ParryReactTime)
        {
            Controller.HorizontalVelocity = _awayDirection * GetPushSpeed();
            return;
        }

        Controller.HorizontalVelocity = Vector3.zero;

        if(_hasChaseInput) Controller.StartAttack(PlayerAttackState.AttackType.ParryChase, _trigger.ParryChaseAction, _parriedTarget);
        else Controller.EnterLocomotion(false);
    }

    // 밀려나는 속도는 처음에 가장 빠르고 반응 시간에 걸쳐 0까지 일정하게 줄어든다.
    // 속도가 일정하게 줄면 이동 거리는 처음 속도 × 시간 ÷ 2 이므로, 처음 속도를 거리 × 2 ÷ 시간으로 잡아 정해진 거리만큼 밀려나게 한다.
    // Lerp 는 t 를 0 ~ 1 로 자르므로 반응 시간이 지나면 0 이 된다.
    private float GetPushSpeed()
    {
        float startSpeed = 2f * _trigger.ParryReactDistance / _trigger.ParryReactTime;
        float t = _timer / _trigger.ParryReactTime;

        return Mathf.Lerp(startSpeed, 0f, t);
    }

    // 패링 액션의 index 번째 동작 모션을 그 동작의 섞는 시간으로 재생한다.
    private void PlayStep(int index)
    {
        ActionStep step = _trigger.ParryAction.GetStep(index);

        Controller.Animation.Play(step.AnimationHash, step.FadeTime);
    }
}

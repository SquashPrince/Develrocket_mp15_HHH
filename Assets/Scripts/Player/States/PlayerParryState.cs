using UnityEngine;

// 패링 상태. 준비 → 성공 시 반응(밀려남 · 무적) → 반응 중 공격 입력이 있었으면 추격 공격, 없으면 이동.
// 패링 한 번은 노란 공격 하나만 막는다. 연속 패링 패턴은 다시 패링 입력을 해야 막는다.
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

    // 패링에 성공하면 꺼진다.
    public bool IsActive { get; private set; }
    public override bool CanSwap => _trigger.AllowsSwap(_trigger.ParryAction.GetStep(_phase == Phase.Ready ? READY_STEP : REACT_STEP));
    public override ActionData CurrentAction => _trigger.ParryAction;
    public override ActionStep CurrentStep => _trigger.ParryAction.GetStep(_phase == Phase.Ready ? READY_STEP : REACT_STEP);

    public override void Enter()
    {
        IsActive = true;
        _phase = Phase.Ready;
        _timer = 0f;

        // 도착 지점이 막혀 있으면 제자리에서 패링한다.
        TeleportManager.TryTeleportTo(Controller, _trigger.SelectedParryAttacker.transform, _trigger.ParryAction.Teleport);

        // 패링에 실패해도 록온은 남는다.
        if(_trigger.SelectedParryAttacker.transform.TryGetComponent(out IEnemyTargetable parryTarget)) Controller.Party.LockOn.LockOnTo(parryTarget);
        Controller.HorizontalVelocity = Vector3.zero;
        PlayStep(READY_STEP);
        BattleFeedback.Raise(BattleFeedback.Moment.ParryStart);
    }

    public override void Exit() => IsActive = false;

    // 반응 시간 동안 필드의 파티 전체가 무적이라, 성공 → 추격 사이에 어떤 공격에도 맞지 않는다.
    public void OnParrySuccess(IEnemyTargetable parriedTarget)
    {
        IsActive = false;
        Controller.Party.BeginFieldInvincible(_trigger.ParryReactTime);
        _parriedTarget = parriedTarget;
        _awayDirection = HorizontalVector.Between(parriedTarget.transform.position, Controller.transform.position);

        // 적과 위치가 겹치면 방향을 알 수 없으므로 뒤로 밀려난다.
        if(_awayDirection == Vector3.zero) _awayDirection = -Controller.transform.forward;

        _awayDirection.Normalize();
        Controller.FaceImmediately(-_awayDirection);

        _phase = Phase.React;
        _timer = 0f;
        _hasChaseInput = false;
        PlayStep(REACT_STEP);
        PlayParryEffect(parriedTarget.transform.position);
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

    // 속도가 0까지 일정하게 줄면 이동 거리는 처음 속도 × 시간 ÷ 2 이므로, 처음 속도를 거리 × 2 ÷ 시간으로 잡는다.
    private float GetPushSpeed()
    {
        float startSpeed = 2f * _trigger.ParryReactDistance / _trigger.ParryReactTime;
        float t = _timer / _trigger.ParryReactTime;

        return Mathf.Lerp(startSpeed, 0f, t);
    }

    // 캐릭터와 적의 중간에 캐릭터 → 적 방향(밀려나는 방향의 반대)으로 띄운다.
    private void PlayParryEffect(Vector3 targetPosition)
    {
        Vector3 position = (Controller.transform.position + targetPosition) * 0.5f;

        position.y = Controller.transform.position.y + Controller.Common.ParryEffectHeight;
        BattleEffect.Play(Controller.Common.ParryEffectPrefab, position, Quaternion.LookRotation(-_awayDirection));
    }

    private void PlayStep(int index)
    {
        ActionStep step = _trigger.ParryAction.GetStep(index);

        Controller.Animation.Play(step.AnimationHash, step.FadeTime);
    }
}

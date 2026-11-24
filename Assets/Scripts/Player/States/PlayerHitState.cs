using UnityEngine;

// 피격 · 큰 피격(경직 · 밀림)과 넉백(밀려남 → 일어나기) 상태.
// 넉백 · 일어나기 동안은 연속으로 맞아 계속 띄워지지 않게 무적이다.
// 자세한 흐름 : Docs/Technical/Player/PlayerHit.md
public class PlayerHitState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    private static readonly int HIT_ANIMATION = Animator.StringToHash("Hit");
    private static readonly int HEAVY_HIT_ANIMATION = Animator.StringToHash("HeavyHit");
    private static readonly int KNOCKBACK_ANIMATION = Animator.StringToHash("Knockback");
    private static readonly int GET_UP_ANIMATION = Animator.StringToHash("GetUp");

    private enum Phase
    {
        Stun,
        Knockback,
        GetUp
    }

    private EnemyAttackData _attack;
    private EnemyAttackData.HitReaction _reaction;
    private Transform _attacker;
    private Vector3 _awayDirection;
    private Phase _phase;
    private float _timer;

    public PlayerHitState(PlayerController controller) : base(controller)
    {
    }

    // 경직 · 밀림 · 넉백 값은 공격(attack)의 값을 쓰고, 반응만 타격마다 받는다.
    public void Setup(EnemyAttackData attack, EnemyAttackData.HitReaction reaction, Transform attacker)
    {
        _attack = attack;
        _reaction = reaction;
        _attacker = attacker;
        _awayDirection = HorizontalVector.Between(attacker.position, Controller.transform.position);

        // 적과 위치가 겹치면 방향을 알 수 없으므로 뒤로 밀려난다.
        if(_awayDirection == Vector3.zero) _awayDirection = -Controller.transform.forward;

        _awayDirection.Normalize();
    }

    public override void Enter()
    {
        _timer = 0f;
        Controller.FaceImmediately(-_awayDirection);
        Controller.HorizontalVelocity = Vector3.zero;

        // 피격과 큰 피격은 같은 경직 흐름을 쓰고 모션만 다르다.
        if(_reaction != EnemyAttackData.HitReaction.Knockback)
        {
            _phase = Phase.Stun;
            Controller.Animation.Play(_reaction == EnemyAttackData.HitReaction.HeavyHit ? HEAVY_HIT_ANIMATION : HIT_ANIMATION);

            // 퇴장 중인 이전 캐릭터가 맞을 때는 연출하지 않는다.
            if(Controller.Party.Controlled == Controller) BattleFeedback.Raise(BattleFeedback.Moment.Hurt);

            return;
        }

        _phase = Phase.Knockback;
        Controller.Party.OpenQuickSupport(Controller, _attacker);
        Controller.Status.BeginInvincible(_attack.KnockbackDuration + _attack.GetUpTime);
        Controller.Animation.Play(KNOCKBACK_ANIMATION);

        if(Controller.Party.Controlled == Controller) BattleFeedback.Raise(BattleFeedback.Moment.Knockback);
    }

    // 넉백 중 대시로 끊으면 남은 넉백 무적을 지운다. 대시 무적은 대시 상태가 다시 건다.
    public override void Exit()
    {
        if(_phase != Phase.Stun) Controller.Status.EndInvincible();
    }

    public override void Tick()
    {
        _timer += Controller.DeltaTime;

        if(_attack.CanDashCancel && Controller.WasDashPressed && Controller.TryStartDash(Controller.ReadMoveDirection())) return;

        switch(_phase)
        {
            case Phase.Stun:
                UpdateStun();
                break;
            case Phase.Knockback:
                UpdateKnockback();
                break;
            case Phase.GetUp:
                if(_timer >= _attack.GetUpTime) Controller.EnterLocomotion(false);
                break;
        }
    }

    private void UpdateStun()
    {
        if(_timer >= _attack.HitStunTime)
        {
            Controller.EnterLocomotion(false);
            return;
        }

        bool isPushing = _timer < _attack.HitPushDuration;

        Controller.HorizontalVelocity = isPushing ? _awayDirection * (_attack.HitPushDistance / _attack.HitPushDuration) : Vector3.zero;
    }

    private void UpdateKnockback()
    {
        if(_timer < _attack.KnockbackDuration)
        {
            Controller.HorizontalVelocity = _awayDirection * (_attack.KnockbackDistance / _attack.KnockbackDuration);
            return;
        }

        Controller.HorizontalVelocity = Vector3.zero;
        _phase = Phase.GetUp;
        _timer = 0f;
        Controller.Animation.Play(GET_UP_ANIMATION);
    }
}

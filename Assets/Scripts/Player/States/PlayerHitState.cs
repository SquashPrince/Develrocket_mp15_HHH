using UnityEngine;

// 피격 · 넉백 상태. 어떤 상태에서든 적 공격에 맞으면 이 상태로 넘어온다.
// 피격 : 공격한 적 쪽을 보고 경직 시간 동안 멈춘 뒤 이동 상태로 돌아간다. 경직 중 다시 맞으면 새 피격으로 바뀐다.
//        공격 설정에 밀림 거리가 있으면 경직이 시작될 때 공격 반대 방향으로 밀려난다.
// 큰 피격 : 피격과 같은 경직 · 밀림 흐름이고, 모션만 큰 피격(HeavyHit)으로 나온다. (강공격)
// 넉백 : 공격한 적 반대 방향으로 밀려난 뒤 일어나고, 일어나기가 끝나면 이동 상태로 돌아간다. 넉백되는 순간 파티의 빠른 지원 입력 시간이 열린다.
//        넉백 · 일어나기 동안은 무적이다. 연속으로 맞으며 계속 띄워지는 것을 막기 위해서다.
// 공격 설정에서 대시 캔슬을 허용한 공격이면 피격 · 넉백 중 대시 · 회피로 끊을 수 있다.
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

    // reaction : 이 타격의 반응. 경직 · 밀림 · 넉백 값은 공격(attack)의 값을 쓴다.
    // attacker : 공격한 적. 이 적의 반대 방향으로 밀려나고, 넉백이면 파티가 빠른 지원으로 이 적 앞에 다음 캐릭터를 보낸다.
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

            // 넉백과 같이 화면의 주인공(조작 캐릭터)이 맞을 때만 알린다.
            if(Controller.Party.Controlled == Controller) BattleFeedback.Raise(BattleFeedback.Moment.Hurt);

            return;
        }

        _phase = Phase.Knockback;
        Controller.Party.OpenQuickSupport(Controller, _attacker);
        Controller.Status.BeginInvincible(_attack.KnockbackDuration + _attack.GetUpTime);
        Controller.Animation.Play(KNOCKBACK_ANIMATION);

        // 화면의 주인공이 아닌 캐릭터(퇴장 중인 이전 캐릭터 등)가 넉백될 때는 연출하지 않는다.
        if(Controller.Party.Controlled == Controller) BattleFeedback.Raise(BattleFeedback.Moment.Knockback);
    }

    // 넉백 중 대시로 끊으면 남은 넉백 무적을 지운다. 대시 무적은 대시 상태가 들어오면서 다시 건다.
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

    // 밀림 시간 동안 같은 속도로 밀려나고, 경직 시간이 지나면 이동 상태로 돌아간다.
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

    // 넉백 시간 동안 같은 속도로 밀려나고, 끝나면 멈춰서 일어나기를 재생한다.
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

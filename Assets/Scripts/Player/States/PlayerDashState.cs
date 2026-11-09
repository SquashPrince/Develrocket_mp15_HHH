using UnityEngine;

// 대시와 회피(백스텝) 상태.
// 두 동작 모두 정해진 방향으로 정해진 시간 동안 같은 속도로 이동하고, 방향 · 거리 · 시간만 다르다.
public class PlayerDashState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    private static readonly int DASH_ANIMATION = Animator.StringToHash("Dash");
    private static readonly int DODGE_ANIMATION = Animator.StringToHash("Dodge");

    private Vector3 _direction;
    private float _speed;
    private float _duration;
    private float _timer;

    public PlayerDashState(PlayerController controller) : base(controller)
    {
    }

    // 회피(백스텝)인지 대시인지. 애니메이션을 고를 때 사용한다.
    public bool IsDodge { get; private set; }

    public void Setup(Vector3 direction, float distance, float duration, bool isDodge)
    {
        _direction = direction;
        _duration = duration;
        _speed = distance / duration;
        IsDodge = isDodge;
    }

    public override void Enter()
    {
        _timer = 0f;
        // 대시 · 회피 시작부터 정해진 시간 동안 무적이고, 같은 시간 동안 완벽 회피 판정이 열린다. 연속 대시는 대시마다 다시 건다.
        Controller.Health.BeginDodgeInvincible(Controller.DashInvincibleTime);
        Controller.HorizontalVelocity = _direction * _speed;
        Controller.PlayAnimation(IsDodge ? DODGE_ANIMATION : DASH_ANIMATION);
    }

    public override void Tick()
    {
        _timer += Time.deltaTime;

        Vector3 moveDirection = Controller.ReadMoveDirection();

        // 대시 · 회피 도중 공격 입력은 언제든 대시를 끊는다. 완벽 회피에 성공했으면 회피 반격, 아니면 대시 공격이 나간다.
        // 대시 공격 · 회피 반격은 누르고 있어도 차지하지 않아 강공격으로 이어지지 않는다.
        if(Controller.WasAttackPressed)
        {
            if(Controller.IsPerfectDodge) Controller.StartDodgeCounter();
            else Controller.StartDashAttack();
            return;
        }

        // 연속 사용 간격이 지나야 다음 대시를 쓸 수 있다. 간격이 지속시간보다 길면 지속시간을 기준으로 한다.
        bool canChain = _timer >= Mathf.Min(Controller.DashChainInterval, _duration);

        if(canChain && Controller.WasDashPressed && Controller.TryStartDash(moveDirection)) return;

        if(_timer < _duration)
        {
            Controller.HorizontalVelocity = _direction * _speed;
            return;
        }

        // 끝날 때 이동 입력이 있으면 달리기로 이어가고, 없으면 멈춘다.
        Controller.HorizontalVelocity = Vector3.zero;
        Controller.EnterLocomotion(moveDirection != Vector3.zero);
    }
}

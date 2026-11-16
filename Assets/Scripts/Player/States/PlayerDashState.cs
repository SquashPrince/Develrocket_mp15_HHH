using UnityEngine;

// 대시와 회피(백스텝) 상태.
// 두 동작 모두 정해진 방향 · 거리 · 시간으로 움직이고, 방향 · 거리 · 시간 · 시작 지연만 다르다.
// 시작 지연 : 상태에 들어온 뒤 시작 지연 동안 제자리에 있다가 전진한다. 무적 · 완벽 회피 판정은 들어온 순간부터 열려,
//   몸이 공격 범위를 빠져나가기 전에 닿은 공격도 완벽 회피로 판정된다. 지연은 이동 시간 안에 포함해 전체 시간 · 거리는 그대로다.
// 감속 이동 : 전진은 처음이 가장 빠르고 끝에서 0 이 되도록 고르게 느려진다. 실제 이동 시간에 대한 진행 비율로 시작 속도에서 0 까지 보간한다.
//   속도가 고르게 줄면 움직인 거리는 시작 속도 × 시간 ÷ 2 이므로, 시작 속도를 거리 × 2 ÷ 실제 이동 시간으로 잡아 정해진 거리만큼 움직인다.
// 시작 지연 값은 대시 · 회피를 따로 공통 설정에서 읽는다.
public class PlayerDashState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    private static readonly int DASH_ANIMATION = Animator.StringToHash("Dash");
    private static readonly int DODGE_ANIMATION = Animator.StringToHash("Dodge");

    private Vector3 _direction;
    private float _startSpeed;
    private float _startDelay;
    private float _moveTime;
    private float _duration;
    private float _timer;

    public PlayerDashState(PlayerController controller) : base(controller)
    {
    }

    // 회피(백스텝)인지 대시인지. 애니메이션을 고를 때 사용한다.
    public bool IsDodge { get; private set; }

    // 시작 지연이 이동 시간보다 크거나 같으면 실제 이동 시간이 없어 이동하지 않는다.
    public void Setup(Vector3 direction, float distance, float duration, bool isDodge)
    {
        _direction = direction;
        _duration = duration;
        IsDodge = isDodge;
        _startDelay = isDodge ? Controller.Common.DodgeStartDelay : Controller.Common.DashStartDelay;
        _moveTime = duration - _startDelay;
        _startSpeed = _moveTime > 0f ? distance * 2f / _moveTime : 0f;
    }

    public override void Enter()
    {
        _timer = 0f;
        // 대시 · 회피 시작부터 정해진 시간 동안 무적이고, 같은 시간 동안 완벽 회피 판정이 열린다. 연속 대시는 대시마다 다시 건다.
        Controller.Status.BeginDodgeInvincible(Controller.DashInvincibleTime);
        Controller.HorizontalVelocity = _direction * GetSpeed();
        Controller.Animation.Play(IsDodge ? DODGE_ANIMATION : DASH_ANIMATION);
    }

    public override void Tick()
    {
        _timer += Controller.DeltaTime;

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
            Controller.HorizontalVelocity = _direction * GetSpeed();
            return;
        }

        // 끝날 때 이동 입력이 있으면 달리기로 이어가고, 없으면 멈춘다.
        Controller.HorizontalVelocity = Vector3.zero;
        Controller.EnterLocomotion(moveDirection != Vector3.zero);
    }

    // 시작 지연 동안은 0, 그 뒤에는 실제 이동 시간에 대한 진행 비율(0 → 1)로 시작 속도에서 0 까지 보간한다.
    // Lerp 는 t 를 0 ~ 1 로 자르므로 이동 시간이 지나면 0 이 된다.
    private float GetSpeed()
    {
        if(_timer < _startDelay || _moveTime <= 0f) return 0f;

        float t = (_timer - _startDelay) / _moveTime;

        return Mathf.Lerp(_startSpeed, 0f, t);
    }
}

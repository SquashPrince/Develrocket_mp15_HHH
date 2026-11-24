using UnityEngine;

// 대시와 회피(백스텝) 상태. 둘은 방향 · 거리 · 시간 · 시작 지연만 다르다.
// 시작 지연 동안 제자리에 있어도 무적 · 완벽 회피 판정은 들어온 순간부터 열려, 몸이 빠져나가기 전에 닿은 공격도 완벽 회피가 된다.
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

    public bool IsDodge { get; private set; }

    // 고르게 감속하면 움직인 거리는 시작 속도 × 시간 ÷ 2 이므로, 시작 속도를 거리 × 2 ÷ 실제 이동 시간으로 잡는다.
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
        Controller.Status.BeginDodgeInvincible(Controller.DashInvincibleTime);
        Controller.HorizontalVelocity = _direction * GetSpeed();
        Controller.Animation.Play(IsDodge ? DODGE_ANIMATION : DASH_ANIMATION);

        // 다른 연출 순간과 같이 조작 중인 캐릭터일 때만 알린다.
        if(Controller.Party.Controlled == Controller) BattleFeedback.Raise(IsDodge ? BattleFeedback.Moment.Dodge : BattleFeedback.Moment.Dash);
    }

    public override void Tick()
    {
        _timer += Controller.DeltaTime;

        Vector3 moveDirection = Controller.ReadMoveDirection();

        // 완벽 회피에 성공했으면 회피 반격, 아니면 대시 공격이 나간다.
        if(Controller.WasAttackPressed)
        {
            if(Controller.IsPerfectDodge) Controller.StartDodgeCounter();
            else Controller.StartDashAttack();
            return;
        }

        // 간격이 지속시간보다 길면 지속시간을 기준으로 한다.
        bool canChain = _timer >= Mathf.Min(Controller.DashChainInterval, _duration);

        if(canChain && Controller.WasDashPressed && Controller.TryStartDash(moveDirection)) return;

        if(_timer < _duration)
        {
            Controller.HorizontalVelocity = _direction * GetSpeed();
            return;
        }

        // 이동 입력이 있으면 달리기로 이어간다.
        Controller.HorizontalVelocity = Vector3.zero;
        Controller.EnterLocomotion(moveDirection != Vector3.zero);
    }

    private float GetSpeed()
    {
        if(_timer < _startDelay || _moveTime <= 0f) return 0f;

        float t = (_timer - _startDelay) / _moveTime;

        return Mathf.Lerp(_startSpeed, 0f, t);
    }
}

using UnityEngine;

// 대시와 회피(백스텝) 상태.
// 두 동작 모두 정해진 방향으로 정해진 시간 동안 같은 속도로 이동하고, 방향 · 거리 · 시간만 다르다.
public class PlayerDashState : PlayerState
{
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
        Controller.HorizontalVelocity = _direction * _speed;
    }

    public override void Tick()
    {
        _timer += Time.deltaTime;

        Vector3 moveDirection = Controller.ReadMoveDirection();

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

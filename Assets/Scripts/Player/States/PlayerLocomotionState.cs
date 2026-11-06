using UnityEngine;

// 멈춤 · 걷기 · 달리기 상태.
// 이동 입력을 일정 시간 유지하면 달리기로 전환하고, 이동 입력이 없으면 멈춘다.
public class PlayerLocomotionState : PlayerState
{
    private enum Gait
    {
        Idle,
        Walk,
        Run
    }

    private Gait _gait;
    private float _moveHoldTime;
    private bool _startRunning;

    public PlayerLocomotionState(PlayerController controller) : base(controller)
    {
    }

    // 대시 · 회피가 끝날 때 이동 입력이 있으면 달리기로 시작하도록 미리 지정한다.
    public void SetStartRunning(bool startRunning) => _startRunning = startRunning;

    public override void Enter()
    {
        _gait = _startRunning ? Gait.Run : Gait.Idle;
        _moveHoldTime = 0f;
        _startRunning = false;
    }

    public override void Tick()
    {
        Vector3 moveDirection = Controller.ReadMoveDirection();

        if(Controller.WasDashPressed && Controller.TryStartDash(moveDirection)) return;

        Controller.HorizontalVelocity = UpdateGait(moveDirection);
    }

    private Vector3 UpdateGait(Vector3 moveDirection)
    {
        if(moveDirection == Vector3.zero)
        {
            _gait = Gait.Idle;
            _moveHoldTime = 0f;
            return Vector3.zero;
        }

        _moveHoldTime += Time.deltaTime;

        if(_gait != Gait.Run && _moveHoldTime >= Controller.AutoRunDelay) _gait = Gait.Run;
        else if(_gait == Gait.Idle) _gait = Gait.Walk;

        Controller.TurnTowards(moveDirection);

        float speed = _gait == Gait.Run ? Controller.RunSpeed : Controller.WalkSpeed;

        // 목표 방향이 아닌 현재 바라보는 방향으로 이동해 곡선으로 꺾는다. 스틱을 덜 기울이면 느려진다.
        return Controller.transform.forward * (moveDirection.magnitude * speed);
    }
}

using UnityEngine;

// 멈춤 · 걷기 · 달리기 상태.
// 이동 입력을 일정 시간 유지하면 달리기로 전환하고, 이동 입력이 없으면 멈춘다.
public class PlayerLocomotionState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    // 대기는 멈출 때마다 처음부터 재생하기 위해 걷기 · 달리기(Move Blend Tree)와 상태를 나눈다.
    private static readonly int IDLE_ANIMATION = Animator.StringToHash("Idle");
    private static readonly int MOVE_ANIMATION = Animator.StringToHash("Move");

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

    public override bool CanUseUltimate => true;

    // 대시 · 회피가 끝날 때 이동 입력이 있으면 달리기로 시작하도록 미리 지정한다.
    public void SetStartRunning(bool startRunning) => _startRunning = startRunning;

    public override void Enter()
    {
        _gait = _startRunning ? Gait.Run : Gait.Idle;
        _moveHoldTime = 0f;
        _startRunning = false;

        Controller.Animation.Play(_gait == Gait.Idle ? IDLE_ANIMATION : MOVE_ANIMATION);
    }

    public override void Tick()
    {
        Vector3 moveDirection = Controller.ReadMoveDirection();

        if(Controller.WasDashPressed && Controller.TryStartDash(moveDirection)) return;

        if(Controller.WasSkillPressed)
        {
            Controller.StartSkill();
            return;
        }

        // 공격 키를 누르는 즉시 기본 공격을 시작하고, 누르고 있는 동안 차지 시간을 잰다.
        // 완벽 회피 슬로모션이 남아 있으면 대시가 끝난 뒤라도 회피 반격이 나간다.
        if(Controller.WasAttackPressed)
        {
            if(Controller.IsPerfectDodge)
            {
                Controller.StartDodgeCounter();
                return;
            }

            Controller.BeginCharge();
            Controller.StartBasicAttack();
            return;
        }

        Controller.HorizontalVelocity = UpdateGait(moveDirection);
        Controller.Animation.SetMoveSpeed(Controller.HorizontalVelocity.magnitude);
    }

    private Vector3 UpdateGait(Vector3 moveDirection)
    {
        if(moveDirection == Vector3.zero)
        {
            // 움직이다 멈추는 순간 대기 모션을 처음부터 재생한다.
            if(_gait != Gait.Idle) Controller.Animation.Play(IDLE_ANIMATION);

            _gait = Gait.Idle;
            _moveHoldTime = 0f;
            return Vector3.zero;
        }

        if(_gait == Gait.Idle)
        {
            _gait = Gait.Walk;
            Controller.Animation.Play(MOVE_ANIMATION);
        }

        _moveHoldTime += Controller.DeltaTime;

        if(_gait != Gait.Run && _moveHoldTime >= Controller.AutoRunDelay) _gait = Gait.Run;

        Controller.TurnTowards(moveDirection);

        float speed = _gait == Gait.Run ? Controller.RunSpeed : Controller.WalkSpeed;

        // 목표 방향이 아닌 현재 바라보는 방향으로 이동해 곡선으로 꺾는다. 스틱을 덜 기울이면 느려진다.
        return Controller.transform.forward * (moveDirection.magnitude * speed);
    }
}

using UnityEngine;

// 교체로 필드에 들어오는 상태. 이전 캐릭터가 있던 자리에서 입장 모션을 재생하며 앞으로 달려 나오다 멈춘다.
// 무적이 아니어서 입장 중에도 적 공격에 맞으면 피격 상태로 넘어간다. 필살기는 입장 모션을 끊고 바로 쓸 수 있다.
// 전진 : 입장 시간 동안 입장 전진 거리만큼 이전 캐릭터가 보던 방향으로 나아간다. 처음이 가장 빠르고 끝에서 0 이 되도록 일정하게 느려진다.
//   속도를 시작 속도(거리 × 2 ÷ 시간)에서 0 까지 고르게 줄이면, 지나간 거리가 정확히 전진 거리가 된다.
// 입력 : 이동 키를 누르면 입장을 바로 끝내고 달리기로 이동을 시작한다. 달려 나오던 흐름을 그대로 잇기 위해 걷기가 아닌 달리기로 시작한다.
//        공격 · 스킬은 입장 행동 가능 시간부터 쓸 수 있다. 그 전에 누르면 기억해 두었다가 그 시간이 되는 순간 기본 공격 1단 · 스킬을 시작한다.
//        입장이 다 끝날 때까지 기다리지 않아 조금 빠르게 나간다. 강공격 차지는 공격이 시작될 때부터 잰다.
//        둘 다 눌렀으면 먼저 누른 쪽을 쓴다. 이동 키로 입장을 먼저 끝냈을 때도 기억한 공격 · 스킬이 있으면 그것을 쓴다.
// 입장 시간이 지나면 이동 상태로 돌아간다. 입장 시간 · 전진 거리는 공통 설정에서 읽는다.
public class PlayerSwapInState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    private static readonly int SWAP_IN_ANIMATION = Animator.StringToHash("SwapIn");

    private enum BufferedInput
    {
        None,
        Attack,
        Skill
    }

    private BufferedInput _bufferedInput;
    private float _timer;

    public PlayerSwapInState(PlayerController controller) : base(controller)
    {
    }

    public override bool CanUseUltimate => true;

    public override void Enter()
    {
        _timer = 0f;
        _bufferedInput = BufferedInput.None;
        Controller.HorizontalVelocity = Vector3.zero;
        Controller.Animation.PlayOnBodyPart(SWAP_IN_ANIMATION, Controller.Character.SwapInBodyPart);
    }

    public override void Tick()
    {
        BufferInput();

        float swapInTime = Controller.Common.SwapInTime;
        bool hasMoveInput = Controller.ReadMoveDirection() != Vector3.zero;

        _timer += Controller.DeltaTime;

        bool canAct = _bufferedInput != BufferedInput.None && _timer >= Controller.Common.SwapInActionDelay;

        if(_timer >= swapInTime || hasMoveInput || canAct)
        {
            Finish(hasMoveInput);
            return;
        }

        float startSpeed = Controller.Common.SwapInAdvanceDistance * 2f / swapInTime;
        float speed = startSpeed * (1f - _timer / swapInTime);

        Controller.HorizontalVelocity = Controller.transform.forward * speed;
    }

    public override void Exit()
    {
        Controller.HorizontalVelocity = Vector3.zero;
        Controller.Animation.EndBodyPart();
    }

    // 아직 기억한 입력이 없을 때만 새로 기억한다. 먼저 누른 입력을 쓰기 위해서다.
    private void BufferInput()
    {
        if(_bufferedInput != BufferedInput.None) return;

        if(Controller.WasAttackPressed) _bufferedInput = BufferedInput.Attack;
        else if(Controller.WasSkillPressed) _bufferedInput = BufferedInput.Skill;
    }

    // 기억한 공격 · 스킬이 있으면 그 행동을, 없으면 이동 상태로 넘어간다.
    // startRunning : 이동 입력으로 입장을 끝냈으면 달리기로 시작한다.
    private void Finish(bool startRunning)
    {
        switch(_bufferedInput)
        {
            case BufferedInput.Attack:
                Controller.BeginCharge();
                Controller.StartBasicAttack();
                break;
            case BufferedInput.Skill:
                Controller.StartSkill();
                break;
            default:
                Controller.EnterLocomotion(startRunning);
                break;
        }
    }
}

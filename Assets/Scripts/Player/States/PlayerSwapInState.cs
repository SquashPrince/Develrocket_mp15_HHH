using UnityEngine;

// 교체로 필드에 들어오는 상태. 이전 캐릭터 자리에서 앞으로 달려 나오다 멈춘다. 무적이 아니다.
// 이동 키를 누르면 달려 나오던 흐름을 잇도록 달리기로 바로 끝낸다.
// 공격 · 스킬은 기억해 두었다가 입장 행동 가능 시간이 되는 순간 시작해, 입장이 다 끝날 때까지 기다리지 않는다.
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

        // 시작 속도(거리 × 2 ÷ 시간)에서 0 까지 고르게 줄이면 지나간 거리가 정확히 전진 거리가 된다.
        float startSpeed = Controller.Common.SwapInAdvanceDistance * 2f / swapInTime;
        float speed = startSpeed * (1f - _timer / swapInTime);

        Controller.HorizontalVelocity = Controller.transform.forward * speed;
    }

    public override void Exit()
    {
        Controller.HorizontalVelocity = Vector3.zero;
        Controller.Animation.EndBodyPart();
    }

    // 먼저 누른 입력을 쓴다.
    private void BufferInput()
    {
        if(_bufferedInput != BufferedInput.None) return;

        if(Controller.WasAttackPressed) _bufferedInput = BufferedInput.Attack;
        else if(Controller.WasSkillPressed) _bufferedInput = BufferedInput.Skill;
    }

    // 이동 키로 먼저 끝냈어도 기억한 공격 · 스킬이 있으면 그것을 쓴다.
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

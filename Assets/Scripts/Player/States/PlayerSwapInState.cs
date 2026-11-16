using UnityEngine;

// 교체로 필드에 들어오는 상태. 이전 캐릭터가 있던 자리에서 입장 모션을 재생하며 앞으로 달려 나오다 멈춘다.
// 입장 모션 동안은 이동 · 공격 입력을 받지 않는다. 필살기만 입장 모션을 끊고 바로 쓸 수 있다.
// 무적이 아니어서 입장 중에도 적 공격에 맞으면 피격 상태로 넘어간다.
// 전진 : 입장 시간 동안 입장 전진 거리만큼 이전 캐릭터가 보던 방향으로 나아간다. 처음이 가장 빠르고 끝에서 0 이 되도록 일정하게 느려진다.
//   속도를 시작 속도(거리 × 2 ÷ 시간)에서 0 까지 고르게 줄이면, 지나간 거리가 정확히 전진 거리가 된다.
// 입장 시간이 지나면 이동 상태로 돌아간다. 입장 시간 · 전진 거리는 공통 설정에서 읽는다.
public class PlayerSwapInState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    private static readonly int SWAP_IN_ANIMATION = Animator.StringToHash("SwapIn");

    private float _timer;

    public PlayerSwapInState(PlayerController controller) : base(controller)
    {
    }

    public override bool CanUseUltimate => true;

    public override void Enter()
    {
        _timer = 0f;
        Controller.HorizontalVelocity = Vector3.zero;
        Controller.Animation.PlayOnBodyPart(SWAP_IN_ANIMATION, Controller.Character.SwapInBodyPart);
    }

    public override void Tick()
    {
        float swapInTime = Controller.Common.SwapInTime;

        _timer += Controller.DeltaTime;

        if(_timer >= swapInTime)
        {
            Controller.EnterLocomotion(false);
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
}

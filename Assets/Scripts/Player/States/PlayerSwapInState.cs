using UnityEngine;

// 교체로 필드에 들어오는 상태. 이전 캐릭터가 있던 자리에서 입장 모션을 재생한다.
// 입장 모션 동안은 이동 · 공격 입력을 받지 않는다. 필살기만 입장 모션을 끊고 바로 쓸 수 있다.
// 무적이 아니어서 입장 중에도 적 공격에 맞으면 피격 상태로 넘어간다.
// 입장 모션 클립의 후딜 끝(RecoveryEnd) 신호에서 이동 상태로 돌아간다. 캐릭터마다 입장 모션 길이가 달라 클립에 맞춘다.
public class PlayerSwapInState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    private static readonly int SWAP_IN_ANIMATION = Animator.StringToHash("SwapIn");

    public PlayerSwapInState(PlayerController controller) : base(controller)
    {
    }

    public override bool CanUseUltimate => true;

    public override void Enter()
    {
        Controller.HorizontalVelocity = Vector3.zero;
        Controller.Animation.Play(SWAP_IN_ANIMATION);
    }

    public override void Tick()
    {
    }

    public override void OnAnimationEvent(PlayerAnimationEventType type, int stateHash)
    {
        if(type != PlayerAnimationEventType.RecoveryEnd || stateHash != SWAP_IN_ANIMATION) return;

        Controller.EnterLocomotion(false);
    }
}

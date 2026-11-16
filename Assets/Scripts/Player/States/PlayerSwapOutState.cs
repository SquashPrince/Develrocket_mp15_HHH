using UnityEngine;

// 교체로 필드에서 나가는 상태. 퇴장 모션을 재생하고, 퇴장 시간이 지나면 캐릭터를 꺼 대기 상태로 만든다.
// 퇴장 모션 동안은 무적이라 퇴장이 끝까지 진행된다.
// 하던 동작이 있으면 그 동작을 끝까지 마친 뒤 이 상태로 들어온다. (컨트롤러의 퇴장 예약)
// 퇴장 시간은 모든 캐릭터가 같이 쓰는 공통 설정에서 읽는다.
public class PlayerSwapOutState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    private static readonly int SWAP_OUT_ANIMATION = Animator.StringToHash("SwapOut");

    private float _timer;

    public PlayerSwapOutState(PlayerController controller) : base(controller)
    {
    }

    public override void Enter()
    {
        _timer = 0f;
        Controller.HorizontalVelocity = Vector3.zero;
        Controller.Status.BeginInvincible(Controller.Common.SwapOutTime);
        Controller.Animation.Play(SWAP_OUT_ANIMATION);
    }

    public override void Tick()
    {
        _timer += Controller.DeltaTime;

        if(_timer >= Controller.Common.SwapOutTime) Controller.Standby();
    }
}

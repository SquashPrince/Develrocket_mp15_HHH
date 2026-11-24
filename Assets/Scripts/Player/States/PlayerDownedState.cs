using UnityEngine;

// 쓰러짐 상태. 모션이 끝나면 파티에 알려 다음 캐릭터로 교체하게 한다. 다시 일어나지 않는다.
// 피격은 PlayerStatus 가 쓰러진 캐릭터에게 오는 공격을 무시해 막는다.
public class PlayerDownedState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    private static readonly int DEATH_ANIMATION = Animator.StringToHash("Death");

    // 마지막 캐릭터는 꺼지지 않고 쓰러진 채 남으므로 매 프레임 알리지 않게 막는다.
    private bool _hasNotified;

    public PlayerDownedState(PlayerController controller) : base(controller)
    {
    }

    public override bool CanSwap => false;

    public override void Enter()
    {
        _hasNotified = false;
        Controller.HorizontalVelocity = Vector3.zero;
        Controller.Animation.EndBodyPart();
        Controller.Animation.SetLegRun(false);
        Controller.Animation.Play(DEATH_ANIMATION);
    }

    public override void Tick()
    {
        if(_hasNotified || !Controller.Animation.IsStateFinished(DEATH_ANIMATION)) return;

        _hasNotified = true;
        Controller.Party.OnMemberDowned(Controller);
    }
}

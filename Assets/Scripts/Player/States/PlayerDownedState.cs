using UnityEngine;

// 쓰러짐 상태. 체력이 0 이 되면 어떤 상태에서든 이 상태로 넘어온다.
// 쓰러짐 모션(Death)을 재생하고, 모션이 끝나면 파티에 알린다. 파티가 이 캐릭터를 내보내고 다음 살아 있는 캐릭터로 교체한다.
// 모션 동안 입력 · 교체를 받지 않는다. 피격은 PlayerStatus 가 쓰러진 캐릭터에게 오는 공격을 무시해 막는다.
// 다시 일어나지 않는다. 재시작은 씬을 다시 불러와 처음 상태로 돌린다.
public class PlayerDownedState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    private static readonly int DEATH_ANIMATION = Animator.StringToHash("Death");

    public PlayerDownedState(PlayerController controller) : base(controller)
    {
    }

    public override bool CanSwap => false;

    public override void Enter()
    {
        Controller.HorizontalVelocity = Vector3.zero;
        Controller.Animation.EndBodyPart();
        Controller.Animation.SetLegRun(false);
        Controller.Animation.Play(DEATH_ANIMATION);
    }

    // 파티가 알림을 받으면 이 캐릭터를 끄므로, 알림은 한 번만 간다.
    public override void Tick()
    {
        if(!Controller.Animation.IsStateFinished(DEATH_ANIMATION)) return;

        Controller.Party.OnMemberDowned(Controller);
    }
}

using UnityEngine;

// 쓰러짐 상태. 체력이 0 이 되면 어떤 상태에서든 이 상태로 넘어온다.
// 쓰러짐 모션(Death)을 재생하고, 모션이 끝나면 파티에 알린다. 파티가 이 캐릭터를 내보내고 다음 살아 있는 캐릭터로 교체한다.
// 마지막 캐릭터면 파티가 내보내지 않아, 쓰러진 모습 그대로 남는다.
// 모션 동안 입력 · 교체를 받지 않는다. 피격은 PlayerStatus 가 쓰러진 캐릭터에게 오는 공격을 무시해 막는다.
// 다시 일어나지 않는다. 재시작은 씬을 다시 불러와 처음 상태로 돌린다.
public class PlayerDownedState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    private static readonly int DEATH_ANIMATION = Animator.StringToHash("Death");

    // 파티에 모션 끝을 알렸는지. 마지막 캐릭터는 꺼지지 않고 쓰러진 채 남으므로, 매 프레임 알리지 않게 막는다.
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

    // 모션이 끝나면 파티에 한 번만 알린다. 마지막 캐릭터는 꺼지지 않고 이 상태로 쓰러진 채 남는다.
    public override void Tick()
    {
        if(_hasNotified || !Controller.Animation.IsStateFinished(DEATH_ANIMATION)) return;

        _hasNotified = true;
        Controller.Party.OnMemberDowned(Controller);
    }
}

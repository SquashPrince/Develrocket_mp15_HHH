// 플레이어 상태의 공통 틀.
// 상태마다 들어올 때(Enter) · 매 프레임(Tick) · 나갈 때(Exit) 처리를 나눈다.
// Enter · Exit · 애니메이션 이벤트 처리는 필요한 상태만 재정의하도록 기본 동작을 비워 둔다.
public abstract class PlayerState
{
    protected PlayerState(PlayerController controller)
    {
        Controller = controller;
    }

    protected PlayerController Controller { get; }

    // 이 상태에서 필살기를 쓸 수 있는지. 기본은 쓸 수 없고, 허용할 상태만 재정의한다.
    // 새 상태를 추가할 때 따로 적지 않아도 필살기가 막히도록 기본값을 불가로 둔다.
    public virtual bool CanUseUltimate => false;

    // 이 상태에서 교체 키로 다른 캐릭터와 교체할 수 있는지. 기본은 가능하다.
    // 액션을 쓰는 상태(공격 · 패링 · 필살기 · 콤보 스킬)는 지금 동작의 끼어들기 허용을 따른다.
    public virtual bool CanSwap => true;

    // 이 상태가 지금 쓰고 있는 액션. 액션을 쓰지 않는 상태(이동 · 피격 등)는 null 이다. 카메라 구도가 읽는다.
    public virtual ActionData CurrentAction => null;

    public virtual void Enter()
    {
    }

    public abstract void Tick();

    public virtual void Exit()
    {
    }

    // stateHash : 이벤트가 발생한 Animator 상태의 이름 해시
    public virtual void OnAnimationEvent(PlayerAnimationEventType type, int stateHash)
    {
    }
}

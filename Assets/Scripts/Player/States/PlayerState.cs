// 플레이어 상태의 공통 틀. 필요한 상태만 재정의하도록 Enter · Exit · 애니메이션 이벤트 처리는 비워 둔다.
public abstract class PlayerState
{
    protected PlayerState(PlayerController controller)
    {
        Controller = controller;
    }

    protected PlayerController Controller { get; }

    // 새 상태를 추가할 때 따로 적지 않아도 필살기가 막히도록 기본값을 불가로 둔다.
    public virtual bool CanUseUltimate => false;

    // 액션을 쓰는 상태는 지금 동작의 끼어들기 허용을 따른다.
    public virtual bool CanSwap => true;

    // 액션을 쓰지 않는 상태는 null 이다.
    public virtual ActionData CurrentAction => null;

    public virtual ActionStep CurrentStep => null;

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

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

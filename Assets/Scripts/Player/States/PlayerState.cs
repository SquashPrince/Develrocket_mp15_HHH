// 플레이어 상태의 공통 틀.
// 상태마다 들어올 때(Enter) · 매 프레임(Tick) · 나갈 때(Exit) 처리를 나눈다.
// Enter · Exit 는 필요한 상태만 재정의하도록 기본 동작을 비워 둔다.
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
}

// 현재 상태를 하나 들고, 상태 전환 시 이전 상태의 Exit 와 다음 상태의 Enter 를 순서대로 호출한다.
public class PlayerStateMachine
{
    public PlayerState CurrentState { get; private set; }

    public void ChangeState(PlayerState nextState)
    {
        CurrentState?.Exit();
        CurrentState = nextState;
        CurrentState.Enter();
    }

    public void Tick() => CurrentState.Tick();
}

using System;
using Unity.Behavior;
using Unity.Properties;
using Action = Unity.Behavior.Action;

// [행동 그래프 행동] 경직 · 그로기 중이면 끝날 때까지 기다리고, 행동할 수 있으면 성공한다.
// 처치된 적은 계속 기다리므로 그 뒤의 행동을 하지 않는다.
// 그래프의 반복 맨 앞에 두어, 경직 · 그로기로 끊긴 뒤 회복하면 처음부터 다시 판단하게 한다.
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Wait Until Recovered",
    description: "경직 · 그로기가 끝날 때까지 기다린다. 처치된 적은 계속 기다린다.",
    story: "Wait until Self recovers",
    category: "Enemy",
    id: "c284431997ec441e937a4ad9702b4dfb")]
public partial class WaitUntilRecoveredAction : Action
{
    private EnemyState _state;

    protected override Status OnStart()
    {
        _state = GameObject.GetComponent<EnemyState>();

        return OnUpdate();
    }

    protected override Status OnUpdate() => !_state.IsAlive || _state.IsRestricted ? Status.Running : Status.Success;
}

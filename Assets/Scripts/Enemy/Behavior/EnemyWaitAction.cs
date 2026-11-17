using System;
using Unity.Behavior;
using Unity.Properties;
using Action = Unity.Behavior.Action;

// [행동 그래프 행동] 적 행동 설정의 공격 간격만큼 기다린 뒤 성공한다.
// 기본 Wait 노드는 Unity 기본 시간을 써서 슬로모션 · 필살기 연출에 느려지지 않으므로, 적 전용 시간으로 기다리는 노드를 따로 둔다.
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Wait",
    description: "공격 간격만큼 적 전용 시간으로 기다린다.",
    story: "Wait for attack interval",
    category: "Enemy",
    id: "04b6f5ac73214240ba61e5e18c708954")]
public partial class EnemyWaitAction : Action
{
    private float _timer;

    protected override Status OnStart()
    {
        _timer = GameObject.GetComponent<EnemyController>().Data.AttackInterval;

        return OnUpdate();
    }

    protected override Status OnUpdate()
    {
        _timer -= EnemyTime.DeltaTime;

        return _timer <= 0f ? Status.Success : Status.Running;
    }
}

using UnityEngine;

// 애니메이션 이벤트는 Animator 가 붙은 외형(Visual)의 메서드를 이름으로 부르므로, 외형에 붙어 루트의 EnemyAttacker 로 넘긴다.
// 메서드 이름은 클립의 이벤트 Function 이름과 같아야 한다.
public class EnemyAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private EnemyAttacker _attacker;

    // 이전 클립과 섞이는 동안 이전 클립의 이벤트도 오므로, 어느 Animator 상태에서 왔는지 함께 넘긴다.
    private void Telegraph(AnimationEvent animationEvent) => Relay(EnemyAnimationEventType.Telegraph, animationEvent);
    private void HitStart(AnimationEvent animationEvent) => Relay(EnemyAnimationEventType.HitStart, animationEvent);
    private void HitEnd(AnimationEvent animationEvent) => Relay(EnemyAnimationEventType.HitEnd, animationEvent);
    private void ResistEnd(AnimationEvent animationEvent) => Relay(EnemyAnimationEventType.ResistEnd, animationEvent);
    private void LeapStart(AnimationEvent animationEvent) => Relay(EnemyAnimationEventType.LeapStart, animationEvent);
    private void LeapLock(AnimationEvent animationEvent) => Relay(EnemyAnimationEventType.LeapLock, animationEvent);
    private void LeapEnd(AnimationEvent animationEvent) => Relay(EnemyAnimationEventType.LeapEnd, animationEvent);

    // 도약은 같은 클립의 다른 이벤트 시점을 읽어야 하므로 이벤트 정보를 그대로 넘긴다.
    private void Relay(EnemyAnimationEventType type, AnimationEvent animationEvent)
    {
        _attacker.HandleAnimationEvent(type, animationEvent);
    }
}

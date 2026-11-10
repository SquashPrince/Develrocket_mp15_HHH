using UnityEngine;

// 애니메이션 이벤트는 Animator 가 붙은 오브젝트(캐릭터 모델)의 메서드를 이름으로 호출한다.
// 그래서 이 컴포넌트를 캐릭터 모델에 붙이고, 받은 이벤트를 루트의 PlayerController 로 넘긴다.
// 메서드 이름은 클립의 이벤트 Function 이름과 같아야 한다.
public class PlayerAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private PlayerController _playerController;

    // 상태가 바뀌며 이전 클립과 섞이는 동안에는 이전 클립의 이벤트도 발생하므로,
    // 어느 Animator 상태에서 온 이벤트인지 함께 넘겨 현재 상태의 이벤트만 처리하게 한다.
    private void ComboInputOpen(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.ComboInputOpen, animationEvent);
    private void Hit(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.Hit, animationEvent);
    private void Impact(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.Impact, animationEvent);
    private void AttackMotionEnd(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.AttackMotionEnd, animationEvent);
    private void RecoveryEnd(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.RecoveryEnd, animationEvent);

    private void Relay(PlayerAnimationEventType type, AnimationEvent animationEvent)
    {
        _playerController.HandleAnimationEvent(type, animationEvent.animatorStateInfo.shortNameHash);
    }
}

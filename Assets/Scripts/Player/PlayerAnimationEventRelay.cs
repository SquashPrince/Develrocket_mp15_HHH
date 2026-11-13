using UnityEngine;

// 애니메이션 이벤트는 Animator 가 붙은 오브젝트(캐릭터 모델)의 메서드를 이름으로 호출한다.
// 그래서 이 컴포넌트를 캐릭터 모델에 붙이고, 받은 이벤트를 루트의 PlayerController 로 넘긴다.
// 메서드 이름은 클립의 이벤트 Function 이름과 같아야 한다.
// 판정 시작 이벤트는 같은 클립에서 뒤따르는 판정 끝 · 강타 끝 이벤트를 찾아 구간 길이를 판정 기능에 미리 알린다.
// 횟수 방식 구간 판정이 시작 순간에 타격 간격을 정하려면 구간 길이가 필요하기 때문이다.
public class PlayerAnimationEventRelay : MonoBehaviour
{
    private const string HIT_END_FUNCTION = nameof(HitEnd);
    private const string IMPACT_END_FUNCTION = nameof(ImpactEnd);

    [SerializeField] private PlayerController _playerController;

    // 상태가 바뀌며 이전 클립과 섞이는 동안에는 이전 클립의 이벤트도 발생하므로,
    // 어느 Animator 상태에서 온 이벤트인지 함께 넘겨 현재 상태의 이벤트만 처리하게 한다.
    private void ComboInputOpen(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.ComboInputOpen, animationEvent);
    private void HitStart(AnimationEvent animationEvent)
    {
        PrepareHitWindow(animationEvent);
        Relay(PlayerAnimationEventType.HitStart, animationEvent);
    }
    private void HitEnd(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.HitEnd, animationEvent);
    private void ImpactEnd(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.ImpactEnd, animationEvent);
    private void AttackMotionEnd(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.AttackMotionEnd, animationEvent);
    private void RecoveryEnd(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.RecoveryEnd, animationEvent);

    private void Relay(PlayerAnimationEventType type, AnimationEvent animationEvent)
    {
        _playerController.HandleAnimationEvent(type, animationEvent.animatorStateInfo.shortNameHash);
    }

    // 이 판정 시작 뒤 같은 클립에서 처음 나오는 판정 끝 · 강타 끝을 찾아, 구간 길이(초, 클립 시간)와 강타로 끝나는지를 알린다.
    // 찾지 못하면 길이를 모르는 것(-1)으로 알린다. Animator 상태의 재생 속도 배수는 1 이라고 본다.
    private void PrepareHitWindow(AnimationEvent animationEvent)
    {
        AnimationClip clip = animationEvent.animatorClipInfo.clip;
        float length = -1f;
        bool endsWithImpact = false;

        if(clip != null)
        {
            float nearestEnd = float.MaxValue;

            foreach(AnimationEvent clipEvent in clip.events)
            {
                if(clipEvent.time <= animationEvent.time || clipEvent.time >= nearestEnd) continue;
                if(clipEvent.functionName != HIT_END_FUNCTION && clipEvent.functionName != IMPACT_END_FUNCTION) continue;

                nearestEnd = clipEvent.time;
                endsWithImpact = clipEvent.functionName == IMPACT_END_FUNCTION;
            }

            if(nearestEnd < float.MaxValue) length = nearestEnd - animationEvent.time;
        }

        _playerController.AttackJudge.PrepareWindow(length, endsWithImpact);
    }
}

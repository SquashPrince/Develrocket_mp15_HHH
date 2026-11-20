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

    // 걷기 · 달리기 클립의 발이 땅에 닿는 프레임에 찍는다. 상태 처리와 상관없는 소리 순간이라 상태로 넘기지 않고 바로 알린다.
    // 조작 중인 캐릭터의 발자국만 알린다. 퇴장 중인 이전 캐릭터가 달려 나갈 때는 소리를 내지 않는다.
    private void Footstep()
    {
        if(_playerController.Party.Controlled == _playerController) BattleFeedback.Raise(BattleFeedback.Moment.Footstep);
    }

    // 동작 이펙트를 띄우는 순간에 찍고, Int 칸에 지금 동작의 이펙트 목록 번호를 넣는다.
    // 상태의 진행과 상관없는 연출이라 상태로 넘기지 않고, 컨트롤러가 지금 동작을 확인해 바로 띄운다.
    private void MotionEffect(AnimationEvent animationEvent)
    {
        _playerController.PlayMotionEffect(animationEvent.intParameter, animationEvent.animatorStateInfo.shortNameHash);
    }

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

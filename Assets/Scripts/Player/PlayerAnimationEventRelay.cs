using UnityEngine;

// 애니메이션 이벤트는 Animator 가 붙은 캐릭터 모델의 메서드를 이름으로 부르므로, 모델에 붙어 루트의 PlayerController 로 넘긴다.
// 메서드 이름은 클립의 이벤트 Function 이름과 같아야 한다.
public class PlayerAnimationEventRelay : MonoBehaviour
{
    private const string HIT_END_FUNCTION = nameof(HitEnd);
    private const string IMPACT_END_FUNCTION = nameof(ImpactEnd);

    [SerializeField] private PlayerController _playerController;

    // 이전 클립과 섞이는 동안 이전 클립의 이벤트도 오므로, 어느 Animator 상태에서 왔는지 함께 넘긴다.
    private void ComboInputOpen(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.ComboInputOpen, animationEvent);
    // Int 값은 이번 휘두르기의 적중 이펙트 번호다.
    private void HitStart(AnimationEvent animationEvent)
    {
        PrepareHitWindow(animationEvent);
        _playerController.AttackJudge.PrepareHitEffect(animationEvent.intParameter);
        Relay(PlayerAnimationEventType.HitStart, animationEvent);
    }
    private void HitEnd(AnimationEvent animationEvent)
    {
        _playerController.StopLoopingMotionEffects(animationEvent.animatorStateInfo.shortNameHash);
        Relay(PlayerAnimationEventType.HitEnd, animationEvent);
    }
    private void ImpactEnd(AnimationEvent animationEvent)
    {
        _playerController.AttackJudge.PrepareHitEffect(animationEvent.intParameter);
        _playerController.StopLoopingMotionEffects(animationEvent.animatorStateInfo.shortNameHash);
        Relay(PlayerAnimationEventType.ImpactEnd, animationEvent);
    }
    private void AttackMotionEnd(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.AttackMotionEnd, animationEvent);
    private void RecoveryEnd(AnimationEvent animationEvent) => Relay(PlayerAnimationEventType.RecoveryEnd, animationEvent);

    // 상태와 상관없는 소리 순간이라 바로 알린다. 퇴장 중인 이전 캐릭터의 발자국은 내지 않는다.
    private void Footstep()
    {
        if(_playerController.Party.Controlled == _playerController) BattleFeedback.Raise(BattleFeedback.Moment.Footstep);
    }

    // 상태의 진행과 상관없는 연출이라 상태로 넘기지 않는다.
    private void MotionEffect(AnimationEvent animationEvent)
    {
        _playerController.PlayMotionEffect(animationEvent.intParameter, animationEvent.animatorStateInfo.shortNameHash);
    }

    private void Relay(PlayerAnimationEventType type, AnimationEvent animationEvent)
    {
        _playerController.HandleAnimationEvent(type, animationEvent.animatorStateInfo.shortNameHash);
    }

    // 횟수 방식 구간 판정은 시작 순간에 간격을 정해야 해서, 같은 클립의 다음 판정 끝 · 강타 끝을 찾아 구간 길이를 미리 알린다.
    // 찾지 못하면 -1. Animator 상태의 재생 속도 배수는 1 이라고 본다.
    private void PrepareHitWindow(AnimationEvent animationEvent)
    {
        float endTime = AnimationClipEvents.FindNextTime(animationEvent.animatorClipInfo.clip, animationEvent.time, HIT_END_FUNCTION, IMPACT_END_FUNCTION, out string endName);
        float length = endTime >= 0f ? endTime - animationEvent.time : -1f;

        _playerController.AttackJudge.PrepareWindow(length, endName == IMPACT_END_FUNCTION);
    }
}

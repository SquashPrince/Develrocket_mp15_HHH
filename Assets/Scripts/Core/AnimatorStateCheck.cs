using UnityEngine;

// Animator 상태 확인. 플레이어 · 적 애니메이션 기능이 "이 모션이 끝까지 재생됐는지" 를 같은 규칙으로 본다.
// 같은 확인을 두 곳에 따로 두지 않기 위해서다.
public static class AnimatorStateCheck
{
    // layer 에서 지금 재생 중인 상태가 stateHash 이고 끝까지 재생되었으면 true 다. 다른 상태와 섞이는 중이면 false 다.
    public static bool IsStateFinished(this Animator animator, int stateHash, int layer = 0)
    {
        if(animator.IsInTransition(layer)) return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);

        return stateInfo.shortNameHash == stateHash && stateInfo.normalizedTime >= 1f;
    }
}

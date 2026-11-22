using UnityEngine;

// 잠깐 커졌다가 원래 크기로 돌아오는 크기 강조. 버튼 눌림 · 필살기 단계 상승처럼 UI 를 짧게 강조할 때 함께 쓴다.
// 남은 시간의 비율만큼 강조 크기에서 원래 크기 쪽으로 돌아온다. 실제 시간으로 줄어 히트스톱 · 슬로모션과 상관없이 같은 속도다.
// 남은 시간 · 크기 값은 쓰는 쪽이 들고 있다. 각자의 Inspector 칸을 그대로 쓰기 위해서다.
public static class ScalePop
{
    // timer : 남은 강조 시간(초). 강조를 시작할 때 duration 으로 채운다. 0 이면 아무것도 하지 않는다.
    public static void Tick(ref float timer, float duration, float popScale, Transform target, Vector3 baseScale)
    {
        if(timer <= 0f) return;

        timer = Mathf.Max(0f, timer - Time.unscaledDeltaTime);
        target.localScale = baseScale * Mathf.Lerp(1f, popScale, timer / duration);
    }
}

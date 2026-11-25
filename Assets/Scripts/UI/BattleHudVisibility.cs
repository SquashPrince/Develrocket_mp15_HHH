using System;
using UnityEngine;

// 전투 HUD 를 지금 보여줄지. 콤보 스킬 캐릭터 선택 중에는 숨긴다.
// 적 바는 실행 중에 생겨 Inspector 로 연결할 수 없어, 바꾸는 쪽이 받는 쪽을 몰라도 되는 공용 알림으로 둔다.
public static class BattleHudVisibility
{
    // 구독하는 쪽은 꺼질 때 반드시 해제한다.
    public static event Action Changed;

    public static bool IsVisible { get; private set; } = true;

    public static void SetVisible(bool isVisible)
    {
        if(IsVisible == isVisible) return;

        IsVisible = isVisible;
        Changed?.Invoke();
    }

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 값 · 구독이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        IsVisible = true;
        Changed = null;
    }
}

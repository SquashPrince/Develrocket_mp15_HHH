using UnityEngine;

// 지금 열려 있는 패링 가능 시간의 수. 적은 패링 공격의 예고를 시작할 때 열고, 판정 · 경직 때 닫는다.
// 플레이어는 패링 키를 누를 때 하나라도 열려 있으면 패링을 시작한다.
public static class ParryWindow
{
    private static int _openCount;

    public static bool IsOpen => _openCount > 0;

    public static void Open() => _openCount++;

    public static void Close() => _openCount = Mathf.Max(0, _openCount - 1);

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 값이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => _openCount = 0;
}

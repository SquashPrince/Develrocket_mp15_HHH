// 판정 구간(판정 시작 ~ 판정 끝)의 다단 타격 시점 계산. 플레이어 공격 판정과 적 공격이 함께 쓴다.
// 같은 계산을 두 곳에 따로 두면 한쪽만 고쳐 플레이어 · 적의 다단 타격 규칙이 어긋날 수 있기 때문이다.
// 구간 길이는 클립 이벤트로 찾은 값(초)이고, 모르면 음수다.
//   간격 방식 : 정한 간격마다 친다. 길이를 모르면 판정이 꺼질 때까지 계속 친다.
//   횟수 방식 : 시작과 끝을 포함해 구간을 (횟수 - 1) 칸으로 나눈 시점마다 친다. 길이를 모르면 시작 시점 한 번만 친다.
// 끝 순간을 다른 타격이 맡으면(플레이어 : 강타로 끝남, 적 : 간격 방식의 막타) 구간 끝 시점의 타격은 여기서 세지 않는다.
public static class HitWindowTiming
{
    // 시점 비교 오차(초). 끝 시점과 타격 시점이 같을 때 실수 오차로 빠지거나 두 번 치지 않게 한다.
    public const float TIME_EPSILON = 0.0001f;

    // index 번째 타격 시점(구간 시작부터 초). 횟수 방식에서 1 타이거나 길이를 모르면 시작 시점이다.
    public static float GetTickTime(bool isInterval, float interval, int hitCount, float windowLength, int index)
    {
        if(isInterval) return interval * index;

        int gaps = hitCount - 1;

        return gaps > 0 && windowLength > 0f ? windowLength / gaps * index : 0f;
    }

    // index 번째 타격이 이 구간에 있는지.
    // endsByOther : 구간 끝 순간의 타격을 다른 타격이 맡는지. 횟수 방식은 마지막 한 번을 빼고, 간격 방식은 끝 직전까지만 센다.
    public static bool HasTick(bool isInterval, float interval, int hitCount, float windowLength, bool endsByOther, int index)
    {
        if(!isInterval)
        {
            if(windowLength < 0f) return index == 0;

            return index < (endsByOther ? hitCount - 1 : hitCount);
        }

        if(windowLength < 0f) return true;

        float tickTime = GetTickTime(true, interval, hitCount, windowLength, index);

        return endsByOther ? tickTime < windowLength - TIME_EPSILON : tickTime <= windowLength + TIME_EPSILON;
    }
}

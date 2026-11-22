using UnityEngine;

// 기록 시간을 분:초:1/100초(예 : 02:54:32) 글자로 바꾼다. 결과 화면과 타이틀 최고 기록이 같은 형식을 쓴다.
// 1/100 초 단위로 버린 뒤 나눈다. 초를 반올림해 쓰면 59.999 초가 60.00 으로 보이기 때문이다.
public static class RecordTimeText
{
    private const int CENTISECONDS_PER_SECOND = 100;
    private const int CENTISECONDS_PER_MINUTE = 6000;
    private const int SECONDS_PER_MINUTE = 60;

    public static string Format(float seconds)
    {
        int centiseconds = Mathf.FloorToInt(seconds * CENTISECONDS_PER_SECOND);
        int minutes = centiseconds / CENTISECONDS_PER_MINUTE;
        int wholeSeconds = centiseconds / CENTISECONDS_PER_SECOND % SECONDS_PER_MINUTE;
        int remainder = centiseconds % CENTISECONDS_PER_SECOND;

        return $"{minutes:00}:{wholeSeconds:00}:{remainder:00}";
    }
}

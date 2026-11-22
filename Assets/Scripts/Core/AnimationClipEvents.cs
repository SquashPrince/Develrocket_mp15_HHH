using UnityEngine;

// 클립에 심어 둔 애니메이션 이벤트 찾기. 플레이어 이벤트 전달 · 적 공격 · 적 도약이 구간 길이 · 착지 시간을 미리 알 때 함께 쓴다.
// 같은 탐색을 세 곳에 따로 두지 않기 위해서다.
public static class AnimationClipEvents
{
    // clip 에서 afterTime 보다 뒤에 있는 functionName 이벤트 중 가장 이른 시간. 없으면 -1 이다.
    public static float FindNextTime(AnimationClip clip, float afterTime, string functionName) =>
        FindNextTime(clip, afterTime, functionName, functionName, out _);

    // clip 에서 afterTime 보다 뒤에 있는 firstName · secondName 이벤트 중 가장 이른 시간. 없으면 -1 이다.
    // foundName : 찾은 이벤트의 이름. 두 종류 중 어느 쪽이 먼저 오는지 알 때 쓴다. 없으면 null 이다.
    public static float FindNextTime(AnimationClip clip, float afterTime, string firstName, string secondName, out string foundName)
    {
        float foundTime = -1f;

        foundName = null;

        if(clip == null) return foundTime;

        foreach(AnimationEvent clipEvent in clip.events)
        {
            if(clipEvent.time <= afterTime) continue;
            if(clipEvent.functionName != firstName && clipEvent.functionName != secondName) continue;
            if(foundTime >= 0f && clipEvent.time >= foundTime) continue;

            foundTime = clipEvent.time;
            foundName = clipEvent.functionName;
        }

        return foundTime;
    }
}

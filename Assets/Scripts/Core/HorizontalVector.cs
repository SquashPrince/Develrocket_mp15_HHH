using UnityEngine;

// 수평(XZ 평면) 벡터 계산. 높이를 무시한 방향 · 거리가 필요한 곳(대상 쪽으로 돌기, 밀려나는 방향, 순간 이동, 생성 위치 간격 등)이 함께 쓴다.
// 같은 두 줄(위치 차이를 구하고 높이를 0 으로)을 여러 곳에 적지 않기 위해서다.
public static class HorizontalVector
{
    // 높이(y)를 0 으로 만든 벡터. 정규화하지 않는다.
    public static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }

    // from 에서 to 로 가는 수평 벡터. 정규화하지 않는다.
    public static Vector3 Between(Vector3 from, Vector3 to) => Flatten(to - from);
}

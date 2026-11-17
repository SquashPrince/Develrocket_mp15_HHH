using UnityEngine;

// 패링 가능 시간을 여는 적. 패링 공격을 하는 적이 구현한다.
// 플레이어가 패링 대상을 고르고, 패링 대상 앞으로 순간 이동하고, 맞았을 때 넉백 방향을 정할 때 쓴다.
// 패링에 성공하면 플레이어는 알림만 보내고, 경직할지 다음 동작으로 이어갈지는 적이 지금 동작 설정으로 정한다.
public interface IEnemyParryable
{
    // 패링 대상 앞으로 순간 이동하거나 넉백 방향을 정할 때 위치 · 정면을 쓴다.
    Transform transform { get; }

    bool IsParryWindowOpen { get; }

    // 패링 공격 예고 표시를 띄울 위치. 표시는 플레이어 쪽이 대기 파티원 수에 따라 색을 골라 띄운다.
    Transform TelegraphPoint { get; }

    // 플레이어가 이 적의 지금 동작을 패링했을 때 부른다.
    void OnParried();
}

using UnityEngine;

// 패링 가능 시간을 여는 적. 패링에 성공하면 플레이어는 알림만 보내고, 경직할지 이어갈지는 적이 정한다.
public interface IEnemyParryable
{
    // 패링 대상 앞으로 순간 이동하거나 넉백 방향을 정할 때 위치 · 정면을 쓴다.
    Transform transform { get; }

    bool IsParryWindowOpen { get; }

    // 표시 색은 플레이어 쪽이 정해 띄운다.
    Transform TelegraphPoint { get; }

    // 지금 동작의 판정 범위 안에 이 위치가 들어가는지. 공격 중이 아니면 false. 교체 패링이 맞을 수 있는 자리를 고를 때 쓴다.
    bool IsInAttackRange(Vector3 point);

    void OnParried();
}

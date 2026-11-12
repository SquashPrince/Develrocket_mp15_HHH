using UnityEngine;

// 적 행동(Behavior Graph)이 쓰는 조정값. 그래프 노드가 이 컴포넌트에서 값을 읽는다.
// 실행 중에 바뀌는 상태(추적 대상 등)는 Behavior Graph 의 Blackboard 에 두고, 사람이 조정하는 값만 여기에 둔다.
// 2주차에 적 데이터를 ScriptableObject 로 옮길 때 이 값들을 함께 옮긴다.
public class EnemyBehaviorSetting : MonoBehaviour
{
    [Header("추적")]
    [Tooltip("추적 · 공격 접근(Run)할 때의 이동 속도. 추적 이동 속도에 곱하는 배율이다 (1 = 같은 속도). 거리 유지 · 백스텝 속도에는 적용하지 않는다")]
    [SerializeField, Min(0f)] private float _runSpeedRate = 1f;

    [Header("거리 유지")]
    [Tooltip("대상과 유지할 거리. 추적은 이 거리까지 다가가면 끝나고, 거리 유지는 대상 둘레의 이 거리 위를 돈다")]
    [SerializeField, Min(0f)] private float _keepDistance = 4f;
    [Tooltip("거리 유지 중 이 값보다 더 벌어지면 추적 속도로 다가간다")]
    [SerializeField, Min(0f)] private float _keepDistanceTolerance = 1f;
    [Tooltip("거리 유지 중 대상 둘레를 도는 속도(초당 거리)")]
    [SerializeField, Min(0f)] private float _strafeSpeed = 1.5f;
    [Tooltip("거리 유지를 하는 최소 시간(초, 적 전용 시간)")]
    [SerializeField, Min(0f)] private float _keepDistanceTimeMin = 2f;
    [Tooltip("거리 유지를 하는 최대 시간(초, 적 전용 시간). 최소 ~ 최대 사이에서 매번 무작위로 정한다")]
    [SerializeField, Min(0f)] private float _keepDistanceTimeMax = 4f;
    [Tooltip("거리 유지를 시작할 때 좌 · 우로 도는 대신 백스텝할 확률")]
    [SerializeField, Range(0f, 1f)] private float _backStepChance = 0.3f;
    [Tooltip("거리 유지를 시작할 때 좌 · 우로 도는 대신 제자리에서 대상만 바라보며 기다릴 확률. 시간은 거리 유지 시간을 쓴다. 백스텝 확률과 합이 1 을 넘으면 남은 만큼만 적용된다")]
    [SerializeField, Range(0f, 1f)] private float _idleChance = 0f;
    [Tooltip("백스텝 중 뒤로 가는 속도. 추적 이동 속도에 곱하는 배율이다 (1 = 같은 속도). 물러나는 거리 = 속도 × 백스텝 모션 길이")]
    [SerializeField, Min(0f)] private float _backStepSpeedRate = 1f;

    [Header("공격")]
    [Tooltip("이 거리 안에 들어오면 공격을 시작한다. 밖이면 이 거리까지 다가간다")]
    [SerializeField, Min(0f)] private float _attackRange = 2f;
    [Tooltip("공격을 마친 뒤 다음 행동까지 기다리는 시간(초, 적 전용 시간)")]
    [SerializeField, Min(0f)] private float _attackInterval = 2f;

    public float RunSpeedRate => _runSpeedRate;
    public float KeepDistance => _keepDistance;
    public float KeepDistanceTolerance => _keepDistanceTolerance;
    public float StrafeSpeed => _strafeSpeed;
    public float BackStepChance => _backStepChance;
    public float IdleChance => _idleChance;
    public float BackStepSpeedRate => _backStepSpeedRate;
    public float AttackRange => _attackRange;
    public float AttackInterval => _attackInterval;

    // 이번 거리 유지 시간을 최소 ~ 최대 사이에서 무작위로 정한다. 최대가 최소보다 작으면 최소를 쓴다.
    public float PickKeepDistanceTime() => Random.Range(_keepDistanceTimeMin, Mathf.Max(_keepDistanceTimeMin, _keepDistanceTimeMax));
}

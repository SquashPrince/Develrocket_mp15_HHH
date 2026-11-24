using UnityEngine;

// 적 공격 하나의 값. 공격 전체에 같은 값은 여기에, 모션마다 다른 값은 동작(EnemyAttackStep) 목록에 둔다.
[CreateAssetMenu(fileName = "NewEnemyAttackData", menuName = "HHH/Enemy Attack Data")]
public class EnemyAttackData : ScriptableObject
{
    // Inspector 에 순서 번호로 저장되므로, 새 반응은 항상 맨 뒤에 추가한다.
    public enum HitReaction
    {
        Hit,
        HeavyHit,
        Knockback
    }

    [Header("선택")]
    [Tooltip("이 공격을 쓴 뒤 다시 쓸 수 있기까지의 시간(초, 적 전용 시간). 공격이 끝나거나 끊긴 시점부터 흐른다")]
    [SerializeField, Min(0f)] private float _cooldown = 3f;
    [Tooltip("기본 선택 가중치. 쓸 수 있는 공격 중 가중치에 비례해 무작위로 고른다")]
    [SerializeField, Min(0f)] private float _baseWeight = 1f;
    [Tooltip("다른 공격이 선택될 때마다 늘어나는 가중치. 오래 쓰지 않은 공격일수록 잘 뽑힌다. 이 공격이 선택되면 기본 가중치로 돌아간다")]
    [SerializeField, Min(0f)] private float _weightGrowth = 1f;
    [Tooltip("대상이 이 거리 이상 떨어져 있어야 쓸 수 있다(수평 거리). 0 이면 거리 제한이 없다")]
    [SerializeField, Min(0f)] private float _minUseDistance;
    [Tooltip("켜면 대상이 공격 사거리 밖에 있을 때 다가가지 않고 바로 쓸 수 있다 (Ranged Attack 노드). 끄면 사거리 안에서만 쓴다. 사거리 안에서는 켜져 있어도 쓸 수 있다")]
    [SerializeField] private bool _canUseOutOfRange;

    [Header("회전")]
    [Tooltip("공격 중 첫 판정 전까지 대상 쪽으로 도는 초당 각도")]
    [SerializeField, Min(0f)] private float _turnSpeed = 360f;

    [Header("도약 · 돌진 (LeapStart 이벤트가 있는 공격)")]
    [Tooltip("도약 중 외형이 떠오르는 최고 높이. 0 이면 땅을 따라 직선으로 돌진한다. 0 보다 크면 공중에 있는 동안 맞지 않는다")]
    [SerializeField, Min(0f)] private float _leapHeight;
    [Tooltip("목표 지점을 대상 위치보다 이 거리만큼 더 지나친 곳으로 잡는다. 돌진처럼 대상을 뚫고 지나가는 공격에 쓴다")]
    [SerializeField, Min(0f)] private float _overshootDistance;

    [Header("위력 · 저항")]
    [Tooltip("플레이어 체력을 깎는 양")]
    [SerializeField, Min(0f)] private float _power = 10f;
    [Tooltip("이 공격 중(공격 시작 ~ 첫 판정, 또는 ResistEnd)의 경직 저항 레벨. 플레이어 공격의 경직 레벨이 이 값 이상이어야 경직된다. 적의 평소 저항 레벨이 더 높으면 그 값을 쓴다. 패링 성공 경직은 이 값을 경직 레벨로 쓴다")]
    [SerializeField, Min(0)] private int _staggerResistLevel;
    [Tooltip("끄면 경직 저항이 첫 판정에서 풀린다. 켜면 클립의 ResistEnd 이벤트까지 유지된다(이벤트가 없으면 공격이 끝날 때까지)")]
    [SerializeField] private bool _resistUntilEvent;

    [Header("완벽 회피")]
    [Tooltip("대시 · 회피 무적 시간 안에 닿으면 완벽 회피(슬로모션)가 되는 공격인지 여부. 끄면 무적으로 무시만 된다")]
    [SerializeField] private bool _canPerfectDodge = true;

    [Header("플레이어 반응")]
    [SerializeField] private HitReaction _reaction = HitReaction.Hit;
    [Tooltip("[피격 · 큰 피격] 멈춰 있는 시간(초). 강공격처럼 큰 경직은 길게 준다")]
    [SerializeField, Min(0f)] private float _hitStunTime = 0.4f;
    [Tooltip("[피격 · 큰 피격] 경직이 시작될 때 공격 반대 방향으로 밀려나는 거리. 0이면 제자리에서 경직")]
    [SerializeField, Min(0f)] private float _hitPushDistance;
    [Tooltip("[피격 · 큰 피격] 밀려나는 데 걸리는 시간(초). 경직 시간보다 길면 경직이 끝날 때 함께 멈춘다")]
    [SerializeField, Min(0.01f)] private float _hitPushDuration = 0.2f;
    [Tooltip("[넉백] 공격 반대 방향으로 밀려나는 거리")]
    [SerializeField, Min(0f)] private float _knockbackDistance = 3f;
    [Tooltip("[넉백] 밀려나는 데 걸리는 시간(초)")]
    [SerializeField, Min(0.01f)] private float _knockbackDuration = 0.5f;
    [Tooltip("[넉백] 밀려난 뒤 일어나는 데 걸리는 시간(초)")]
    [SerializeField, Min(0f)] private float _getUpTime = 1f;
    [Tooltip("피격 · 넉백 중 대시 · 회피로 끊을 수 있는지 여부")]
    [SerializeField] private bool _canDashCancel = true;

    [Header("동작")]
    [Tooltip("이 공격의 동작(모션) 목록. 순서대로 재생하고 각 동작의 상태가 끝나면 다음 동작으로 넘어간다")]
    [SerializeField] private EnemyAttackStep[] _steps = { new EnemyAttackStep() };

    public float Cooldown => _cooldown;
    public float BaseWeight => _baseWeight;
    public float WeightGrowth => _weightGrowth;
    public float MinUseDistance => _minUseDistance;
    public bool CanUseOutOfRange => _canUseOutOfRange;
    public float TurnSpeed => _turnSpeed;
    public float LeapHeight => _leapHeight;
    public float OvershootDistance => _overshootDistance;
    public float Power => _power;
    public int StaggerResistLevel => _staggerResistLevel;
    public bool ResistUntilEvent => _resistUntilEvent;
    public bool CanPerfectDodge => _canPerfectDodge;
    public HitReaction Reaction => _reaction;
    public float HitStunTime => _hitStunTime;
    public float HitPushDistance => _hitPushDistance;
    public float HitPushDuration => _hitPushDuration;
    public float KnockbackDistance => _knockbackDistance;
    public float KnockbackDuration => _knockbackDuration;
    public float GetUpTime => _getUpTime;
    public bool CanDashCancel => _canDashCancel;
    public int StepCount => _steps.Length;

    // 패링 이어가기 동작이 있는 공격(연속 패링 패턴)인지
    public bool HasParryChain
    {
        get
        {
            foreach(EnemyAttackStep step in _steps)
            {
                if(step.ContinuesOnParry) return true;
            }

            return false;
        }
    }

    public EnemyAttackStep GetStep(int index) => _steps[index];
}

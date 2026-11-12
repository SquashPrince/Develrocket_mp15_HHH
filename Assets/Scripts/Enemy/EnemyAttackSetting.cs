using System;
using UnityEngine;

// 적 공격 하나의 설정 묶음. 예고 · 판정 · 플레이어가 맞았을 때의 반응을 공격마다 Inspector 에서 정한다.
// MonoBehaviour 가 아닌 일반 클래스이므로 [Serializable] 을 붙여야 Inspector 에 펼쳐서 보인다.
[Serializable]
public class EnemyAttackSetting
{
    // 플레이어가 맞았을 때의 반응. 피격은 제자리 경직, 넉백은 밀려난 뒤 일어난다.
    // 큰 피격은 피격과 같이 경직 · 밀림 값을 쓰고 모션만 큰 피격 모션으로 나온다. (강공격)
    // Inspector 에 저장된 값은 순서 번호이므로, 새 반응은 항상 맨 뒤에 추가한다.
    public enum HitReaction
    {
        Hit,
        HeavyHit,
        Knockback
    }

    [Tooltip("Inspector 와 로그에서 공격을 구분하기 위한 이름")]
    [SerializeField] private string _name = "Attack";

    [Header("모션 · 선택 (일반 적)")]
    [Tooltip("이 공격의 단계별 Animator 상태 이름. 순서대로 재생하고 각 상태가 끝나면 다음 상태로 넘어간다. 예: 펀치는 \"Attack 1\" 하나, 스핀은 \"Attack 3\", \"Attack 4\", \"Attack 5\"")]
    [SerializeField] private string[] _animationStates = { "Attack 1" };
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

    [Header("도약 · 돌진 (LeapStart 이벤트가 있는 공격)")]
    [Tooltip("도약 중 외형이 떠오르는 최고 높이. 0 이면 땅을 따라 직선으로 돌진한다. 0 보다 크면 공중에 있는 동안 맞지 않는다")]
    [SerializeField, Min(0f)] private float _leapHeight;
    [Tooltip("목표 지점을 대상 위치보다 이 거리만큼 더 지나친 곳으로 잡는다. 돌진처럼 대상을 뚫고 지나가는 공격에 쓴다")]
    [SerializeField, Min(0f)] private float _overshootDistance;


    [Header("판정")]
    [Tooltip("패링할 수 있는 공격인지 여부. 켜면 클립의 Telegraph 이벤트부터 첫 Hit 이벤트까지 패링 가능 시간이 열리고 예고 표시가 켜진다. Telegraph 이벤트는 준비 단계 클립에 넣는다")]
    [SerializeField] private bool _isParryable;
    [Tooltip("[패링 공격] 패링당했을 때 적이 경직하는 시간(초)")]
    [SerializeField, Min(0f)] private float _parryStaggerTime = 1.5f;
    [Tooltip("이 공격 중(공격 시작 ~ 첫 Hit, 또는 ResistEnd)의 경직 저항 레벨. 플레이어 공격의 경직 레벨이 이 값 이상이어야 경직된다. 적의 평소 저항 레벨이 더 높으면 그 값을 쓴다. 패링 성공 경직은 이 값을 경직 레벨로 쓴다")]
    [SerializeField, Min(0)] private int _staggerResistLevel;
    [Tooltip("[일반 적] 끄면 경직 저항이 첫 Hit 이벤트에서 풀린다. 켜면 클립의 ResistEnd 이벤트까지 유지된다(이벤트가 없으면 공격이 끝날 때까지)")]
    [SerializeField] private bool _resistUntilEvent;
    [Tooltip("[일반 적] 끄면 Hit 이벤트 순간에 한 번 판정한다. 켜면 Hit 이벤트부터 그 단계가 끝날 때까지 매 프레임 판정한다. 어느 쪽이든 플레이어는 공격 한 번에 한 번만 맞는다")]
    [SerializeField] private bool _isContinuousHit;
    [Tooltip("대시 · 회피 무적 시간 안에 닿으면 완벽 회피(슬로모션)가 되는 공격인지 여부. 끄면 무적으로 무시만 된다")]
    [SerializeField] private bool _canPerfectDodge = true;
    [Tooltip("판정 구의 중심 (적 기준 위치)")]
    [SerializeField] private Vector3 _hitCenter = new Vector3(0f, 1f, 1.2f);
    [SerializeField, Min(0f)] private float _hitRadius = 1.5f;
    [Tooltip("플레이어 체력을 깎는 양")]
    [SerializeField, Min(0f)] private float _power = 10f;

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

    public string Name => _name;
    public string[] AnimationStates => _animationStates;
    public float Cooldown => _cooldown;
    public float BaseWeight => _baseWeight;
    public float WeightGrowth => _weightGrowth;
    public float MinUseDistance => _minUseDistance;
    public bool CanUseOutOfRange => _canUseOutOfRange;
    public float LeapHeight => _leapHeight;
    public float OvershootDistance => _overshootDistance;
    public float ParryStaggerTime => _parryStaggerTime;
    public int StaggerResistLevel => _staggerResistLevel;
    public bool ResistUntilEvent => _resistUntilEvent;
    public bool IsContinuousHit => _isContinuousHit;
    public bool IsParryable => _isParryable;
    public bool CanPerfectDodge => _canPerfectDodge;
    public Vector3 HitCenter => _hitCenter;
    public float HitRadius => _hitRadius;
    public float Power => _power;
    public HitReaction Reaction => _reaction;
    public float HitStunTime => _hitStunTime;
    public float HitPushDistance => _hitPushDistance;
    public float HitPushDuration => _hitPushDuration;
    public float KnockbackDistance => _knockbackDistance;
    public float KnockbackDuration => _knockbackDuration;
    public float GetUpTime => _getUpTime;
    public bool CanDashCancel => _canDashCancel;
}

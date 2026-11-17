using System;
using UnityEngine;

// 적 공격을 이루는 동작(모션) 하나의 값. 공격은 동작 목록을 가지며 순서대로 이어 재생한다.
// 모션마다 달라지는 값(재생할 Animator 상태, 판정 방식 · 타격 시점 · 범위, 막타, 추적, 패링)을 담는다. 위력 · 플레이어 반응 같은 값은 공격 공통이다.
// MonoBehaviour 가 아닌 일반 클래스이므로 [Serializable] 을 붙여야 Inspector 에 펼쳐서 보인다.
[Serializable]
public class EnemyAttackStep
{
    // 판정 방식. Inspector 에 저장된 값은 순서 번호이므로, 새 방식은 항상 맨 뒤에 추가한다.
    //   구간 : 판정 시작(HitStart) ~ 판정 끝(HitEnd) 사이의 타격 시점마다 범위 안의 캐릭터를 맞힌다. 판정 끝이 없으면 동작이 끝날 때까지 간격마다 맞힌다
    //   한 순간 : 판정 시작(HitStart) 순간에 한 번 판정한다
    // 타격마다 대미지 · 경직 · 완벽 회피 · 패링을 따로 판정한다. 한 타격에서는 캐릭터마다 한 번만 맞는다.
    public enum HitMode
    {
        Window,
        Instant
    }

    // 구간 판정의 타격 시점 정하기. Inspector 에 저장된 값은 순서 번호이므로, 새 방식은 항상 맨 뒤에 추가한다.
    //   간격 : 판정 시작부터 간격마다 타격한다. 판정 끝 순간에 막타를 한 번 더 한다
    //   횟수 : 구간을 횟수로 나눠 타격한다. 시작과 끝 시점을 모두 포함하고, 끝 시점의 타격이 막타다 (예 : 3 타면 시작 · 가운데 · 끝)
    public enum WindowTiming
    {
        Interval,
        Count
    }

    [Tooltip("재생할 Animator 상태 이름. 공통 Animator 의 상태 이름과 같아야 한다 (예 : \"Attack 1\")")]
    [SerializeField] private string _animationState = "Attack 1";

    [Header("판정")]
    [Tooltip("구간 : 판정 시작 ~ 끝 사이의 타격 시점마다 판정 / 한 순간 : 판정 시작 순간 한 번 판정")]
    [SerializeField] private HitMode _hitMode = HitMode.Instant;
    [Tooltip("[구간] 간격 : 간격마다 타격 + 판정 끝 순간 막타 / 횟수 : 구간을 횟수로 나눠 타격 (시작 · 끝 포함, 끝이 막타)")]
    [SerializeField] private WindowTiming _windowTiming = WindowTiming.Interval;
    [Tooltip("[구간 · 간격] 타격 간격(초, 적 전용 시간)")]
    [SerializeField, Min(0.01f)] private float _hitInterval = 0.2f;
    [Tooltip("[구간 · 횟수] 구간 동안의 타격 횟수. 시작과 끝을 포함해 고르게 나눈다. 판정 끝 이벤트가 없으면 시작 시점에 한 번만 타격한다")]
    [SerializeField, Min(1)] private int _hitCount = 3;

    [Header("막타")]
    [Tooltip("[구간] 켜면 막타(간격 : 판정 끝 순간 / 횟수 : 마지막 타격)만 아래 반응을 쓴다. 끄면 막타도 공격의 반응을 쓴다. 위력 · 밀림 · 넉백 값은 공격 공통 값을 쓴다")]
    [SerializeField] private bool _useFinalReaction;
    [SerializeField] private EnemyAttackData.HitReaction _finalReaction = EnemyAttackData.HitReaction.Knockback;

    [Header("판정 범위")]
    [Tooltip("상자 : 중심 · 크기 / 구 : 중심 · 반지름 / 캡슐 : 중심 · 반지름 · 길이 (적 앞쪽으로 긴 막대) / 부채꼴 : 중심 · 반지름 · 각도 · 안쪽 반지름 · 높이")]
    [SerializeField] private HitShape _hitShape = HitShape.Sphere;
    [Tooltip("판정 범위의 중심 (적 기준 위치)")]
    [SerializeField] private Vector3 _hitCenter = new Vector3(0f, 1f, 1.2f);
    [Tooltip("[상자] 판정 상자의 크기 (좌우 · 위아래 · 앞뒤)")]
    [SerializeField] private Vector3 _hitSize = new Vector3(2f, 2f, 2f);
    [Tooltip("[구 · 캡슐 · 부채꼴] 반지름 (부채꼴은 바깥 반지름)")]
    [SerializeField, Min(0f)] private float _hitRadius = 1.5f;
    [Tooltip("[캡슐] 적 앞쪽 방향으로의 전체 길이. 반지름의 두 배보다 짧으면 구와 같다")]
    [SerializeField, Min(0f)] private float _hitLength = 3f;
    [Tooltip("[부채꼴] 정면 기준 펼친 각도. 좌우로 절반씩 펼친다 (180 = 반원, 360 = 원)")]
    [SerializeField, Range(0f, 360f)] private float _hitAngle = 90f;
    [Tooltip("[부채꼴] 안쪽 반지름. 이보다 가까운 대상은 맞지 않는다 (0 보다 크면 도넛형)")]
    [SerializeField, Min(0f)] private float _hitInnerRadius;
    [Tooltip("[부채꼴] 판정 높이(위아래 합친 두께). 중심 높이 기준 위아래로 절반씩이다")]
    [SerializeField, Min(0f)] private float _hitHeight = 2f;

    [Header("추적")]
    [Tooltip("켜면 이 동작 동안 대상 쪽으로 이동한다. 서로의 몸 반지름이 닿으면 멈춘다. 이동 모션은 재생하지 않는다 (예 : 회전 공격)")]
    [SerializeField] private bool _followsTarget;
    [Tooltip("[추적] 이동 속도. 적 이동 속도에 곱하는 배율이다 (1 = 같은 속도)")]
    [SerializeField, Min(0f)] private float _followSpeedRate = 1f;
    [Tooltip("켜면 이 동작 동안 계속 대상 쪽으로 몸을 돌린다 (예 : 돌진). 끄면 첫 판정 전까지만 돈다")]
    [SerializeField] private bool _facesTarget;

    [Header("패링")]
    [Tooltip("패링할 수 있는 동작인지 여부. 켜면 클립의 Telegraph 이벤트부터 첫 판정까지 패링 가능 시간이 열리고 예고 표시가 켜진다. Telegraph 이벤트는 준비 단계 클립에 넣는다")]
    [SerializeField] private bool _isParryable;
    [Tooltip("[패링] 패링당했을 때 적이 경직하는 시간(초)")]
    [SerializeField, Min(0f)] private float _parryStaggerTime = 1.5f;

    public string AnimationState => _animationState;
    public HitMode Mode => _hitMode;
    public WindowTiming Timing => _windowTiming;
    public float HitInterval => _hitInterval;
    public int HitCount => _hitCount;
    public bool UseFinalReaction => _useFinalReaction;
    public EnemyAttackData.HitReaction FinalReaction => _finalReaction;
    // 판정 범위. 찾기 · 그리기는 공용 판정 범위가 한다.
    public HitRange Range => new HitRange(_hitShape, _hitCenter, _hitSize, _hitRadius, _hitLength, _hitAngle, _hitInnerRadius, _hitHeight);
    public bool FollowsTarget => _followsTarget;
    public float FollowSpeedRate => _followSpeedRate;
    public bool FacesTarget => _facesTarget;
    public bool IsParryable => _isParryable;
    public float ParryStaggerTime => _parryStaggerTime;
}

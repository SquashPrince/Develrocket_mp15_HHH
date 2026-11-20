using System;
using UnityEngine;

// 액션을 이루는 동작(모션) 하나의 값. 액션은 동작 목록을 가지며, 1개면 한 동작, 여러 개면 순서대로 이어 재생한다.
// 모션마다 달라지는 값(재생할 Animator 상태, 판정 방식 · 범위, 전진, 섞는 시간)을 담는다. 위력 · 게이지 같은 타격 값은 액션 공통이다.
// MonoBehaviour 가 아닌 일반 클래스이므로 [Serializable] 을 붙여야 Inspector 에 펼쳐서 보인다.
[Serializable]
public class ActionStep
{
    // 판정 방식. Inspector 에 저장된 값은 순서 번호이므로, 새 방식은 항상 맨 뒤에 추가한다.
    //   구간 : 판정 시작(HitStart) ~ 판정 끝(HitEnd) · 강타 끝(ImpactEnd) 사이에 타격 시점을 두고, 시점마다 범위 안의 적을 모두 맞힌다
    //   한 순간 : 판정 시작(HitStart) 순간에 한 번만 판정한다
    // 어느 방식이든 강타 끝(ImpactEnd) 순간에 강타 판정을 한 번 하고 판정을 끝낸다.
    public enum HitMode
    {
        Window,
        Instant
    }

    // 구간 판정의 타격 시점 정하기. Inspector 에 저장된 값은 순서 번호이므로, 새 방식은 항상 맨 뒤에 추가한다.
    //   간격 : 판정 시작부터 간격마다 타격한다. 구간 끝을 넘어가는 타격은 하지 않는다
    //   횟수 : 구간을 횟수로 나눠 간격을 정한다. 시작과 끝 시점을 모두 포함한다 (예 : 3타면 시작 · 가운데 · 끝)
    // 강타 끝으로 끝나는 구간은 끝 시점의 타격이 강타다. 횟수 방식에서는 강타가 횟수의 마지막 1타다.
    public enum WindowTiming
    {
        Interval,
        Count
    }

    // 이 동작 중 끼어들 수 있는 행동. 여러 개를 함께 고르는 값이라 각 항목이 비트 하나를 차지한다.
    // Inspector 에 저장된 값은 비트를 더한 숫자이므로, 새 항목은 항상 맨 뒤에 다음 비트로 추가한다.
    [Flags]
    public enum Interrupt
    {
        None = 0,
        Dash = 1 << 0,
        Ultimate = 1 << 1,
        Swap = 1 << 2,
        // 적이 패링 가능 시간을 열어 교체가 지원 패링이 될 때만 교체를 허용한다. 연속 패링을 받아칠 때 쓴다.
        ParrySwap = 1 << 3
    }

    [Tooltip("재생할 Animator 상태 이름. Animator Controller 의 상태 이름과 같아야 한다")]
    [SerializeField] private string _animationState = "";

    [Header("판정 방식")]
    [Tooltip("구간 : 판정 시작 ~ 끝 사이의 타격 시점마다 맞힌다 / 한 순간 : 판정 시작 순간 한 번만 판정한다")]
    [SerializeField] private HitMode _hitMode = HitMode.Window;
    [Tooltip("[구간] 간격 : 간격마다 타격 / 횟수 : 구간을 횟수로 나눠 타격 (시작 · 끝 포함)")]
    [SerializeField] private WindowTiming _windowTiming = WindowTiming.Interval;
    [Tooltip("[구간 · 간격] 타격 간격(초, 캐릭터 시간). 판정 시작부터 이 간격마다 타격하고, 구간 끝을 넘어가는 타격은 하지 않는다")]
    [SerializeField, Min(0.01f)] private float _hitInterval = 0.1f;
    [Tooltip("[구간 · 횟수] 구간 동안의 타격 횟수. 시작과 끝을 포함해 고르게 나눈다. 강타 끝으로 끝나면 마지막 1타가 강타다")]
    [SerializeField, Min(1)] private int _hitCount = 1;
    [Tooltip("[구간] 켜면 경직 · 히트렉은 판정 구간(판정 시작 한 번)에서 처음 적중한 타격에만 건다. 나머지 타격은 위력 · 에너지 · 게이지만 준다. 강타는 항상 경직 · 히트렉을 건다")]
    [SerializeField] private bool _reactOnFirstHitOnly;
    [Tooltip("적중할 때마다 공격한 캐릭터만 멈추는 시간(초, 실제 시간). 적은 멈추지 않는다. 0 이면 멈추지 않는다")]
    [SerializeField, Min(0f)] private float _hitLagTime;
    [Tooltip("[구간] 켜면 히트렉은 판정 구간(판정 시작 한 번)에서 처음 적중한 타격에만 건다. 경직은 타격마다 그대로 건다. 펀치 러시처럼 다단히트마다 멈추면 늘어질 때 쓴다. 강타는 항상 히트렉을 건다")]
    [SerializeField] private bool _hitLagOnFirstHitOnly;

    [Header("판정 범위")]
    [Tooltip("상자 : 중심 · 크기 / 구 : 중심 · 반지름 / 캡슐 : 중심 · 반지름 · 길이 (캐릭터 앞쪽으로 긴 막대) / 부채꼴 : 중심 · 반지름 · 각도 · 안쪽 반지름 · 높이")]
    [SerializeField] private HitShape _hitShape = HitShape.Box;
    [Tooltip("판정 범위의 중심 (캐릭터 기준 위치)")]
    [SerializeField] private Vector3 _hitCenter = new Vector3(0f, 1f, 1f);
    [Tooltip("[상자] 판정 상자의 크기 (좌우 · 위아래 · 앞뒤)")]
    [SerializeField] private Vector3 _hitSize = new Vector3(1.5f, 1.5f, 2f);
    [Tooltip("[구 · 캡슐 · 부채꼴] 반지름 (부채꼴은 바깥 반지름)")]
    [SerializeField, Min(0f)] private float _hitRadius = 1.5f;
    [Tooltip("[캡슐] 캐릭터 앞쪽 방향으로의 전체 길이. 반지름의 두 배보다 짧으면 구와 같다")]
    [SerializeField, Min(0f)] private float _hitLength = 3f;
    [Tooltip("[부채꼴] 정면 기준 펼친 각도. 좌우로 절반씩 펼친다 (180 = 반원, 360 = 원)")]
    [SerializeField, Range(0f, 360f)] private float _hitAngle = 90f;
    [Tooltip("[부채꼴] 안쪽 반지름. 이보다 가까운 대상은 맞지 않는다 (0 보다 크면 도넛형)")]
    [SerializeField, Min(0f)] private float _hitInnerRadius;
    [Tooltip("[부채꼴] 판정 높이(위아래 합친 두께). 중심 높이 기준 위아래로 절반씩이다")]
    [SerializeField, Min(0f)] private float _hitHeight = 2f;

    [Header("이동 · 연결")]
    [Tooltip("이 동작을 시작할 때 앞으로 나아가는 거리. 대상이 있으면 대상 앞까지 다가가는 거리를 대신 쓴다")]
    [SerializeField, Min(0f)] private float _advanceDistance = 0.5f;
    [Tooltip("앞으로 나아가는 데 걸리는 시간(초)")]
    [SerializeField, Min(0.01f)] private float _advanceDuration = 0.15f;
    [Tooltip("이전 모션에서 이 모션으로 섞으며 넘어가는 시간(초)")]
    [SerializeField, Min(0f)] private float _fadeTime = 0.1f;

    [Header("끼어들기")]
    [Tooltip("이 동작 중 허용할 행동. 체크한 것만 허용한다.\n대시 : 휘두르는 중 대시 · 회피로 끊기 (후딜은 항상 끊을 수 있다)\n필살기 : 게이지가 가득 차 있으면 필살기로 끊기 (공격 액션만 사용)\n교체 : 교체 키로 다른 캐릭터와 교체. 막힌 동작 중 누른 교체 키는 무시한다\n패링 교체 : 적이 패링 가능 시간을 열었을 때만 교체(지원 패링)를 허용한다. 교체를 켜면 이 항목은 필요 없다 (예 : 패링 반응 중 연속 패링)")]
    [SerializeField] private Interrupt _interrupts = Interrupt.Dash | Interrupt.Ultimate | Interrupt.Swap;

    [Header("소리")]
    [Tooltip("이 동작을 휘두를(시전할) 때 내는 소리. 적이 없어도 판정 시작(판정 시작이 없으면 강타 끝) 순간에 동작마다 한 번 낸다. 비워 두면 소리가 없다")]
    [SerializeField] private SoundGroup _swingSound;

    [Header("이펙트")]
    [Tooltip("휘두르기마다 고를 적중 이펙트 목록. 클립의 판정 시작(HitStart) · 강타 끝(ImpactEnd) 이벤트 Int 칸에 목록 번호(0 부터)를 넣는다. 구간 판정의 다단히트는 그 구간을 연 판정 시작의 번호를 쓴다. 비워 두면 적중 이펙트를 띄우지 않는다")]
    [SerializeField] private HitEffectSetting[] _hitEffects = new HitEffectSetting[0];
    [Tooltip("이 동작 중 띄울 이펙트 목록. 클립에 동작 이펙트(MotionEffect) 이벤트를 찍고 Int 칸에 목록 번호(0 부터)를 넣으면 그 순간 그 번호의 이펙트를 캐릭터에 붙여 띄운다.\nPrefab 이 반복(Looping) 이펙트면 판정 끝 · 강타 끝 이벤트가 오거나 이 동작이 끝날 때까지 반복한다")]
    [SerializeField] private MotionEffectSetting[] _motionEffects = new MotionEffectSetting[0];

    public string AnimationState => _animationState;
    // 재생 · 이벤트 구분에 쓰는 상태 해시. 상태 이름으로 매번 계산한다.
    public int AnimationHash => Animator.StringToHash(_animationState);
    public HitMode Mode => _hitMode;
    public float HitInterval => _hitInterval;
    public WindowTiming Timing => _windowTiming;
    public int HitCount => _hitCount;
    public bool ReactOnFirstHitOnly => _reactOnFirstHitOnly;
    public float HitLagTime => _hitLagTime;
    public bool HitLagOnFirstHitOnly => _hitLagOnFirstHitOnly;
    // 판정 범위. 찾기 · 그리기는 공용 판정 범위가 한다.
    public HitRange Range => new HitRange(_hitShape, _hitCenter, _hitSize, _hitRadius, _hitLength, _hitAngle, _hitInnerRadius, _hitHeight);
    public float AdvanceDistance => _advanceDistance;
    public float AdvanceDuration => _advanceDuration;
    public float FadeTime => _fadeTime;
    public SoundGroup SwingSound => _swingSound;

    // 이 동작 중 interrupt 행동을 허용하는지
    public bool Allows(Interrupt interrupt) => (_interrupts & interrupt) != 0;

    // 적중 이펙트 목록을 쓰는지. 비어 있으면 적중 이펙트를 띄우지 않는다.
    public bool HasHitEffects => _hitEffects.Length > 0;

    // index 번 적중 이펙트. 목록에 없는 번호면 null 이다.
    public HitEffectSetting GetHitEffect(int index)
    {
        if(index < 0 || index >= _hitEffects.Length) return null;

        return _hitEffects[index];
    }

    // index 번 동작 이펙트. 목록에 없는 번호면 null 이다.
    public MotionEffectSetting GetMotionEffect(int index)
    {
        if(index < 0 || index >= _motionEffects.Length) return null;

        return _motionEffects[index];
    }
}

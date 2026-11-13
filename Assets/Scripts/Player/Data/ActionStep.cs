using System;
using UnityEngine;

// 액션을 이루는 동작(모션) 하나의 값. 액션은 동작 목록을 가지며, 1개면 한 동작, 여러 개면 순서대로 이어 재생한다.
// 모션마다 달라지는 값(재생할 Animator 상태, 판정 방식 · 범위, 전진, 섞는 시간)을 담는다. 위력 · 게이지 같은 타격 값은 액션 공통이다.
// MonoBehaviour 가 아닌 일반 클래스이므로 [Serializable] 을 붙여야 Inspector 에 펼쳐서 보인다.
[Serializable]
public class ActionStep
{
    // 판정 범위 모양. Inspector 에 저장된 값은 순서 번호이므로, 새 모양은 항상 맨 뒤에 추가한다.
    public enum HitShape
    {
        Box,
        Sphere,
        Capsule
    }

    // 판정 방식. Inspector 에 저장된 값은 순서 번호이므로, 새 방식은 항상 맨 뒤에 추가한다.
    //   구간 : 판정 시작(HitStart) ~ 판정 끝(HitEnd) 동안 판정이 켜지고, 같은 적을 간격 · 최대 횟수만큼 맞힌다
    //   한 순간 : 판정 시작(HitStart) 순간에 한 번만 판정한다
    // 어느 방식이든 강타 끝(ImpactEnd) 순간에 강타 판정을 한 번 하고 판정을 끝낸다.
    public enum HitMode
    {
        Window,
        Instant
    }

    [Tooltip("재생할 Animator 상태 이름. Animator Controller 의 상태 이름과 같아야 한다")]
    [SerializeField] private string _animationState = "";

    [Header("판정 방식")]
    [Tooltip("구간 : 판정 시작 ~ 끝 동안 켜져 같은 적을 여러 번 맞힌다 / 한 순간 : 판정 시작 순간 한 번만 판정한다")]
    [SerializeField] private HitMode _hitMode = HitMode.Window;
    [Tooltip("[구간] 같은 적을 다시 맞히기까지의 간격(초, 캐릭터 시간). 히트렉으로 멈춘 동안은 흐르지 않는다")]
    [SerializeField, Min(0.01f)] private float _hitInterval = 0.1f;
    [Tooltip("[구간] 판정 구간 한 번에 같은 적을 맞히는 최대 횟수. 강타 끝의 강타 판정은 이 횟수와 따로 한 번 더 맞는다")]
    [SerializeField, Min(1)] private int _maxHitsPerTarget = 1;
    [Tooltip("[구간] 켜면 경직 · 히트렉은 판정 구간에서 각 적의 첫 타격에만 건다. 나머지 타격은 위력 · 에너지 · 게이지만 준다. 강타 끝의 강타는 항상 경직 · 히트렉을 건다")]
    [SerializeField] private bool _reactOnFirstHitOnly;
    [Tooltip("적중할 때마다 공격한 캐릭터만 멈추는 시간(초, 실제 시간). 적은 멈추지 않는다. 0 이면 멈추지 않는다")]
    [SerializeField, Min(0f)] private float _hitLagTime;

    [Header("판정 범위")]
    [Tooltip("상자 : 중심 · 크기 / 구 : 중심 · 반지름 / 캡슐 : 중심 · 반지름 · 길이 (캐릭터 앞쪽으로 긴 막대)")]
    [SerializeField] private HitShape _hitShape = HitShape.Box;
    [Tooltip("판정 범위의 중심 (캐릭터 기준 위치)")]
    [SerializeField] private Vector3 _hitCenter = new Vector3(0f, 1f, 1f);
    [Tooltip("[상자] 판정 상자의 크기 (좌우 · 위아래 · 앞뒤)")]
    [SerializeField] private Vector3 _hitSize = new Vector3(1.5f, 1.5f, 2f);
    [Tooltip("[구 · 캡슐] 반지름")]
    [SerializeField, Min(0f)] private float _hitRadius = 1.5f;
    [Tooltip("[캡슐] 캐릭터 앞쪽 방향으로의 전체 길이. 반지름의 두 배보다 짧으면 구와 같다")]
    [SerializeField, Min(0f)] private float _hitLength = 3f;

    [Header("이동 · 연결")]
    [Tooltip("이 동작을 시작할 때 앞으로 나아가는 거리. 대상이 있으면 대상 앞까지 다가가는 거리를 대신 쓴다")]
    [SerializeField, Min(0f)] private float _advanceDistance = 0.5f;
    [Tooltip("앞으로 나아가는 데 걸리는 시간(초)")]
    [SerializeField, Min(0.01f)] private float _advanceDuration = 0.15f;
    [Tooltip("이전 모션에서 이 모션으로 섞으며 넘어가는 시간(초)")]
    [SerializeField, Min(0f)] private float _fadeTime = 0.1f;

    public string AnimationState => _animationState;
    // 재생 · 이벤트 구분에 쓰는 상태 해시. 상태 이름으로 매번 계산한다.
    public int AnimationHash => Animator.StringToHash(_animationState);
    public HitMode Mode => _hitMode;
    public float HitInterval => _hitInterval;
    public int MaxHitsPerTarget => _maxHitsPerTarget;
    public bool ReactOnFirstHitOnly => _reactOnFirstHitOnly;
    public float HitLagTime => _hitLagTime;
    public HitShape Shape => _hitShape;
    public Vector3 HitCenter => _hitCenter;
    public Vector3 HitSize => _hitSize;
    public float HitRadius => _hitRadius;
    public float HitLength => _hitLength;
    public float AdvanceDistance => _advanceDistance;
    public float AdvanceDuration => _advanceDuration;
    public float FadeTime => _fadeTime;
}

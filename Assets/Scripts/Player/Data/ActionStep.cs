using System;
using UnityEngine;

// 액션을 이루는 동작(모션) 하나의 값. 액션은 동작 목록을 가지며, 1개면 한 동작, 여러 개면 순서대로 이어 재생한다.
// 모션마다 달라지는 값(재생할 Animator 상태, 판정 범위, 전진, 섞는 시간)을 담는다. 위력 · 게이지 같은 타격 값은 액션 공통이다.
// MonoBehaviour 가 아닌 일반 클래스이므로 [Serializable] 을 붙여야 Inspector 에 펼쳐서 보인다.
[Serializable]
public class ActionStep
{
    // 판정 범위 모양. 기본은 판정 기능에 있는 공통 상자를 쓴다.
    // Inspector 에 저장된 값은 순서 번호이므로, 새 모양은 항상 맨 뒤에 추가한다.
    public enum HitShape
    {
        Default,
        Box,
        Sphere,
        Capsule
    }

    [Tooltip("재생할 Animator 상태 이름. Animator Controller 의 상태 이름과 같아야 한다")]
    [SerializeField] private string _animationState = "";

    [Header("판정 범위")]
    [Tooltip("기본 : 판정 기능의 공통 상자 / 상자 : 중심 · 크기 / 구 : 중심 · 반지름 / 캡슐 : 중심 · 반지름 · 길이 (캐릭터 앞쪽으로 긴 막대)")]
    [SerializeField] private HitShape _hitShape = HitShape.Default;
    [Tooltip("[상자 · 구 · 캡슐] 판정 범위의 중심 (캐릭터 기준 위치)")]
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
    public HitShape Shape => _hitShape;
    public Vector3 HitCenter => _hitCenter;
    public Vector3 HitSize => _hitSize;
    public float HitRadius => _hitRadius;
    public float HitLength => _hitLength;
    public float AdvanceDistance => _advanceDistance;
    public float AdvanceDuration => _advanceDuration;
    public float FadeTime => _fadeTime;
}

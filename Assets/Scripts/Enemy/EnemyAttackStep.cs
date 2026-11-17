using System;
using UnityEngine;

// 적 공격을 이루는 동작(모션) 하나의 값. 공격은 동작 목록을 가지며 순서대로 이어 재생한다.
// 모션마다 달라지는 값(재생할 Animator 상태, 판정 방식 · 범위, 패링)을 담는다. 위력 · 플레이어 반응 같은 값은 공격 공통이다.
// MonoBehaviour 가 아닌 일반 클래스이므로 [Serializable] 을 붙여야 Inspector 에 펼쳐서 보인다.
[Serializable]
public class EnemyAttackStep
{
    // 판정 방식. Inspector 에 저장된 값은 순서 번호이므로, 새 방식은 항상 맨 뒤에 추가한다.
    //   구간 : 판정 시작(HitStart)부터 이 동작이 끝날 때까지 매 프레임 판정한다
    //   한 순간 : 판정 시작(HitStart) 순간에 한 번만 판정한다
    // 어느 방식이든 플레이어 캐릭터는 공격 한 번에 한 번만 맞는다.
    public enum HitMode
    {
        Window,
        Instant
    }

    [Tooltip("재생할 Animator 상태 이름. 공통 Animator 의 상태 이름과 같아야 한다 (예 : \"Attack 1\")")]
    [SerializeField] private string _animationState = "Attack 1";

    [Header("판정")]
    [Tooltip("구간 : 판정 시작부터 동작 끝까지 매 프레임 판정 / 한 순간 : 판정 시작 순간 한 번만 판정")]
    [SerializeField] private HitMode _hitMode = HitMode.Instant;
    [Tooltip("판정 구의 중심 (적 기준 위치)")]
    [SerializeField] private Vector3 _hitCenter = new Vector3(0f, 1f, 1.2f);
    [SerializeField, Min(0f)] private float _hitRadius = 1.5f;

    [Header("패링")]
    [Tooltip("패링할 수 있는 동작인지 여부. 켜면 클립의 Telegraph 이벤트부터 첫 판정까지 패링 가능 시간이 열리고 예고 표시가 켜진다. Telegraph 이벤트는 준비 단계 클립에 넣는다")]
    [SerializeField] private bool _isParryable;
    [Tooltip("[패링] 패링당했을 때 적이 경직하는 시간(초)")]
    [SerializeField, Min(0f)] private float _parryStaggerTime = 1.5f;

    public string AnimationState => _animationState;
    public HitMode Mode => _hitMode;
    public Vector3 HitCenter => _hitCenter;
    public float HitRadius => _hitRadius;
    public bool IsParryable => _isParryable;
    public float ParryStaggerTime => _parryStaggerTime;
}

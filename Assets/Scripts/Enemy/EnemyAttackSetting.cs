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

    [Header("예고")]
    [Tooltip("예고(색 · 크기 변화)가 이어지는 시간(초)")]
    [SerializeField, Min(0f)] private float _telegraphTime = 0.6f;
    [Tooltip("예고가 끝난 뒤 판정까지의 시간(초). 패링 공격은 이 시간까지 패링을 입력할 수 있다")]
    [SerializeField, Min(0f)] private float _hitDelay;
    [Tooltip("예고 동안 몸 색을 바꿀지 여부. 패링 공격(노란색)만 켜고, 약 · 강공격은 색 없이 크기 · 예고 시간으로 구분한다")]
    [SerializeField] private bool _useTelegraphColor;
    [SerializeField] private Color _telegraphColor = Color.yellow;
    [Tooltip("예고 동안 몸 크기 배율")]
    [SerializeField, Min(0.1f)] private float _telegraphScale = 1.1f;

    [Header("판정")]
    [Tooltip("패링할 수 있는 공격인지 여부. 켜면 예고 시작부터 판정까지 패링 가능 시간이 열린다")]
    [SerializeField] private bool _isParryable;
    [Tooltip("[패링 공격] 패링당했을 때 적이 경직하는 시간(초)")]
    [SerializeField, Min(0f)] private float _parryStaggerTime = 1.5f;
    [Tooltip("이 공격 중(예고 ~ 판정)의 경직 저항 레벨. 플레이어 공격의 경직 레벨이 이 값 이상이어야 경직된다. 적의 평소 저항 레벨이 더 높으면 그 값을 쓴다. 패링 성공 경직은 이 값을 경직 레벨로 쓴다")]
    [SerializeField, Min(0)] private int _staggerResistLevel;
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
    public float TelegraphTime => _telegraphTime;
    public float HitDelay => _hitDelay;
    public float ParryStaggerTime => _parryStaggerTime;
    public int StaggerResistLevel => _staggerResistLevel;
    public bool UseTelegraphColor => _useTelegraphColor;
    public Color TelegraphColor => _telegraphColor;
    public float TelegraphScale => _telegraphScale;
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

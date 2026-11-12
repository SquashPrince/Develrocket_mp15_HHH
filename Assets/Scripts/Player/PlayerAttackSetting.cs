using System;
using UnityEngine;

// 공격 하나의 설정 묶음. 콤보 단계 · 강공격 · 스킬 · 특수스킬이 같은 형태로 Inspector 에서 값을 정한다.
// 판정 범위 모양도 공격마다 정한다. (예: 내려 베기는 좌우가 좁은 상자, 회전 베기는 캐릭터 중심의 구)
// MonoBehaviour 가 아닌 일반 클래스이므로 [Serializable] 을 붙여야 Inspector 에 펼쳐서 보인다.
[Serializable]
public class PlayerAttackSetting
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

    [Header("수치 · 동작")]
    [Tooltip("적중한 적 하나마다 얻는 에너지")]
    [SerializeField, Min(0f)] private float _energyGain = 5f;
    [Tooltip("적중한 적 하나마다 얻는 필살기 게이지")]
    [SerializeField, Min(0f)] private float _ultimateGaugeGain = 50f;
    [Tooltip("적중 시 위력. 맞은 적의 체력을 이만큼 깎는다")]
    [SerializeField, Min(0f)] private float _power = 10f;
    [Tooltip("경직 레벨. 맞은 적의 경직 저항 레벨 이상이면 경직시킨다")]
    [SerializeField, Min(0)] private int _staggerLevel = 1;
    [Tooltip("맞은 적을 경직시키는 시간(초, 적 전용 시간). 0이면 경직시키지 않는다. 경직하면 적이 진행 중이던 공격이 끊긴다")]
    [SerializeField, Min(0f)] private float _staggerTime;
    [Tooltip("휘두르기 시작부터 앞으로 나아가는 거리")]
    [SerializeField, Min(0f)] private float _advanceDistance = 0.5f;
    [Tooltip("앞으로 나아가는 데 걸리는 시간(초)")]
    [SerializeField, Min(0.01f)] private float _advanceDuration = 0.15f;
    [Tooltip("이전 모션에서 이 모션으로 섞으며 넘어가는 시간(초)")]
    [SerializeField, Min(0f)] private float _fadeTime = 0.1f;
    [Tooltip("[콤보 단계만 사용] 공격 동작이 끝난 뒤 이 시간 안에 공격하면 다음 단계로 이어진다")]
    [SerializeField, Min(0f)] private float _inputBuffer = 0.4f;
    [Tooltip("대상에게 전진 거리보다 멀리 다가갈 때 하체에 달리기 모션을 덮을지 여부. 하체 동작이 중요한 모션이면 끈다")]
    [SerializeField] private bool _useLegRun = true;
    [Tooltip("하체 달리기를 쓸 때, 대상과 거리에 상관없이 전진하는 동안 항상 하체 달리기를 보여줄지 여부 (대시 공격처럼 달려 나가는 공격)")]
    [SerializeField] private bool _alwaysLegRun;
    [Tooltip("휘두르는 중 대시 · 회피로 끊을 수 있는지. 끄면 휘두르기가 끝날 때까지 대시 입력을 무시한다. 후딜은 항상 끊을 수 있다")]
    [SerializeField] private bool _canDashCancel = true;
    [Tooltip("이 공격 중(휘두르기 · 후딜) 필살기 게이지가 가득 차 있으면 필살기로 끊을 수 있는지")]
    [SerializeField] private bool _canUltimateCancel = true;
    [Tooltip("휘두르는 동안 적 공격에 맞지 않는지 여부. 회피하지 않아도 무적이며, 후딜에 들어가면 풀린다. 무적 중 들어온 노란 공격은 패링되지 않는다")]
    [SerializeField] private bool _isInvincibleOnSwing;

    public HitShape Shape => _hitShape;
    public Vector3 HitCenter => _hitCenter;
    public Vector3 HitSize => _hitSize;
    public float HitRadius => _hitRadius;
    public float HitLength => _hitLength;
    public float EnergyGain => _energyGain;
    public float UltimateGaugeGain => _ultimateGaugeGain;
    public float Power => _power;
    public int StaggerLevel => _staggerLevel;
    public float StaggerTime => _staggerTime;
    public float AdvanceDistance => _advanceDistance;
    public float AdvanceDuration => _advanceDuration;
    public float FadeTime => _fadeTime;
    public float InputBuffer => _inputBuffer;
    public bool UseLegRun => _useLegRun;
    public bool AlwaysLegRun => _alwaysLegRun;
    public bool CanDashCancel => _canDashCancel;
    public bool CanUltimateCancel => _canUltimateCancel;
    public bool IsInvincibleOnSwing => _isInvincibleOnSwing;
}

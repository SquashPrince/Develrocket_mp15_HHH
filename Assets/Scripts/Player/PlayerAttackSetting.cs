using System;
using UnityEngine;

// 공격 하나의 설정 묶음. 콤보 단계 · 강공격 · 스킬 · 특수스킬이 같은 형태로 Inspector 에서 값을 정한다.
// MonoBehaviour 가 아닌 일반 클래스이므로 [Serializable] 을 붙여야 Inspector 에 펼쳐서 보인다.
[Serializable]
public class PlayerAttackSetting
{
    [Tooltip("적중한 적 하나마다 얻는 에너지")]
    [SerializeField, Min(0f)] private float _energyGain = 5f;
    [Tooltip("적중 시 위력. 적 체력이 생기기 전까지는 로그로만 확인한다")]
    [SerializeField, Min(0f)] private float _power = 10f;
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
    [Tooltip("휘두르는 동안 적 공격에 맞지 않는지 여부. 회피하지 않아도 무적이며, 후딜에 들어가면 풀린다. 무적 중 들어온 노란 공격은 패링되지 않는다")]
    [SerializeField] private bool _isInvincibleOnSwing;

    public float EnergyGain => _energyGain;
    public float Power => _power;
    public float AdvanceDistance => _advanceDistance;
    public float AdvanceDuration => _advanceDuration;
    public float FadeTime => _fadeTime;
    public float InputBuffer => _inputBuffer;
    public bool UseLegRun => _useLegRun;
    public bool AlwaysLegRun => _alwaysLegRun;
    public bool CanDashCancel => _canDashCancel;
    public bool IsInvincibleOnSwing => _isInvincibleOnSwing;
}

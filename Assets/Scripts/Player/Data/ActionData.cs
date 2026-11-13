using UnityEngine;

// 액션 하나(콤보 단계 · 강공격 · 스킬 · 특수스킬 · 대시 공격 · 회피 반격 · 패링 추격 · 필살기 · 콤보 스킬)의 값.
// 액션마다 에셋 하나를 만들고, 캐릭터는 자기 액션 에셋을 참조한다. 같은 값을 여러 캐릭터 · 여러 자리에서 다시 쓸 수 있다.
// 액션은 동작 목록을 가진다. 1개면 한 동작만, 여러 개면 순서대로 이어 재생한다. (예: 필살기 난무 2동작)
// 동작마다 재생할 상태 · 판정 범위 · 전진이 다르고, 위력 · 경직 · 게이지 · 캔슬 · 무적 같은 타격 값은 액션 공통이다.
// 일부 액션에만 붙는 기능은 추가 옵션(옵션 에셋 참조)으로 붙인다. 비워 두면 그 기능을 쓰지 않는다. (예: 순간 이동)
// 카메라 워크 · Timeline 같은 연출 참조와 시네마틱 이동(경로 이동)은 연출 작업 때 이 에셋에 추가한다.
[CreateAssetMenu(fileName = "NewActionData", menuName = "HHH/Action Data")]
public class ActionData : ScriptableObject
{
    // 액션 종류. Inspector 에 저장된 값은 순서 번호이므로, 새 종류는 항상 맨 뒤에 추가한다.
    public enum ActionType
    {
        Basic,
        Heavy,
        DashAttack,
        DodgeCounter,
        ParryChase,
        Skill,
        SpecialSkill,
        Ultimate,
        ComboSkill
    }

    [Tooltip("[예정] 액션 종류. 공격 종류에 따라 대미지를 다르게 계산할 때 쓴다. 지금은 동작에 쓰지 않는다")]
    [SerializeField] private ActionType _actionType = ActionType.Basic;

    [Tooltip("이 액션을 이루는 동작 목록. 1개면 한 동작, 여러 개면 순서대로 이어 재생한다")]
    [SerializeField] private ActionStep[] _steps = { new ActionStep() };

    [Header("타격 값")]
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

    [Header("비용")]
    [Tooltip("이 액션을 쓸 때 드는 에너지. 지금은 특수스킬만 쓴다. 스킬 입력 시 에너지가 이보다 적으면 일반 스킬이 나간다")]
    [SerializeField, Min(0f)] private float _energyCost;

    [Header("입력 · 캔슬")]
    [Tooltip("[강공격만 사용] 공격 키를 누르고 있으면 이 시간 뒤 강공격이 나간다")]
    [SerializeField, Min(0.01f)] private float _chargeTime = 0.6f;
    [Tooltip("[콤보 단계만 사용] 마지막 동작이 끝난 뒤 이 시간 안에 공격하면 다음 콤보 단계로 이어진다")]
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

    [Header("그로기")]
    [Tooltip("[예정] 적중할 때(패링 액션은 패링 성공할 때) 적에게 쌓는 그로기 값. 적 그로기 게이지를 만들 때 쓴다. 지금은 쓰지 않는다")]
    [SerializeField, Min(0f)] private float _groggyGain;

    [Header("추가 옵션")]
    [Tooltip("액션을 시작할 때 대상 앞으로 순간 이동하는 옵션. 비워 두면 순간 이동하지 않는다")]
    [SerializeField] private TeleportOption _teleport;

    public ActionType Type => _actionType;
    public int StepCount => _steps.Length;
    public float EnergyGain => _energyGain;
    public float UltimateGaugeGain => _ultimateGaugeGain;
    public float Power => _power;
    public int StaggerLevel => _staggerLevel;
    public float StaggerTime => _staggerTime;
    public float EnergyCost => _energyCost;
    public float ChargeTime => _chargeTime;
    public float InputBuffer => _inputBuffer;
    public bool UseLegRun => _useLegRun;
    public bool AlwaysLegRun => _alwaysLegRun;
    public bool CanDashCancel => _canDashCancel;
    public bool CanUltimateCancel => _canUltimateCancel;
    public bool IsInvincibleOnSwing => _isInvincibleOnSwing;
    public float GroggyGain => _groggyGain;
    public TeleportOption Teleport => _teleport;

    // index : 동작 번호(0부터)
    public ActionStep GetStep(int index) => _steps[index];
}

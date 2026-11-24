using UnityEngine;

// 캐릭터마다 다른 능력치 · 이동 · 대시 수치와 쓰는 액션. 모든 캐릭터가 같은 규칙은 CommonCharacterSettings 에,
// 액션 하나에만 쓰는 값은 그 ActionData 에 둔다.
[CreateAssetMenu(fileName = "NewCharacterData", menuName = "HHH/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("표시")]
    [Tooltip("캐릭터 이름. 전투 화면의 캐릭터 칸 이름표에 보인다. 비워 두면 에셋 이름을 쓴다")]
    [SerializeField] private string _displayName;
    [Tooltip("화면(파티 HUD · 콤보 선택)에 보일 캐릭터 초상화. 상반신 이미지를 두고, 칸 모양 마스크 안에서 위치 · 크기로 구도를 맞춘다")]
    [SerializeField] private Sprite _portrait;
    [Tooltip("캐릭터 상징색. 전투 화면의 캐릭터 칸 틀 · 이름표 · 체력 막대 등에 곱해 누구 칸인지 한눈에 보이게 한다")]
    [SerializeField] private Color _themeColor = Color.white;

    [Header("능력치")]
    [SerializeField, Min(1f)] private float _maxHealth = 100f;
    [SerializeField, Min(0f)] private float _maxEnergy = 120f;
    [Tooltip("시간이 지나며 초당 쌓이는 에너지")]
    [SerializeField, Min(0f)] private float _energyRegenPerSecond = 1f;
    [SerializeField, Min(1f)] private float _maxUltimateGauge = 3000f;
    [Tooltip("패링 성공 한 번에 얻는 필살기 게이지")]
    [SerializeField, Min(0f)] private float _parryGaugeGain = 300f;
    [Tooltip("완벽 회피 한 번에 얻는 필살기 게이지")]
    [SerializeField, Min(0f)] private float _perfectDodgeGaugeGain = 300f;

    [Header("대미지")]
    [Tooltip("대미지 흔들림 비율. 0.1 이면 액션 위력의 90% ~ 110% 사이에서 무작위로 정한다. 0 이면 항상 위력 그대로")]
    [SerializeField, Range(0f, 1f)] private float _damageVariance = 0.1f;
    [Tooltip("크리티컬 확률. 0.1 이면 10%")]
    [SerializeField, Range(0f, 1f)] private float _criticalChance = 0.1f;
    [Tooltip("크리티컬일 때 대미지 배율. 흔들림을 적용한 대미지에 곱한다")]
    [SerializeField, Min(1f)] private float _criticalMultiplier = 1.5f;

    [Header("이동")]
    [SerializeField, Min(0f)] private float _walkSpeed = 4f;
    [SerializeField, Min(0f)] private float _runSpeed = 7f;
    [Tooltip("목표 방향으로 몸을 돌리는 데 걸리는 시간(초). 이동은 캐릭터가 보는 방향으로 하므로 곡선을 그리며 꺾는다")]
    [SerializeField, Min(0f)] private float _turnTime = 0.3f;
    [Tooltip("이동 입력을 이 시간 이상 유지하면 달리기로 전환")]
    [SerializeField, Min(0f)] private float _autoRunDelay = 1.5f;

    [Header("대시 · 회피")]
    [SerializeField, Min(0f)] private float _dashDistance = 4f;
    [SerializeField, Min(0.01f)] private float _dashDuration = 0.2f;
    [SerializeField, Min(0f)] private float _dodgeDistance = 3f;
    [SerializeField, Min(0.01f)] private float _dodgeDuration = 0.2f;
    [Tooltip("대시 스택 1개가 차는 시간")]
    [SerializeField, Min(0f)] private float _dashCooldown = 1f;
    [Tooltip("대시 · 회피 시작 후 다음 대시를 쓸 수 있기까지의 시간. 지속시간보다 길게 설정하면 지속시간을 기준으로 한다")]
    [SerializeField, Min(0f)] private float _dashChainInterval = 0.2f;
    [Tooltip("대시 · 회피 시작부터 적 공격에 맞지 않는 시간(초). 이 시간 안에 닿은 공격은 완벽 회피가 된다. 대시와 회피가 같은 값을 쓴다")]
    [SerializeField, Min(0f)] private float _dashInvincibleTime = 0.15f;

    [Header("일반 공격")]
    [Tooltip("콤보 단계별 액션. 배열 크기가 콤보 단수다. 마지막 단계는 마무리 공격으로, 후딜이 끝날 때까지 공격 입력을 받지 않는다")]
    [SerializeField] private ActionData[] _comboSteps;
    [Tooltip("공격 키를 누르고 있으면 나가는 강공격. 차지 시간은 이 액션의 값을 쓴다")]
    [SerializeField] private ActionData _heavyAttack;

    [Header("회피 공격")]
    [Tooltip("대시 · 회피 도중 공격 입력으로 나가는 공격")]
    [SerializeField] private ActionData _dashAttack;
    [Tooltip("완벽 회피 슬로모션 중 공격 입력으로 나가는 반격")]
    [SerializeField] private ActionData _dodgeCounter;

    [Header("교체 · 패링")]
    [Tooltip("패링(준비 · 반응) 액션. 동작 0 번이 준비, 1 번이 반응 모션이다. 순간 이동 옵션과 그로기 값도 이 액션에서 읽는다")]
    [SerializeField] private ActionData _parry;
    [Tooltip("패링 반응 중 공격 입력으로 나가는 추격 공격. 시전 중 무적을 켜 두어야 기획대로 추격 중 무적이 된다")]
    [SerializeField] private ActionData _parryChase;
    [Tooltip("빠른 지원 액션. 이전 캐릭터가 넉백된 뒤 입력 시간 안에 교체하면 넉백시킨 적 앞으로 순간 이동해 쓴다. 순간 이동 옵션(대상 정면)과 시전 중 무적을 켜 둔다. 비워 두면 보통 교체로 들어온다")]
    [SerializeField] private ActionData _quickSupport;
    [Tooltip("입장 때 캐릭터 클립으로 덮을 부위. 입장은 모든 캐릭터가 같은 공용 모션을 재생하고, 고른 부위만 Override 에 넣은 이 캐릭터의 클립으로 덮는다. 없음이면 공용 모션만, 전신이면 하체 · 상체 모두 덮는다")]
    [SerializeField] private PlayerAnimation.BodyPart _swapInBodyPart = PlayerAnimation.BodyPart.None;
    [Tooltip("퇴장 때 캐릭터 클립으로 덮을 부위. 없음이면 공용 모션만, 전신이면 하체 · 상체 모두 덮는다")]
    [SerializeField] private PlayerAnimation.BodyPart _swapOutBodyPart = PlayerAnimation.BodyPart.None;

    [Header("스킬")]
    [SerializeField] private ActionData _skill;
    [Tooltip("에너지가 이 액션의 에너지 비용 이상이면 스킬 입력 시 일반 스킬 대신 나간다")]
    [SerializeField] private ActionData _specialSkill;

    [Header("필살기")]
    [Tooltip("연출 뒤 이어지는 필살기 액션. 동작 목록 순서대로 이어 재생한다. 하체 달리기 · 대시 캔슬 · 입력 버퍼 값은 쓰지 않는다")]
    [SerializeField] private ActionData _ultimate;

    [Header("콤보 스킬")]
    [Tooltip("한 번의 콤보 스킬 액션. 동작 목록 순서대로 이어 재생한다. 동작 수와 연계 수는 관계없다. 하체 달리기 · 대시 캔슬 · 입력 버퍼 · 필살기로 끊기 값은 쓰지 않는다")]
    [SerializeField] private ActionData _comboSkill;

    public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
    public Sprite Portrait => _portrait;
    public Color ThemeColor => _themeColor;
    public float MaxHealth => _maxHealth;
    public float MaxEnergy => _maxEnergy;
    public float EnergyRegenPerSecond => _energyRegenPerSecond;
    public float MaxUltimateGauge => _maxUltimateGauge;
    public float ParryGaugeGain => _parryGaugeGain;
    public float PerfectDodgeGaugeGain => _perfectDodgeGaugeGain;
    public float WalkSpeed => _walkSpeed;
    public float RunSpeed => _runSpeed;
    public float TurnTime => _turnTime;
    public float AutoRunDelay => _autoRunDelay;
    public float DashDistance => _dashDistance;
    public float DashDuration => _dashDuration;
    public float DodgeDistance => _dodgeDistance;
    public float DodgeDuration => _dodgeDuration;
    public float DashCooldown => _dashCooldown;
    public float DashChainInterval => _dashChainInterval;
    public float DashInvincibleTime => _dashInvincibleTime;
    public int ComboStepCount => _comboSteps.Length;
    public ActionData HeavyAttack => _heavyAttack;
    public ActionData DashAttack => _dashAttack;
    public ActionData DodgeCounter => _dodgeCounter;
    public ActionData Parry => _parry;
    public ActionData ParryChase => _parryChase;
    public ActionData QuickSupport => _quickSupport;
    public PlayerAnimation.BodyPart SwapInBodyPart => _swapInBodyPart;
    public PlayerAnimation.BodyPart SwapOutBodyPart => _swapOutBodyPart;
    public ActionData Skill => _skill;
    public ActionData SpecialSkill => _specialSkill;
    public ActionData Ultimate => _ultimate;
    public ActionData ComboSkill => _comboSkill;

    // step : 콤보 단계(0부터)
    public ActionData GetComboStep(int step) => _comboSteps[step];

    // 액션 위력에 이 캐릭터의 대미지 흔들림과 크리티컬을 적용한 대미지를 뽑는다. 부를 때마다 새로 뽑는다.
    public float RollDamage(float power, out bool isCritical)
    {
        float damage = power * Random.Range(1f - _damageVariance, 1f + _damageVariance);

        isCritical = Random.value < _criticalChance;

        return isCritical ? damage * _criticalMultiplier : damage;
    }
}

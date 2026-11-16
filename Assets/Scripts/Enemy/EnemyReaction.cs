using System;
using UnityEngine;

// 적의 경직 · 그로기. 경직 · 그로기 시간은 적 전용 시간으로 흘러 슬로모션 · 필살기 연출 동안 느려지거나 멈춘다.
// 경직은 경직을 거는 쪽의 경직 레벨이 지금의 경직 저항 레벨 이상일 때만 걸린다.
// 지금의 경직 저항 레벨은 평소 저항 레벨과, 공격 중이면 그 공격의 저항 레벨 중 큰 값이다.
// 그로기 중에는 경직이 걸려도 그로기를 유지한다. 그로기가 될 때마다 콤보 스킬 기회가 한 번 생긴다.
// 그로기 시간 정지 : 플레이어 쪽이 콤보 스킬 흐름에 맞춰 정해진 시간 동안 멈추거나(정지 시간), 캐릭터 선택 동안 멈춘다.
//   멈춘 동안 남은 그로기 시간은 그대로이고, 다시 흐를 때 이어서 줄어든다.
// 그로기 게이지 : 플레이어 공격 적중 · 패링 성공으로 쌓이고, 최대치에 이르면 그로기가 된다. 시간이 지나도 줄지 않는다.
//   그로기 중에는 더 쌓이지 않고, 그로기가 끝나면 0 부터 다시 쌓는다.
// 적의 공격 기능은 알림을 받아 진행 중인 공격을 끊고, 경직 · 그로기 동안에는 공격하지 않는다.
// 적 체력과 같은 오브젝트(루트)에 붙인다.
public class EnemyReaction : MonoBehaviour, IEnemyStaggerable, IEnemyGroggyable
{
    private enum State
    {
        None,
        Stagger,
        Groggy
    }

    [Tooltip("평소 경직 저항 레벨. 경직을 거는 쪽의 경직 레벨이 이 값 이상이어야 경직된다")]
    [SerializeField, Min(0)] private int _baseStaggerResistLevel;
    [Tooltip("그로기 게이지 최대치. 게이지가 이 값에 이르면 그로기가 된다")]
    [SerializeField, Min(1f)] private float _maxGroggy = 100f;
    [Tooltip("그로기가 유지되는 시간(초, 적 전용 시간)")]
    [SerializeField, Min(0f)] private float _groggyTime = 12f;
    [Tooltip("이 적에게 콤보 스킬이 이어지는 최대 연계 수. 일반 1, 엘리트 2")]
    [SerializeField, Min(1)] private int _maxComboChain = 1;

    [Header("[확인용] 현재 값")]
    [Tooltip("현재 그로기 게이지. 실행 중 확인용이며, 시작할 때 0 으로 정한다")]
    [SerializeField] private float _currentGroggy;

    private State _state;
    private float _timer;
    // 그로기 시간이 줄지 않는 남은 시간. 플레이어 쪽이 콤보 스킬 흐름에 맞춰 건다.
    private float _groggyHoldTimer;
    // 진행 중인 공격의 경직 저항 레벨. 공격 기능이 공격을 시작할 때 정하고, 공격이 끝나거나 끊기면 0 으로 돌린다.
    private int _attackStaggerResistLevel;
    // 이번 그로기에서 콤보 스킬을 아직 발동하지 않았는지. 그로기가 될 때마다 한 번 생긴다.
    private bool _hasComboSkillChance;
    // 이번 그로기에서 남은 연계 수. 그로기가 될 때마다 최대 연계 수로 채우고, 콤보 스킬 시전 · 캔슬마다 1 씩 준다.
    private int _remainingComboChain;
    // 콤보 스킬 캐릭터 선택 동안 그로기 시간을 멈추는지
    private bool _isGroggyPaused;

    public bool IsGroggy => _state == State.Groggy;
    public bool HasComboSkillChance => IsGroggy && _hasComboSkillChance;
    public bool HasRemainingComboChain => IsGroggy && _remainingComboChain > 0;
    // 경직 · 그로기 중인지. 이 동안 적은 공격하지 않는다.
    public bool IsRestricted => _state != State.None;
    public float CurrentGroggy => _currentGroggy;
    public float MaxGroggy => _maxGroggy;
    // 그로기 중 남은 그로기 시간의 비율(1 → 0). 정지 중에는 줄지 않는다. 그로기가 아니면 0 이다.
    public float GroggyTimeRatio => IsGroggy && _groggyTime > 0f ? Mathf.Clamp01(_timer / _groggyTime) : 0f;

    public event Action Staggered;
    public event Action GroggyEntered;
    // 경직 · 그로기가 끝났을 때 알린다.
    public event Action Recovered;
    // 그로기 게이지가 바뀌면 알린다. 쌓일 때와, 그로기가 끝나 0 이 될 때다.
    public event Action GroggyGaugeChanged;

    // 확인용 그로기 게이지는 다른 오브젝트를 참조하지 않으므로 Awake 에서 정해, 화면이 시작 단계에서 읽어도 맞게 한다.
    private void Awake() => Init();
    private void Update() => UpdateTimer();

    // 경직한다. 경직 중 다시 걸면 경직 시간을 처음부터 센다.
    // 경직 레벨이 지금의 경직 저항 레벨보다 낮으면 경직하지 않는다.
    // 그로기 중에는 이미 공격을 멈춘 상태이므로 경직하지 않고 그로기를 유지한다.
    public void Stagger(float duration, int staggerLevel)
    {
        if(IsGroggy || staggerLevel < Mathf.Max(_baseStaggerResistLevel, _attackStaggerResistLevel)) return;

        _timer = duration;
        _state = State.Stagger;

        Staggered?.Invoke();
    }

    // 공격 기능이 공격을 시작할 때 그 공격의 경직 저항 레벨로, 공격이 끝나거나 끊기면 0 으로 정한다.
    public void SetAttackStaggerResistLevel(int level) => _attackStaggerResistLevel = level;

    // 그로기 게이지를 쌓고, 최대치에 이르면 그로기가 된다. 그로기 중에는 쌓지 않는다.
    // 이번에 그로기가 되었으면 true 를 돌려준다. 플레이어 쪽이 그로기 진입 순간의 시간 정지를 걸 때 쓴다.
    public bool AddGroggy(float amount)
    {
        if(IsGroggy || amount <= 0f) return false;

        _currentGroggy = Mathf.Min(_currentGroggy + amount, _maxGroggy);
        GroggyGaugeChanged?.Invoke();

        if(_currentGroggy < _maxGroggy) return false;

        EnterGroggy();
        return true;
    }

    // 그로기 중일 때만 건다. 멈춰 있던 남은 시간은 버리고 duration 부터 다시 센다.
    public void HoldGroggy(float duration)
    {
        if(IsGroggy) _groggyHoldTimer = duration;
    }

    public void SetGroggyPaused(bool isPaused) => _isGroggyPaused = isPaused;

    public bool TryConsumeComboSkillChance()
    {
        if(!IsGroggy || !_hasComboSkillChance) return false;

        _hasComboSkillChance = false;
        return true;
    }

    // 그로기 중일 때만 남은 연계를 1 회 쓴다. 남은 연계가 없으면 그대로 둔다.
    public void UseComboChain()
    {
        if(HasRemainingComboChain) _remainingComboChain--;
    }

    // 이번 그로기의 콤보 스킬 기회와 남은 연계를 모두 없앤다.
    public void ClearComboChain()
    {
        _hasComboSkillChance = false;
        _remainingComboChain = 0;
    }

    // 그로기가 된다. 그로기마다 콤보 스킬 기회가 한 번 생기고, 남은 연계 수를 최대 연계 수로 채운다.
    private void EnterGroggy()
    {
        _timer = _groggyTime;
        _state = State.Groggy;
        _hasComboSkillChance = true;
        _remainingComboChain = _maxComboChain;
        _groggyHoldTimer = 0f;
        _isGroggyPaused = false;

#if UNITY_EDITOR
        Debug.Log($"[적 그로기] {name} {_groggyTime}초");
#endif

        GroggyEntered?.Invoke();
    }

    // 그로기는 선택 중 멈춤 → 정지 시간 → 남은 시간 순서로 줄인다. 정지 시간이 끝난 뒤에만 남은 그로기 시간이 줄어든다.
    private void UpdateTimer()
    {
        if(_state == State.None) return;

        if(IsGroggy && _isGroggyPaused) return;

        if(IsGroggy && _groggyHoldTimer > 0f)
        {
            _groggyHoldTimer -= EnemyTime.DeltaTime;
            return;
        }

        _timer -= EnemyTime.DeltaTime;

        if(_timer > 0f) return;

        // 그로기가 끝나면 게이지를 0 부터 다시 쌓는다.
        if(IsGroggy)
        {
            _currentGroggy = 0f;
            GroggyGaugeChanged?.Invoke();
        }

        _state = State.None;
        Recovered?.Invoke();
    }

    private void Init()
    {
        _currentGroggy = 0f;
    }
}

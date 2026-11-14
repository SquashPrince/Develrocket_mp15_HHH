using System;
using UnityEngine;

// 적의 경직 · 그로기. 경직 · 그로기 시간은 적 전용 시간으로 흘러 슬로모션 · 필살기 연출 동안 느려지거나 멈춘다.
// 경직은 경직을 거는 쪽의 경직 레벨이 지금의 경직 저항 레벨 이상일 때만 걸린다.
// 지금의 경직 저항 레벨은 평소 저항 레벨과, 공격 중이면 그 공격의 저항 레벨 중 큰 값이다.
// 그로기 중에는 경직이 걸려도 그로기를 유지한다. 그로기가 될 때마다 콤보 스킬 기회가 한 번 생긴다.
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
    // 진행 중인 공격의 경직 저항 레벨. 공격 기능이 공격을 시작할 때 정하고, 공격이 끝나거나 끊기면 0 으로 돌린다.
    private int _attackStaggerResistLevel;
    // 이번 그로기에서 콤보 스킬을 아직 발동하지 않았는지. 그로기가 될 때마다 한 번 생긴다.
    private bool _hasComboSkillChance;

    public bool IsGroggy => _state == State.Groggy;
    // 경직 · 그로기 중인지. 이 동안 적은 공격하지 않는다.
    public bool IsRestricted => _state != State.None;
    public int MaxComboChain => _maxComboChain;

    public event Action Staggered;
    public event Action GroggyEntered;
    // 경직 · 그로기가 끝났을 때 알린다.
    public event Action Recovered;

    private void Start() => Init();
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
    public void AddGroggy(float amount)
    {
        if(IsGroggy || amount <= 0f) return;

        _currentGroggy = Mathf.Min(_currentGroggy + amount, _maxGroggy);

        if(_currentGroggy >= _maxGroggy) EnterGroggy();
    }

    public bool TryConsumeComboSkillChance()
    {
        if(!IsGroggy || !_hasComboSkillChance) return false;

        _hasComboSkillChance = false;
        return true;
    }

    // 그로기가 된다. 그로기마다 콤보 스킬 기회가 한 번 생긴다.
    private void EnterGroggy()
    {
        _timer = _groggyTime;
        _state = State.Groggy;
        _hasComboSkillChance = true;

#if UNITY_EDITOR
        Debug.Log($"[적 그로기] {name} {_groggyTime}초");
#endif

        GroggyEntered?.Invoke();
    }

    private void UpdateTimer()
    {
        if(_state == State.None) return;

        _timer -= EnemyTime.DeltaTime;

        if(_timer > 0f) return;

        // 그로기가 끝나면 게이지를 0 부터 다시 쌓는다.
        if(IsGroggy) _currentGroggy = 0f;

        _state = State.None;
        Recovered?.Invoke();
    }

    private void Init()
    {
        _currentGroggy = 0f;
    }
}

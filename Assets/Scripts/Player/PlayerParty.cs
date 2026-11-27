using System;
using System.Collections.Generic;
using UnityEngine;

// 파티 캐릭터 목록 · 공통 설정을 들고, 조작 캐릭터 교체(지원 공격 · 빠른 지원 · 콤보 스킬 이어받기)와 쓰러짐 · 전멸을 맡는다.
// 슬로모션 · 록온처럼 플레이어 전체에 하나뿐인 기능은 파티에 두고 캐릭터가 꺼내 쓴다.
// 자세한 흐름 : Docs/Technical/Player/PlayerParty.md
[RequireComponent(typeof(BattleSlowMotion), typeof(PlayerLockOn), typeof(ParryTelegraphView))]
[RequireComponent(typeof(ComboSkillSelector))]
public class PlayerParty : MonoBehaviour
{
    private const int PREVIOUS = -1;
    private const int NEXT = 1;

    [Tooltip("파티 캐릭터 목록. 씬에 놓인 캐릭터를 넣는다. 이 순서대로 교체하고, 첫 번째 캐릭터를 조작하며 시작한다")]
    [SerializeField] private PlayerController[] _members;
    [Tooltip("모든 캐릭터가 함께 쓰는 규칙 값 · 입력")]
    [SerializeField] private CommonCharacterSettings _common;
    [Tooltip("이동 방향 · 화면 중앙 대상 찾기의 기준이 되는 카메라 (Main Camera)")]
    [SerializeField] private Camera _mainCamera;

    // 적마다 예고가 뜬 순간 정한 패링 가능 여부
    private readonly Dictionary<IEnemyParryable, bool> _parryableAtOpen = new Dictionary<IEnemyParryable, bool>();
    // 예고 하나에 캐릭터 하나만 패링하게, 이미 패링을 시작한 적을 기록한다.
    private readonly HashSet<IEnemyParryable> _claimedParryAttackers = new HashSet<IEnemyParryable>();
    // 빠른 지원 : 넉백된 캐릭터와 넉백시킨 적
    private PlayerController _quickSupportSource;
    private Transform _quickSupportTarget;
    private int _controlledIndex;
    private float _quickSupportTimer;
    // 실제 시간 기준
    private float _lastSwapTime = float.NegativeInfinity;
    private bool _isStandbyReady;
    private bool _isInputLocked;

    public CommonCharacterSettings Common => _common;
    public Camera MainCamera => _mainCamera;
    public PlayerController Controlled => _members[_controlledIndex];
    public IReadOnlyList<PlayerController> Members => _members;
    public BattleSlowMotion SlowMotion { get; private set; }
    public PlayerLockOn LockOn { get; private set; }
    // 없으면 지원 공격을 할 수 없어 적의 패링 공격 예고가 패링 불가로 뜬다.
    public bool HasStandby => FindStandbyIndex(NEXT) >= 0;
    // 필드에 나와 있는 파티원(연속 패링 · 퇴장 중)도 콤보 스킬을 이어받을 수 있다.
    public bool HasComboMember => CountAliveMembers() > 1;
    // 연속 패링 패턴 중에는 필드의 파티원이 다시 패링한다. 살아 있는 캐릭터가 혼자면 패링할 수 없다.
    public bool CanParry => HasStandby || ParryWindow.IsChainActive && CountAliveMembers() > 1;
    public ComboSkillSelector ComboSelector { get; private set; }
    public bool IsInputBlocked => _isInputLocked || GamePause.IsPaused || ComboSelector.IsBlockingInput;
    // 끝난 뒤에는 파티가 적의 공격에 맞지 않는다.
    public bool IsBattleEnded { get; private set; }

    // 시작할 때도 첫 캐릭터로 한 번 알린다.
    public event Action<PlayerController> ControlledChanged;
    // 결과 화면으로 넘어가며 전투가 끝나면 알린다.
    public event Action BattleEnded;
    // 마지막 살아 있는 캐릭터의 쓰러짐 모션이 끝나면 알린다.
    public event Action Wiped;

    // 캐릭터 기능이 Start 에서 공통 설정을 읽으므로 Awake 에서 파티를 알려 준다.
    private void Awake()
    {
        CacheComponent();
        JoinMembers();
    }
    // 입력 기능이 각 캐릭터의 Awake 에서 준비되므로 Start 에서 조작 캐릭터를 정한다.
    private void Start() => Init();
    private void OnEnable() => BindParryWindowEvent();
    private void OnDisable() => UnbindParryWindowEvent();
    private void Update()
    {
        HideStandbyOnce();
        UpdateQuickSupportTimer();
        RegenerateStandbyEnergy();
        ReadSwapInput();
    }

    // 예고 색과 실제 판정을 맞추려고 예고가 뜬 순간의 판단을 그대로 쓴다.
    // 같은 알림을 받는 기능 사이의 순서 때문에 아직 기록 전이면 지금 판단해 기록한다.
    public bool IsParryable(IEnemyParryable attacker)
    {
        if(!_parryableAtOpen.TryGetValue(attacker, out bool isParryable))
        {
            isParryable = CanParry;
            _parryableAtOpen[attacker] = isParryable;
        }

        return isParryable;
    }

    // 교체 키를 연타해도 예고 하나에 캐릭터 하나만 패링하게 한다.
    public bool CanStartParry(IEnemyParryable attacker) => IsParryable(attacker) && !_claimedParryAttackers.Contains(attacker);

    public void ClaimParry(IEnemyParryable attacker) => _claimedParryAttackers.Add(attacker);

    // 퇴장 중인 이전 캐릭터의 넉백은 빠른 지원을 열지 않는다.
    public void OpenQuickSupport(PlayerController knockedCharacter, Transform attacker)
    {
        if(knockedCharacter != Controlled) return;

        _quickSupportSource = knockedCharacter;
        _quickSupportTarget = attacker;
        _quickSupportTimer = _common.QuickSupportInputTime;
    }

    // 필살기 연출 동안 시전자를 뺀 파티원을 멈춘다. 필드에 없는 캐릭터에 걸어도 영향이 없다.
    public void SetOthersFrozen(PlayerController caster, bool isFrozen)
    {
        for(int i = 0; i < _members.Length; i++)
        {
            if(_members[i] != caster) _members[i].Status.SetFrozen(isFrozen);
        }
    }

    // 대기 캐릭터도 교체해 들어왔을 때 필살기를 쓸 수 있게 게이지 일부를 나눠 준다.
    public void ShareUltimateGauge(PlayerController giver, float amount)
    {
        float shared = amount * Common.UltimateGaugeShareRatio;

        for(int i = 0; i < _members.Length; i++)
        {
            if(_members[i] != giver) _members[i].Status.ReceiveSharedUltimateGauge(shared);
        }
    }

    // 패링 성공 때 다른 적의 공격에 맞지 않게 한다. 꺼진 대기 캐릭터는 시간이 흐르지 않아 무적이 남으므로 뺀다.
    public void BeginFieldInvincible(float duration)
    {
        for(int i = 0; i < _members.Length; i++)
        {
            if(_members[i].gameObject.activeSelf) _members[i].Status.BeginInvincible(duration);
        }
    }

    // 결과 화면 클릭이 공격 입력으로 들어가지 않게 입력을 막는다.
    public void EndBattle()
    {
        _isInputLocked = true;
        IsBattleEnded = true;
        BattleEnded?.Invoke();
    }

    // CharacterController 가 켜져 있으면 위치를 덮어쓰므로 잠깐 끄고 옮긴다.
    public void PlaceAt(Transform point)
    {
        for(int i = 0; i < _members.Length; i++)
        {
            CharacterController body = _members[i].Body;

            body.enabled = false;
            _members[i].transform.SetPositionAndRotation(point.position, point.rotation);
            body.enabled = true;
        }
    }

    // 쓰러짐 모션이 끝나면 부른다. 넘길 캐릭터가 없으면 쓰러진 모습 그대로 두고 전멸을 알린다.
    public void OnMemberDowned(PlayerController member)
    {
        if(member != Controlled)
        {
            member.Standby();
            return;
        }

        int standbyIndex = FindStandbyIndex(NEXT);

        if(standbyIndex >= 0)
        {
            PlayerController next = _members[standbyIndex];

            member.Standby();
            PlaceNext(member, next);
            next.SwapIn();
            SetControlled(standbyIndex);
            return;
        }

        int fieldIndex = FindAliveIndex(NEXT);

        if(fieldIndex >= 0)
        {
            member.Standby();
            _members[fieldIndex].CancelSwapOut();
            SetControlled(fieldIndex);
            return;
        }

#if UNITY_EDITOR
        Debug.Log("[파티 전멸]");
#endif

        Wiped?.Invoke();
    }

    private void JoinMembers()
    {
        for(int i = 0; i < _members.Length; i++)
        {
            _members[i].JoinParty(this);
        }
    }

    // 캐릭터는 Start 에서 상태를 만들므로, 모든 Start 가 끝난 첫 Update 에서 대기 캐릭터를 끈다.
    private void HideStandbyOnce()
    {
        if(_isStandbyReady) return;

        _isStandbyReady = true;

        for(int i = 0; i < _members.Length; i++)
        {
            if(i != _controlledIndex) _members[i].Standby();
        }
    }

    // 대기 캐릭터는 꺼져 있어 스스로 회복하지 못하므로 파티가 실제 시간으로 채운다.
    private void RegenerateStandbyEnergy()
    {
        float rate = _common.StandbyEnergyRegenRate * Time.deltaTime;

        for(int i = 0; i < _members.Length; i++)
        {
            PlayerController member = _members[i];

            if(member.gameObject.activeSelf || member.Status.IsDowned) continue;

            member.Status.AddEnergy(member.Character.EnergyRegenPerSecond * rate);
        }
    }

    // 히트렉 · 슬로모션과 상관없이 실제 시간으로 잰다.
    private void UpdateQuickSupportTimer()
    {
        if(_quickSupportTimer <= 0f) return;

        _quickSupportTimer -= Time.deltaTime;

        if(_quickSupportTimer <= 0f) ClearQuickSupport();
    }

    private void ClearQuickSupport()
    {
        _quickSupportSource = null;
        _quickSupportTarget = null;
        _quickSupportTimer = 0f;
    }

    private void ReadSwapInput()
    {
        if(IsInputBlocked) return;

        if(_common.ParryPreviousAction.action.WasPressedThisFrame()) TrySwap(PREVIOUS);
        else if(_common.ParryNextAction.action.WasPressedThisFrame()) TrySwap(NEXT);
    }

    // 일반 교체만 최소 간격을 지킨다. 빠른 지원 · 패링이 되는 교체는 간격과 상관없다.
    private void TrySwap(int direction)
    {
        if(!Controlled.CanSwap) return;

        int nextIndex = FindStandbyIndex(direction);

        if(nextIndex < 0)
        {
            TryChainParry(direction);
            return;
        }

        if(!IsSupportSwap() && Time.unscaledTime < _lastSwapTime + _common.SwapCooldown) return;

        PlayerController previous = Controlled;
        PlayerController next = _members[nextIndex];

        PlaceNext(previous, next);
        EnterNext(previous, next);
        FinishSwap(previous, nextIndex);
    }

    // 연속 패링 패턴 중 대기 캐릭터가 없으면 필드의 파티원(퇴장 중 포함)을 다시 조작해 패링한다.
    private void TryChainParry(int direction)
    {
        if(!ParryWindow.IsChainActive) return;

        int nextIndex = FindAliveIndex(direction);

        if(nextIndex < 0) return;

        PlayerController previous = Controlled;
        PlayerController next = _members[nextIndex];

        if(!next.ActionTrigger.TryStartSupportParry()) return;

        next.CancelSwapOut();
        FinishSwap(previous, nextIndex);
    }

    // 고른 캐릭터를 이전 캐릭터 자리에 꺼내 콤보 스킬로 들여보낸다. 이미 필드에 있으면 옮기지 않고 퇴장 예약만 지운다.
    public void EnterComboSkill(PlayerController next, IEnemyTargetable target)
    {
        PlayerController previous = Controlled;

        ClearQuickSupport();

        if(next.gameObject.activeSelf)
        {
            SlowMotion.End();
            next.CancelSwapOut();
        }
        else PlaceNext(previous, next);

        next.ActionTrigger.BeginComboSkill(target);
        FinishSwap(previous, Array.IndexOf(_members, next));
    }

    // 이번 연계에서 아직 쓰지 않은 살아 있는 후보를 찾는다. 필드에 나와 있는 파티원도 후보다.
    public PlayerController FindComboCandidate(bool isNext, HashSet<PlayerController> used)
    {
        int direction = isNext ? NEXT : PREVIOUS;

        for(int step = 1; step < _members.Length; step++)
        {
            PlayerController member = _members[(_controlledIndex + direction * step + _members.Length) % _members.Length];

            if(!used.Contains(member) && !member.Status.IsDowned) return member;
        }

        return null;
    }

    // 꺼져 있는 동안 옮겨야 켜지는 순간 CharacterController 가 새 위치에서 시작한다.
    private void PlaceNext(PlayerController previous, PlayerController next)
    {
        SlowMotion.End();
        next.transform.SetPositionAndRotation(previous.transform.position, previous.transform.rotation);
        next.gameObject.SetActive(true);
    }

    private void FinishSwap(PlayerController previous, int nextIndex)
    {
        previous.ReserveSwapOut();
        SetControlled(nextIndex);
        _lastSwapTime = Time.unscaledTime;
    }

    // 들어올 캐릭터는 지금 캐릭터 자리에 나오므로 지금 캐릭터 기준으로 본다.
    private bool IsSupportSwap() => (_quickSupportTimer > 0f && _quickSupportSource == Controlled) || Controlled.ActionTrigger.HasParryTarget;

    // 우선순위 : 빠른 지원 → 지원 공격 → 보통 입장. 빠른 지원 입력 시간은 어느 쪽으로 들어오든 닫는다.
    private void EnterNext(PlayerController previous, PlayerController next)
    {
        bool canQuickSupport = _quickSupportTimer > 0f && _quickSupportSource == previous;
        Transform quickSupportTarget = _quickSupportTarget;

        ClearQuickSupport();

        if(canQuickSupport && next.TryStartQuickSupport(quickSupportTarget)) return;
        if(next.ActionTrigger.TryStartSupportParry()) return;

        next.SwapIn();
    }

    // 쓰러진 캐릭터도 꺼져 있으므로 쓰러짐 여부를 함께 본다.
    private int FindStandbyIndex(int direction)
    {
        for(int step = 1; step < _members.Length; step++)
        {
            int index = (_controlledIndex + direction * step + _members.Length) % _members.Length;

            if(!_members[index].gameObject.activeSelf && !_members[index].Status.IsDowned) return index;
        }

        return -1;
    }

    private int FindAliveIndex(int direction)
    {
        for(int step = 1; step < _members.Length; step++)
        {
            int index = (_controlledIndex + direction * step + _members.Length) % _members.Length;

            if(!_members[index].Status.IsDowned) return index;
        }

        return -1;
    }

    private int CountAliveMembers()
    {
        int count = 0;

        for(int i = 0; i < _members.Length; i++)
        {
            if(!_members[i].Status.IsDowned) count++;
        }

        return count;
    }

    private void SetControlled(int index)
    {
        _controlledIndex = index;

        for(int i = 0; i < _members.Length; i++)
        {
            _members[i].Input.IsControlled = i == index;
        }

        ControlledChanged?.Invoke(Controlled);
    }

    private void CacheComponent()
    {
        SlowMotion = GetComponent<BattleSlowMotion>();
        LockOn = GetComponent<PlayerLockOn>();
        ComboSelector = GetComponent<ComboSkillSelector>();
    }

    private void Init()
    {
        SetControlled(0);
    }

    private void BindParryWindowEvent()
    {
        ParryWindow.Opened += OnParryWindowOpened;
        ParryWindow.Closed += OnParryWindowClosed;
    }

    private void UnbindParryWindowEvent()
    {
        ParryWindow.Opened -= OnParryWindowOpened;
        ParryWindow.Closed -= OnParryWindowClosed;
        _parryableAtOpen.Clear();
        _claimedParryAttackers.Clear();
    }

    private void OnParryWindowOpened(IEnemyParryable attacker) => IsParryable(attacker);

    private void OnParryWindowClosed(IEnemyParryable attacker)
    {
        _parryableAtOpen.Remove(attacker);
        _claimedParryAttackers.Remove(attacker);
    }
}

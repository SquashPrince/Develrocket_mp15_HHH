using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;

// 파티 전투의 캐릭터 목록과 공통 설정을 들고, 조작 중인 캐릭터와 교체를 맡는다.
// 공통 설정 에셋 · 메인 카메라는 파티 하나가 들고, 각 캐릭터의 기능은 컨트롤러를 통해 꺼내 쓴다.
//   캐릭터마다 같은 에셋 · 씬 오브젝트를 연결하지 않아도 되고, 바꿀 곳이 이 파티 하나다.
// 캐릭터 하나만 있는 씬도 목록에 한 명을 넣어 같은 방식으로 쓴다. 교체할 캐릭터가 없어 교체 키는 아무 일도 하지 않는다.
// 조작 중인 캐릭터만 입력에 반응하고, 나머지 캐릭터의 입력은 끈다.
// 슬로모션 · 록온처럼 캐릭터가 아닌 플레이어 전체에 하나뿐인 기능은 파티 오브젝트에 두고, 캐릭터는 파티를 통해 꺼내 쓴다.
//
// 교체 : 이전 · 다음 캐릭터 키로 목록 순서대로 돌아가며 교체한다. (마지막 다음은 처음, 처음 이전은 마지막)
//        다음 순서의 캐릭터가 아직 필드에 있으면(퇴장 중) 건너뛰고 같은 방향의 다음 대기 캐릭터로 교체한다.
//        대기 캐릭터가 없으면(모두 필드에 있음) 교체하지 않는다. 일반 교체는 마지막 교체부터 최소 간격(실제 시간)이 지나야 하고, 빠른 지원 · 패링이 되는 교체는 간격과 상관없이 한다.
//        조작 중인 캐릭터의 지금 동작이 교체를 허용하지 않으면(액션 동작의 끼어들기 허용) 교체 키를 무시한다.
//        새 캐릭터는 이전 캐릭터 자리에서 입장하고, 이전 캐릭터는 하던 동작을 마친 뒤 퇴장한다.
//        슬로모션 중에 교체하면 슬로모션이 끝난다. 록온 대상은 그대로 이어진다.
// 지원 공격 : 적이 패링 가능 시간을 열었을 때 교체하면, 새 캐릭터가 입장 대신 바로 패링 상태로 들어가 그 적 앞으로 순간 이동한다.
//            이전 캐릭터는 보통 교체처럼 하던 동작을 마친 뒤 퇴장한다. 현재 캐릭터가 직접 패링하지는 않는다.
//            예고 하나는 캐릭터 하나만 패링한다. 이미 패링을 시작한 예고에 교체 키를 다시 누르면 패링이 아닌 일반 교체로 본다.
// 빠른 지원 : 조작 중인 캐릭터가 넉백되면 입력 시간이 열리고, 그 안에 교체하면 새 캐릭터가 넉백시킨 적 앞으로 순간 이동해 빠른 지원 액션을 쓴다.
//            넉백된 캐릭터는 일어난 뒤 퇴장한다. 지원 공격보다 먼저 본다.
// 콤보 스킬 이어받기 : 콤보 스킬 선택(같은 오브젝트)이 고른 캐릭터를 교체처럼 이전 자리에 꺼내 콤보 스킬로 들여보낸다.
//                     선택 중에는 교체 키 · 캐릭터 입력을 막는다.
// 대기 : 필드에 없는 캐릭터는 꺼 둔다. 꺼진 오브젝트는 적에게 맞거나 대상으로 잡히지 않는다.
//        대기 캐릭터의 에너지는 파티가 초당 회복량 × 대기 회복 배율로 채운다.
// 쓰러짐 : 체력이 0 이 된 캐릭터는 쓰러짐 모션이 끝나면 꺼지고, 교체 · 콤보 스킬 후보 · 대기 에너지 회복에서 빠진다.
//          조작 중인 캐릭터가 쓰러지면 다음 순서의 살아 있는 대기 캐릭터가 그 자리로 교체 입장한다.
//          대기 캐릭터가 없고 필드에 퇴장 중인 캐릭터만 살아 있으면 퇴장을 취소하고 그 캐릭터를 조작한다.
//          살아 있는 캐릭터가 없으면 전멸을 알린다. 혼자 남으면 연속 패링 패턴 중에도 패링할 수 없다.
// 적의 공격 대상 : 시작할 때와 교체할 때 등록된 적 행동 그래프의 Target 을 조작 중인 캐릭터로 바꾼다.
//   씬에 미리 놓인 적은 파티가 깨어날 때 한 번 찾아 등록하고, 실행 중 생성되는 적은 생성한 쪽이 등록한다.
//   적은 공격을 시작할 때 대상을 따로 저장하므로, 진행 중인 공격은 이전 캐릭터의 위치로 끝까지 시전하고 다음 공격부터 새 캐릭터를 노린다.
[RequireComponent(typeof(BattleSlowMotion), typeof(PlayerLockOn), typeof(ParryTelegraphView))]
[RequireComponent(typeof(ComboSkillSelector))]
public class PlayerParty : MonoBehaviour
{
    private const int PREVIOUS = -1;
    private const int NEXT = 1;
    // 적 행동 그래프의 Blackboard 에서 공격 대상을 담는 변수 이름. 일반 적 · 보스 그래프가 같은 이름을 쓴다.
    private const string ENEMY_TARGET_VARIABLE = "Target";

    [Tooltip("파티 캐릭터 목록. 씬에 놓인 캐릭터를 넣는다. 이 순서대로 교체하고, 첫 번째 캐릭터를 조작하며 시작한다")]
    [SerializeField] private PlayerController[] _members;
    [Tooltip("모든 캐릭터가 함께 쓰는 규칙 값 · 입력")]
    [SerializeField] private CommonCharacterSettings _common;
    [Tooltip("이동 방향 · 화면 중앙 대상 찾기의 기준이 되는 카메라 (Main Camera)")]
    [SerializeField] private Camera _mainCamera;

    // 패링 가능 시간을 연 적마다, 예고가 뜬 순간 정한 패링 가능 여부. 패링 가능 시간이 닫히면 지운다.
    private readonly Dictionary<IEnemyParryable, bool> _parryableAtOpen = new Dictionary<IEnemyParryable, bool>();
    // 이번 패링 가능 시간에 이미 패링을 시작한 적. 예고 하나에 캐릭터 하나만 패링하게 한다. 패링 가능 시간이 닫히면 지운다.
    private readonly HashSet<IEnemyParryable> _claimedParryAttackers = new HashSet<IEnemyParryable>();
    // 공격 대상을 받을 적 행동 그래프. 처치되어 꺼진 적도 남겨 둔다. 꺼진 그래프에 값을 넣어도 문제없다.
    private readonly List<BehaviorGraphAgent> _enemyAgents = new List<BehaviorGraphAgent>();
    // 빠른 지원 : 넉백된 캐릭터와 그 캐릭터를 넉백시킨 적. 입력 시간이 지나거나 쓰면 비운다.
    private PlayerController _quickSupportSource;
    private Transform _quickSupportTarget;
    private int _controlledIndex;
    private float _quickSupportTimer;
    // 마지막 교체 시각(실제 시간). 일반 교체의 최소 간격을 잰다.
    private float _lastSwapTime = float.NegativeInfinity;
    // 처음 조작하지 않는 캐릭터들을 껐는지
    private bool _isStandbyReady;
    // 결과 화면이 떠 입력을 잠갔는지. 씬을 다시 불러오면 풀린다.
    private bool _isInputLocked;

    public CommonCharacterSettings Common => _common;
    public Camera MainCamera => _mainCamera;
    public PlayerController Controlled => _members[_controlledIndex];
    // 파티 캐릭터 목록 (교체 순서). 화면이 대기 캐릭터 칸을 채우는 데 쓴다.
    public IReadOnlyList<PlayerController> Members => _members;
    public BattleSlowMotion SlowMotion { get; private set; }
    public PlayerLockOn LockOn { get; private set; }
    // 교체할 수 있는 대기 캐릭터가 있는지. 없으면 지원 공격(교체 패링)을 할 수 없어 적의 패링 공격 예고가 패링 불가로 뜬다.
    public bool HasStandby => FindStandbyIndex(NEXT) >= 0;
    // 콤보 스킬을 이어받을 살아 있는 파티원(조작 캐릭터 말고)이 있는지. 필드에 나와 있어도(연속 패링 · 퇴장 중) 이어받을 수 있다.
    public bool HasComboMember => CountAliveMembers() > 1;
    // 지금 교체 키로 패링할 수 있는지. 대기 캐릭터가 있거나, 적이 연속 패링 패턴 중이면(필드의 파티원이 다시 패링) true 다.
    // 살아 있는 캐릭터가 혼자면 연속 패링 패턴 중에도 패링할 수 없다.
    public bool CanParry => HasStandby || ParryWindow.IsChainActive && CountAliveMembers() > 1;
    public ComboSkillSelector ComboSelector { get; private set; }
    // 플레이어 입력(교체 · 캐릭터 행동 · 록온)을 막는지. 콤보 스킬 캐릭터 선택 중이거나, 결과 화면이 떠 입력을 잠갔거나, 일시정지 중일 때다.
    public bool IsInputBlocked => _isInputLocked || GamePause.IsPaused || ComboSelector.IsBlockingInput;

    // 조작 중인 캐릭터가 바뀌면 알린다. 시작할 때도 첫 캐릭터로 한 번 알린다. 카메라가 따라갈 대상을 바꾸는 데 쓴다.
    public event Action<PlayerController> ControlledChanged;
    // 마지막 살아 있는 캐릭터의 쓰러짐 모션이 끝나면 알린다. 게임 흐름이 실패를 처리하는 데 쓴다.
    // Unity.Behavior 에도 Action 이 있어 System 을 붙여 구분한다.
    public event System.Action Wiped;

    // 캐릭터 기능은 Start 부터 공통 설정을 읽는다. 그래서 캐릭터의 Start 보다 먼저 도는 Awake 에서 파티를 알려 준다.
    private void Awake()
    {
        CacheComponent();
        JoinMembers();
    }
    // 입력 기능은 각 캐릭터의 Awake 에서 준비되므로, 모든 Awake 가 끝난 뒤인 Start 에서 조작 캐릭터를 정한다.
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

    // attacker 를 지금 패링할 수 있는지. 예고가 뜬 순간(패링 가능 시간이 열린 순간)의 판단을 그대로 쓴다.
    // 예고가 빨간색(패링 불가)으로 뜬 뒤 대기 캐릭터가 생겨도, 그 예고는 끝까지 패링할 수 없다. 예고 색과 실제 판정을 맞추기 위해서다.
    // 열린 순간 판단을 아직 기록하지 않았으면(같은 알림을 받는 기능 사이의 순서) 지금 판단해 기록한다.
    public bool IsParryable(IEnemyParryable attacker)
    {
        if(!_parryableAtOpen.TryGetValue(attacker, out bool isParryable))
        {
            isParryable = CanParry;
            _parryableAtOpen[attacker] = isParryable;
        }

        return isParryable;
    }

    // attacker 를 지금 새로 패링할 수 있는지. 예고가 뜬 순간 패링 가능이었고, 이번 패링 가능 시간에 아직 아무도 패링을 시작하지 않았어야 한다.
    // 교체 키를 빠르게 여러 번 눌러도 예고 하나에 캐릭터 여럿이 동시에 패링하지 않게 하기 위해서다.
    public bool CanStartParry(IEnemyParryable attacker) => IsParryable(attacker) && !_claimedParryAttackers.Contains(attacker);

    // 캐릭터가 attacker 의 패링을 시작했음을 기록한다. 지원 패링을 시작할 때 부른다.
    public void ClaimParry(IEnemyParryable attacker) => _claimedParryAttackers.Add(attacker);

    // 조작 중인 캐릭터가 넉백될 때 피격 상태가 부른다. 입력 시간 동안 교체하면 빠른 지원이 된다.
    // 퇴장하려고 하던 동작을 마치는 이전 캐릭터가 넉백된 것은 빠른 지원을 열지 않는다.
    public void OpenQuickSupport(PlayerController knockedCharacter, Transform attacker)
    {
        if(knockedCharacter != Controlled) return;

        _quickSupportSource = knockedCharacter;
        _quickSupportTarget = attacker;
        _quickSupportTimer = _common.QuickSupportInputTime;
    }

    // 필살기 연출 동안 시전한 캐릭터를 뺀 파티원을 멈추거나 다시 움직인다. 필드에 없는 캐릭터에 걸어도 영향이 없다.
    public void SetOthersFrozen(PlayerController caster, bool isFrozen)
    {
        for(int i = 0; i < _members.Length; i++)
        {
            if(_members[i] != caster) _members[i].Status.SetFrozen(isFrozen);
        }
    }

    // 조작 중인 캐릭터가 얻은 필살기 게이지의 일정 비율을 나머지 파티원(대기 · 퇴장 중 포함)에게 나눠 준다.
    // 대기 캐릭터도 싸움에 참여한 몫을 받아, 교체해 들어왔을 때 필살기를 쓸 수 있게 하기 위해서다.
    public void ShareUltimateGauge(PlayerController giver, float amount)
    {
        float shared = amount * Common.UltimateGaugeShareRatio;

        for(int i = 0; i < _members.Length; i++)
        {
            if(_members[i] != giver) _members[i].Status.ReceiveSharedUltimateGauge(shared);
        }
    }

    // 필드에 나와 있는 모든 캐릭터를 duration 동안 무적으로 만든다. 패링 성공 때 불러, 막은 순간 다른 적의 공격에 파티가 맞지 않게 한다.
    // 대기 중(꺼진) 캐릭터는 시간이 흐르지 않아 나중에 나올 때 무적이 남으므로 넣지 않는다.
    public void BeginFieldInvincible(float duration)
    {
        for(int i = 0; i < _members.Length; i++)
        {
            if(_members[i].gameObject.activeSelf) _members[i].Status.BeginInvincible(duration);
        }
    }

    // 결과 화면이 뜨면 게임 흐름이 부른다. 화면의 버튼을 누르는 클릭이 공격 입력으로 들어가지 않게 한다.
    public void LockInput() => _isInputLocked = true;

    // 모든 캐릭터를 point 위치 · 방향으로 옮긴다. 보스전 재도전으로 씬을 다시 불러왔을 때 시작하며 부른다.
    // CharacterController 가 켜져 있으면 위치를 직접 바꿔도 덮어쓰므로 잠깐 끄고 옮긴다.
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

    // 실행 중 생성한 적을 등록하고 조작 중인 캐릭터를 공격 대상으로 준다. 이후 교체할 때마다 새 캐릭터로 바꿔 준다.
    public void RegisterEnemy(BehaviorGraphAgent agent)
    {
        _enemyAgents.Add(agent);
        agent.SetVariableValue(ENEMY_TARGET_VARIABLE, Controlled.transform);
    }

    // 캐릭터의 쓰러짐 모션이 끝나면 쓰러짐 상태가 부른다. 그 캐릭터를 끄고, 조작 중이던 캐릭터면 다음 살아 있는 캐릭터로 넘긴다.
    // 퇴장 중에 쓰러진 이전 캐릭터는 끄기만 한다.
    public void OnMemberDowned(PlayerController member)
    {
        member.Standby();

        if(member != Controlled) return;

        int standbyIndex = FindStandbyIndex(NEXT);

        if(standbyIndex >= 0)
        {
            PlayerController next = _members[standbyIndex];

            PlaceNext(member, next);
            next.SwapIn();
            SetControlled(standbyIndex);
            return;
        }

        int fieldIndex = FindAliveIndex(NEXT);

        if(fieldIndex >= 0)
        {
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

    // 처음 조작하지 않는 캐릭터를 끈다. 캐릭터는 Start 에서 상태를 만들기 때문에, Start 전에 끄면 처음 교체할 때 상태가 없다.
    // 씬의 모든 Start 는 첫 Update 전에 끝나므로 첫 Update 에서 끈다.
    private void HideStandbyOnce()
    {
        if(_isStandbyReady) return;

        _isStandbyReady = true;

        for(int i = 0; i < _members.Length; i++)
        {
            if(i != _controlledIndex) _members[i].Standby();
        }
    }

    // 대기 캐릭터는 꺼져 있어 스스로 에너지를 회복하지 못하므로 파티가 대신 채운다.
    // 초당 회복량(캐릭터 데이터)에 공통 설정의 대기 회복 배율을 곱한다. 꺼져 있어 캐릭터 시간(히트렉)이 없으므로 실제 시간으로 잰다.
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

    // 빠른 지원 입력 시간은 실제 시간으로 잰다. 캐릭터의 히트렉 · 슬로모션과 상관없이 흐른다.
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

    // 콤보 스킬 캐릭터 선택 중에는 교체 키를 선택이 쓰므로 교체하지 않는다.
    private void ReadSwapInput()
    {
        if(IsInputBlocked) return;

        if(_common.ParryPreviousAction.action.WasPressedThisFrame()) TrySwap(PREVIOUS);
        else if(_common.ParryNextAction.action.WasPressedThisFrame()) TrySwap(NEXT);
    }

    // direction 쪽으로 목록을 돌며 처음 만나는 대기 캐릭터로 교체한다.
    // 대기 캐릭터가 없으면 교체하지 않는다. 단 적이 연속 패링 패턴 중이면 필드의 파티원으로 다시 패링한다.
    // 조작 중인 캐릭터의 지금 동작이 교체를 허용하지 않으면 교체 키를 무시한다.
    // 일반 교체는 마지막 교체부터 최소 간격이 지나야 한다. 빠른 지원 · 패링이 되는 교체는 간격과 상관없이 한다.
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

    // 연속 패링 패턴 중 대기 캐릭터가 없을 때, 누른 방향으로 가장 가까운 살아 있는 파티원이 필드에 있어도(퇴장 중 포함) 다시 조작해 패링한다.
    // 살아 있는 캐릭터가 혼자면 패링하지 않는다. 패링을 시작하면 그 파티원의 퇴장 예약을 지운다.
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

    // 콤보 스킬 선택에서 고른 캐릭터를 콤보 스킬로 들여보낸다. 교체처럼 이전 캐릭터 자리에 꺼내 놓고 조작 캐릭터로 바꾼다.
    // 이전 캐릭터 자리에서 콤보 스킬의 순간 이동을 하므로, 남아 있는 이전 캐릭터 위치를 기준으로 비켜 선다.
    // 이전 캐릭터(강타를 맞힌 캐릭터 · 앞 연계의 콤보 스킬 캐릭터)는 하던 동작을 마친 뒤 퇴장한다.
    // 고른 캐릭터가 이미 필드에 있으면(연속 패링 · 퇴장 중) 옮기지 않고 그 자리에서 들어오며, 퇴장 예약을 지운다. (연속 패링과 같은 방식)
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

    // 조작 중인 캐릭터에서 isNext 쪽으로 한 칸씩 가며, used 에 없는 살아 있는 캐릭터를 찾는다. 없으면 null 이다.
    // 콤보 스킬 선택이 이번 연계에서 아직 쓰지 않은 후보를 고를 때 쓴다. 필드에 나와 있는 파티원도 후보다. (HasComboMember 와 같은 기준)
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

    // 슬로모션을 끝내고, 다음 캐릭터를 이전 캐릭터 자리에 꺼내 놓는다. 어떻게 들어올지는 부른 쪽이 정한다.
    // 꺼져 있는 동안 옮겨 두어야 켜지는 순간 CharacterController 가 새 위치에서 시작한다.
    private void PlaceNext(PlayerController previous, PlayerController next)
    {
        SlowMotion.End();
        next.transform.SetPositionAndRotation(previous.transform.position, previous.transform.rotation);
        next.gameObject.SetActive(true);
    }

    // 이전 캐릭터의 퇴장을 예약하고 조작 캐릭터를 바꾼다. 교체 시각을 기록해 일반 교체의 최소 간격을 잰다.
    private void FinishSwap(PlayerController previous, int nextIndex)
    {
        previous.ReserveSwapOut();
        SetControlled(nextIndex);
        _lastSwapTime = Time.unscaledTime;
    }

    // 이번 교체가 빠른 지원이나 지원 패링이 되는지. 들어올 캐릭터는 지금 캐릭터 자리에 나오므로 지금 캐릭터 기준으로 본다.
    private bool IsSupportSwap() => (_quickSupportTimer > 0f && _quickSupportSource == Controlled) || Controlled.ActionTrigger.HasParryTarget;

    // 새 캐릭터가 어떻게 들어올지 정한다. 우선순위는 빠른 지원 → 지원 공격 → 보통 입장이다.
    //   빠른 지원 : 이전 캐릭터가 넉백된 뒤 입력 시간 안이면, 넉백시킨 적 앞으로 순간 이동해 빠른 지원 액션을 쓴다.
    //   지원 공격 : 패링 가능 시간을 연 적이 있으면 입장 대신 바로 패링한다.
    // 빠른 지원 입력 시간은 교체하면 어느 쪽으로 들어오든 닫는다.
    private void EnterNext(PlayerController previous, PlayerController next)
    {
        bool canQuickSupport = _quickSupportTimer > 0f && _quickSupportSource == previous;
        Transform quickSupportTarget = _quickSupportTarget;

        ClearQuickSupport();

        if(canQuickSupport && next.TryStartQuickSupport(quickSupportTarget)) return;
        if(next.ActionTrigger.TryStartSupportParry()) return;

        next.SwapIn();
    }

    // 조작 중인 캐릭터에서 direction 쪽으로 한 칸씩 가며 꺼져 있는(대기) 살아 있는 캐릭터를 찾는다. 없으면 -1 이다.
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

    // 조작 중인 캐릭터에서 direction 쪽으로 한 칸씩 가며 대기 · 필드와 상관없이 살아 있는 캐릭터를 찾는다. 없으면 -1 이다.
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

    // index 의 캐릭터만 입력에 반응하게 하고, 적의 공격 대상을 바꾼 뒤 바뀐 것을 알린다.
    private void SetControlled(int index)
    {
        _controlledIndex = index;

        for(int i = 0; i < _members.Length; i++)
        {
            _members[i].Input.IsControlled = i == index;
        }

        RetargetEnemies();
        ControlledChanged?.Invoke(Controlled);
    }

    // 등록된 적 행동 그래프마다 공격 대상을 조작 중인 캐릭터로 바꾼다.
    private void RetargetEnemies()
    {
        for(int i = 0; i < _enemyAgents.Count; i++)
        {
            _enemyAgents[i].SetVariableValue(ENEMY_TARGET_VARIABLE, Controlled.transform);
        }
    }

    // 씬에 미리 놓인 적은 Awake 에서 찾는다. 생성기가 Start 에서 만든 적은 등록으로 들어오므로, 그보다 먼저 찾아야 두 번 들어가지 않는다.
    private void CacheComponent()
    {
        SlowMotion = GetComponent<BattleSlowMotion>();
        LockOn = GetComponent<PlayerLockOn>();
        ComboSelector = GetComponent<ComboSkillSelector>();
        _enemyAgents.AddRange(FindObjectsByType<BehaviorGraphAgent>(FindObjectsSortMode.None));
    }

    private void Init()
    {
        SetControlled(0);
    }

    // 패링 가능 시간이 열리는 순간 패링 가능 여부를 기록하고, 닫히면 지운다.
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

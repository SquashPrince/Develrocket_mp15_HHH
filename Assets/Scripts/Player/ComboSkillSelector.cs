using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 콤보 스킬 이어받기. 그로기 적에게 강타가 맞으면 캐릭터 선택 단계를 열고, 고른 캐릭터가 콤보 스킬을 이어서 쓰게 한다.
// 어느 캐릭터의 강타든 같은 규칙을 따르는 전투 공통 규칙이라, 캐릭터가 아닌 파티 오브젝트에 하나만 둔다.
//
// 연계 : 한 적의 그로기에서 쓸 수 있는 연계 횟수는 그 적의 최대 연계 수다. 콤보 스킬 시전과 캔슬이 각각 1회를 쓴다.
//        첫 강타(조작 캐릭터)와 콤보 스킬 중 대상에게 맞은 강타가 선택 단계를 연다.
//        캔슬했거나 콤보 스킬이 강타 없이 끝났는데 연계가 남았으면, 조작 캐릭터가 같은 적에게 강타를 맞혀 다시 선택 단계를 연다.
//        이번 연계에서 아직 콤보 스킬을 쓰지 않은 대기 캐릭터만 고를 수 있다. 그로기가 끝나거나 대상이 처치되면 남은 연계는 사라진다.
// 선택 단계 : 게임 전체 시간 배율을 낮춰 적 · 캐릭터 · 타격 간격 · 카메라가 함께 느려진다. 고르는 시간은 실제 시간으로 줄어든다.
//            이전(교체 이전 · 공격 · 필살기) · 다음(교체 다음 · 대시 · 스킬) 입력으로 고르고, 캔슬 입력이나 시간 초과는 캔슬이다.
//            이 동안 다른 플레이어 입력은 막는다.
// 그로기 시간 : 선택 동안 멈추고, 그로기 진입 · 콤보 스킬 시전 시작 · 연계가 남은 캔슬에 정지 시간을 건다.
//              마지막 연계를 콤보 스킬 없이 캔슬하면 정지 없이 바로 흐른다. 대기 캐릭터가 없으면(1인) 진입 정지도 없다.
[RequireComponent(typeof(PlayerParty))]
public class ComboSkillSelector : MonoBehaviour
{
    private enum ChainState
    {
        // 연계 없음
        None,
        // 캐릭터를 고르는 중
        Selecting,
        // 고른 캐릭터가 콤보 스킬을 시전하는 중
        Casting,
        // 연계가 남아 조작 캐릭터의 강타를 기다리는 중
        AwaitingStrike
    }

    // 이번 연계에서 콤보 스킬을 쓴 캐릭터
    private readonly HashSet<PlayerController> _usedCharacters = new HashSet<PlayerController>();
    private PlayerParty _party;
    private IEnemyTargetable _target;
    private IEnemyGroggyable _groggyable;
    // 이번 선택 단계에서 이전 · 다음 입력으로 고를 캐릭터. 후보가 하나면 둘이 같다.
    private PlayerController _previousCandidate;
    private PlayerController _nextCandidate;
    // 마지막으로 콤보 스킬을 시작한 캐릭터. 이 캐릭터의 강타 · 콤보 종료만 연계에 반영한다.
    private PlayerController _performer;
    private ChainState _state;
    private float _remainingTime;
    private int _maxChain;
    // 이번 연계에서 쓴 횟수 (콤보 스킬 시전 + 캔슬)
    private int _usedChainCount;
    // 선택을 마친 프레임. 선택에 쓴 입력이 같은 프레임에 교체 · 공격으로 다시 읽히지 않게 이 프레임까지 입력을 막는다.
    private int _closedFrame = -1;

    public bool IsSelecting => _state == ChainState.Selecting;
    // 선택 중이거나 선택을 마친 프레임이면 플레이어 입력을 막는다.
    public bool IsBlockingInput => IsSelecting || _closedFrame == Time.frameCount;
    public float RemainingTime => _remainingTime;
    // 남은 선택 시간의 비율(1 → 0). 화면의 원형 · 막대 표시에 쓴다.
    public float RemainingRatio => Mathf.Clamp01(_remainingTime / Common.ComboSelectTime);
    public PlayerController PreviousCandidate => _previousCandidate;
    public PlayerController NextCandidate => _nextCandidate;
    private CommonCharacterSettings Common => _party.Common;
    private bool IsLastChainUsed => _usedChainCount >= _maxChain;

    private void Awake() => CacheComponent();
    private void Update()
    {
        UpdateSelection();
        CheckChainExpired();
    }
    // 선택 중에 꺼지면 낮춘 게임 시간을 되돌린다.
    private void OnDisable()
    {
        if(!IsSelecting) return;

        CloseSelection();
        EndChain();
    }

    // 적이 그로기에 들어갔을 때 그로기를 쌓은 쪽(판정 · 패링)이 부른다. 대기 캐릭터가 있을 때만 정지 시간을 건다.
    public void OnGroggyEntered(IEnemyGroggyable groggyable)
    {
        if(_party.HasStandby) groggyable.HoldGroggy(Common.GroggyHoldTime);
    }

    // 강타 대기 중인 연계의 대상인지. 판정 기능이 기회를 이미 쓴 그로기 적도 콤보 대상으로 고를 때 쓴다.
    public bool IsAwaitingStrikeOn(IEnemyGroggyable groggyable) => _state == ChainState.AwaitingStrike && groggyable == _groggyable;

    // 조작 캐릭터의 강타가 그로기 적에게 맞았을 때 판정 기능이 부른다. 선택 단계를 열었으면 true 를 돌려준다.
    // 새 연계는 적의 콤보 스킬 기회를 쓰고 시작한다. 강타 대기 중인 연계는 같은 적일 때 이어서 연다.
    // 고를 대기 캐릭터가 없으면(1인 포함) 열지 않고, 새 연계라면 적의 기회도 남긴다.
    public bool TryBeginChain(IEnemyTargetable target, IEnemyGroggyable groggyable)
    {
        if(!HasCandidate()) return false;

        if(IsAwaitingStrikeOn(groggyable))
        {
            OpenSelection();
            return true;
        }

        if(_state != ChainState.None || !groggyable.TryConsumeComboSkillChance()) return false;

        _target = target;
        _groggyable = groggyable;
        _maxChain = groggyable.MaxComboChain;
        _usedChainCount = 0;
        _performer = null;

        OpenSelection();
        return true;
    }

    // 콤보 스킬 중 강타가 맞았을 때 특수 행동 시작 기능이 부른다.
    // 지금 시전 중인 캐릭터의 강타가 연계 대상에게 맞았고 고를 캐릭터가 있으면 다음 선택 단계를 연다.
    // 고를 캐릭터가 없으면 그대로 두고, 콤보 스킬이 끝나면 강타 대기로 넘어간다.
    public void RequestNextChain(PlayerController performer, Transform hitEnemy)
    {
        if(_state != ChainState.Casting || performer != _performer || hitEnemy != _target.transform) return;
        if(!HasCandidate()) return;

        OpenSelection();
    }

    // 콤보 스킬 상태가 끝날 때 부른다. 시전 중이던 캐릭터가 다음 선택 없이 끝났으면 강타 대기로 넘어간다.
    public void OnComboSkillEnded(PlayerController performer)
    {
        if(_state == ChainState.Casting && performer == _performer) _state = ChainState.AwaitingStrike;
    }

    private void UpdateSelection()
    {
        if(!IsSelecting) return;

        _remainingTime -= Time.unscaledDeltaTime;

        if(WasPressed(Common.ParryPreviousAction, Common.AttackAction, Common.UltimateAction)) Select(_previousCandidate);
        else if(WasPressed(Common.ParryNextAction, Common.DashAction, Common.SkillAction)) Select(_nextCandidate);
        else if(Common.ComboCancelAction.action.WasPressedThisFrame() || _remainingTime <= 0f) Cancel();
    }

    // 강타 대기 · 시전 중에 그로기가 끝나거나 대상이 처치되면 남은 연계는 사라진다.
    private void CheckChainExpired()
    {
        if(_state != ChainState.AwaitingStrike && _state != ChainState.Casting) return;

        if(!_target.IsAlive || !_groggyable.IsGroggy) EndChain();
    }

    // 게임 시간을 낮추고 그로기 시간을 멈춘 뒤 후보를 정한다. 완벽 회피 슬로모션이 남아 있으면 겹치지 않게 끝낸다.
    private void OpenSelection()
    {
        _previousCandidate = _party.FindComboCandidate(false, _usedCharacters);
        _nextCandidate = _party.FindComboCandidate(true, _usedCharacters);
        _remainingTime = Common.ComboSelectTime;
        _state = ChainState.Selecting;

        _party.SlowMotion.End();
        _groggyable.SetGroggyPaused(true);
        Time.timeScale = Common.ComboSelectTimeScale;
    }

    // 고른 캐릭터가 콤보 스킬로 들어오고 연계 1회를 쓴다. 시전을 시작할 때마다(마지막 연계 포함) 그로기 정지 시간을 건다.
    // 대상이 그 사이 처치되었으면 콤보 스킬 없이 끝낸다.
    private void Select(PlayerController candidate)
    {
        CloseSelection();

        if(!_target.IsAlive)
        {
            EndChain();
            return;
        }

        _usedChainCount++;
        _usedCharacters.Add(candidate);
        _performer = candidate;
        _state = ChainState.Casting;

        _groggyable.HoldGroggy(Common.GroggyHoldTime);
        _party.EnterComboSkill(candidate, _target);

        if(IsLastChainUsed) EndChain();
    }

    // 캔슬 입력 · 시간 초과. 콤보 스킬 없이 연계 1회를 쓴다.
    // 연계가 남았으면 그로기 정지 시간을 걸고 강타 대기로, 마지막 연계였으면 정지 없이 끝낸다.
    private void Cancel()
    {
        CloseSelection();

        _usedChainCount++;

        if(IsLastChainUsed)
        {
            EndChain();
            return;
        }

        _groggyable.HoldGroggy(Common.GroggyHoldTime);
        _state = ChainState.AwaitingStrike;
    }

    private void CloseSelection()
    {
        _closedFrame = Time.frameCount;
        Time.timeScale = 1f;
        _groggyable.SetGroggyPaused(false);
    }

    private void EndChain()
    {
        _usedCharacters.Clear();
        _state = ChainState.None;
        _target = null;
        _groggyable = null;
        _performer = null;
    }

    private bool HasCandidate() => _party.FindComboCandidate(true, _usedCharacters) != null;

    private static bool WasPressed(InputActionReference first, InputActionReference second, InputActionReference third)
    {
        return first.action.WasPressedThisFrame() || second.action.WasPressedThisFrame() || third.action.WasPressedThisFrame();
    }

    private void CacheComponent()
    {
        _party = GetComponent<PlayerParty>();
    }
}

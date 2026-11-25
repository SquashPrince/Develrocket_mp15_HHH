using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 콤보 스킬 이어받기. 그로기 적에게 강타가 맞으면 캐릭터 선택 단계를 열고, 고른 캐릭터가 콤보 스킬을 이어서 쓰게 한다.
// 전투 공통 규칙이라 파티 오브젝트에 하나만 둔다. 남은 연계 수는 적의 바가 보여줄 수 있게 적이 센다.
// 자세한 흐름 : Docs/Technical/Player/PlayerComboSkill.md
[RequireComponent(typeof(PlayerParty))]
public class ComboSkillSelector : MonoBehaviour
{
    private enum ChainState
    {
        None,
        Selecting,
        Casting,
        // 연계가 남아 조작 캐릭터의 강타를 기다리는 중
        AwaitingStrike
    }

    private readonly HashSet<PlayerController> _usedCharacters = new HashSet<PlayerController>();
    private PlayerParty _party;
    private IEnemyTargetable _target;
    private IEnemyGroggyable _groggyable;
    // 후보가 하나면 둘이 같다.
    private PlayerController _previousCandidate;
    private PlayerController _nextCandidate;
    // 이 캐릭터의 강타 · 콤보 종료만 연계에 반영한다.
    private PlayerController _performer;
    private ChainState _state;
    private float _remainingTime;
    // 선택에 쓴 입력이 같은 프레임에 교체 · 공격으로 다시 읽히지 않게 이 프레임까지 입력을 막는다.
    private int _closedFrame = -1;

    public bool IsSelecting => _state == ChainState.Selecting;
    public IEnemyTargetable Target => _target;
    public bool IsBlockingInput => IsSelecting || _closedFrame == Time.frameCount;
    public float RemainingTime => _remainingTime;
    public float RemainingRatio => Mathf.Clamp01(_remainingTime / Common.ComboSelectTime);
    public PlayerController PreviousCandidate => _previousCandidate;
    public PlayerController NextCandidate => _nextCandidate;
    private CommonCharacterSettings Common => _party.Common;
    private bool IsLastChainUsed => !_groggyable.HasRemainingComboChain;

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

    // 이어받을 파티원이 없으면(1인) 적의 연계를 없애 적의 바가 처음부터 연계 없음으로 보이게 한다.
    public void OnGroggyEntered(IEnemyGroggyable groggyable)
    {
        if(_party.HasComboMember) groggyable.HoldGroggy(Common.GroggyHoldTime);
        else groggyable.ClearComboChain();
    }

    public bool IsAwaitingStrikeOn(IEnemyGroggyable groggyable) => _state == ChainState.AwaitingStrike && groggyable == _groggyable;

    // 고를 캐릭터가 없으면 열지 않아 적의 기회도 남는다.
    // 강타 대기 뒤의 강타는 새 이어받기의 시작이라 쓴 캐릭터 기록을 먼저 비운다.
    public bool TryBeginChain(IEnemyTargetable target, IEnemyGroggyable groggyable)
    {
        if(IsAwaitingStrikeOn(groggyable))
        {
            _usedCharacters.Clear();

            if(!HasCandidate()) return false;

            OpenSelection();
            return true;
        }

        if(!HasCandidate()) return false;

        if(_state != ChainState.None || !groggyable.TryConsumeComboSkillChance()) return false;

        _target = target;
        _groggyable = groggyable;
        _performer = null;

        OpenSelection();
        return true;
    }

    // 고를 캐릭터가 없으면 그대로 두고, 콤보 스킬이 끝나면 강타 대기로 넘어간다.
    public void RequestNextChain(PlayerController performer, Transform hitEnemy)
    {
        if(_state != ChainState.Casting || performer != _performer || hitEnemy != _target.transform) return;
        if(!HasCandidate()) return;

        OpenSelection();
    }

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

    private void CheckChainExpired()
    {
        if(_state != ChainState.AwaitingStrike && _state != ChainState.Casting) return;

        if(!_target.IsAlive || !_groggyable.IsGroggy) EndChain();
    }

    // 게임 전체 시간을 낮춘다. 고르는 시간은 실제 시간으로 줄어든다.
    private void OpenSelection()
    {
        _previousCandidate = _party.FindComboCandidate(false, _usedCharacters);
        _nextCandidate = _party.FindComboCandidate(true, _usedCharacters);
        _remainingTime = Common.ComboSelectTime;
        _state = ChainState.Selecting;

        _party.SlowMotion.End();
        _groggyable.SetGroggyPaused(true);
        Time.timeScale = Common.ComboSelectTimeScale;
        BattleFeedback.Raise(BattleFeedback.Moment.ComboSelectOpened);
    }

    private void Select(PlayerController candidate)
    {
        CloseSelection();

        if(!_target.IsAlive)
        {
            EndChain();
            return;
        }

        _groggyable.UseComboChain();
        _usedCharacters.Add(candidate);
        _performer = candidate;
        _state = ChainState.Casting;

        _groggyable.HoldGroggy(Common.GroggyHoldTime);
        _party.EnterComboSkill(candidate, _target);

        if(IsLastChainUsed)
        {
            EndChain();
            return;
        }

        // 이어받을 캐릭터가 더 없으면 적의 바가 연계 없음으로 보이게 남은 연계를 없앤다.
        if(!HasCandidate())
        {
            _groggyable.ClearComboChain();
            EndChain();
        }
    }

    // 콤보 스킬 없이 연계 1회를 쓴다. 마지막 연계였으면 그로기 정지 없이 끝낸다.
    private void Cancel()
    {
        CloseSelection();

        _groggyable.UseComboChain();

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
        BattleFeedback.Raise(BattleFeedback.Moment.ComboSelectClosed);
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

using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어 공격의 타격 판정. 애니메이션 이벤트가 정한 시점에 범위 안의 적을 찾아 대미지 · 경직 · 그로기 · 콤보 스킬 연계를 적용한다.
// 구간 판정은 캐릭터 시간으로 잰 타격 시점마다 맞히므로, 히트렉으로 멈춘 동안은 다음 시점이 오지 않는다.
// 자세한 흐름 : Docs/Technical/Player/PlayerAttack.md
[RequireComponent(typeof(PlayerStatus), typeof(PlayerActionTrigger))]
public class PlayerAttackJudge : MonoBehaviour
{
    private const int MAX_HIT_COLLIDERS = 16;

    private readonly HashSet<IEnemyDamageable> _hitTargets = new HashSet<IEnemyDamageable>();
    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    private PlayerStatus _status;
    private PlayerActionTrigger _actionTrigger;
    private PlayerInputReader _input;
    private PlayerController _controller;
    private ActionData _windowAction;
    private ActionStep _windowStep;
    // 이벤트 전달 기능이 판정 시작 직전에 알려 준다. 모르면 -1
    private float _preparedLength = -1f;
    private int _preparedHitEffectIndex;
    private int _hitEffectIndex;
    private bool _preparedEndsWithImpact;
    // 캐릭터 시계 기준
    private float _windowStartTime;
    private float _windowLength;
    private bool _windowEndsWithImpact;
    private int _nextTickIndex;
    private bool _hasReacted;
    private bool _hasPlayedSwing;

    // 디버그 기능이 범위를 표시하는 데 쓴다.
    public event Action<ActionStep> RangeChecked;

    // 콤보 스킬 발동 판정은 전투 공통 규칙이라 파티의 콤보 스킬 선택이 맡는다.
    private ComboSkillSelector ComboSelector => _controller.Party.ComboSelector;

    public bool IsWindowOpen { get; private set; }

    private void Awake() => CacheComponent();
    private void Update() => UpdateWindow();

    public void PrepareWindow(float length, bool endsWithImpact)
    {
        _preparedLength = length;
        _preparedEndsWithImpact = endsWithImpact;
    }

    public void PrepareHitEffect(int index)
    {
        _preparedHitEffectIndex = index;
    }

    // 판정 범위 · 방식은 동작(step), 위력 · 게이지 같은 타격 값은 액션(action)에서 읽는다.
    public void BeginHit(ActionData action, ActionStep step)
    {
        CloseWindow();
        PlaySwingSoundOnce(step);
        UsePreparedHitEffect();

        if(step.Mode == ActionStep.HitMode.Instant)
        {
            HitInRange(action, step, false, true, true);
            return;
        }

#if UNITY_EDITOR
        if(step.Timing == ActionStep.WindowTiming.Count && _preparedLength < 0f) Debug.LogWarning($"[판정] {step.AnimationState} : 횟수 방식인데 판정 시작 뒤에 판정 끝 · 강타 끝 이벤트가 없어 시작 시점에 한 번만 타격한다");
#endif
        _windowAction = action;
        _windowStep = step;
        _windowStartTime = _status.LocalTime;
        _windowLength = _preparedLength;
        _windowEndsWithImpact = _preparedEndsWithImpact;
        _nextTickIndex = 0;
        _hasReacted = false;
        IsWindowOpen = true;
        _preparedLength = -1f;

        UpdateWindow();
    }

    // 이벤트가 프레임 사이에 와도 끝 시점의 타격이 빠지지 않게 남은 타격을 마치고 끈다.
    public void FinishHit()
    {
        if(IsWindowOpen) HitDueTicks(GetWindowEndTime());

        CloseWindow();
    }

    // 동작이 바뀔 때 부른다. 남은 타격은 하지 않고 휘두름 소리 기록도 지운다.
    public void EndHit()
    {
        CloseWindow();
        _hasPlayedSwing = false;
    }

    // 구간 끝 전의 타격을 마치고 강타 판정을 한 번 한다. 강타는 항상 경직 · 히트렉을 건다.
    public void PerformImpact(ActionData action, ActionStep step)
    {
        if(IsWindowOpen) HitDueTicks(GetWindowEndTime());

        CloseWindow();
        PlaySwingSoundOnce(step);
        UsePreparedHitEffect();
        HitInRange(action, step, true, true, true);
    }

    // 번호를 넣지 않은 이벤트(Int 0)는 목록의 첫 이펙트를 쓴다.
    private void UsePreparedHitEffect()
    {
        _hitEffectIndex = _preparedHitEffectIndex;
        _preparedHitEffectIndex = 0;
    }

    // 맞았는지와 상관없는 시전 소리라 적이 없어도 동작마다 한 번 낸다.
    private void PlaySwingSoundOnce(ActionStep step)
    {
        if(_hasPlayedSwing) return;

        _hasPlayedSwing = true;
        SfxRequest.Play(step.SwingSound);
    }

    // 같은 동작 안에서 쓰므로 휘두름 소리 기록은 남긴다.
    private void CloseWindow()
    {
        IsWindowOpen = false;
        _windowStep = null;
        _windowAction = null;
    }

    private void UpdateWindow()
    {
        if(!IsWindowOpen) return;

        HitDueTicks(_status.LocalTime - _windowStartTime);
    }

    private void HitDueTicks(float elapsed)
    {
        while(IsWindowOpen && HasTick(_nextTickIndex) && GetTickTime(_nextTickIndex) <= elapsed + HitWindowTiming.TIME_EPSILON)
        {
            _nextTickIndex++;

            // 첫 타격에만 반응 · 히트렉을 거는 동작은, 이미 반응한 구간에서 반응 · 멈춤을 생략한다.
            bool canReact = !_windowStep.ReactOnFirstHitOnly || !_hasReacted;
            bool canHitLag = canReact && (!_windowStep.HitLagOnFirstHitOnly || !_hasReacted);

            if(HitInRange(_windowAction, _windowStep, false, canReact, canHitLag) && canReact) _hasReacted = true;
        }
    }

    // 강타로 끝나면 끝 시점은 강타가 맡는다.
    private bool HasTick(int index) =>
        HitWindowTiming.HasTick(IsIntervalWindow, _windowStep.HitInterval, _windowStep.HitCount, _windowLength, _windowEndsWithImpact, index);

    private float GetTickTime(int index) =>
        HitWindowTiming.GetTickTime(IsIntervalWindow, _windowStep.HitInterval, _windowStep.HitCount, _windowLength, index);

    private bool IsIntervalWindow => _windowStep.Timing == ActionStep.WindowTiming.Interval;

    // 구간 길이를 모르면 지금까지 지난 시간까지 친다.
    private float GetWindowEndTime() => _windowLength >= 0f ? _windowLength : _status.LocalTime - _windowStartTime;

    // 한 명이라도 맞았으면 true. 히트렉은 여러 적을 맞혀도 한 번만 건다.
    private bool HitInRange(ActionData action, ActionStep step, bool isImpact, bool canReact, bool canHitLag)
    {
        int count = step.Range.Overlap(transform, _hitColliders, Physics.AllLayers);
        IEnemyTargetable comboSkillTarget = null;
        IEnemyGroggyable comboSkillGroggyable = null;
        bool hasHit = false;

        RangeChecked?.Invoke(step);

        // 콜라이더가 여러 개인 적도 한 번의 찾기에서 한 번만 맞도록 이번에 맞은 적을 기억한다.
        _hitTargets.Clear();

        for(int i = 0; i < count; i++)
        {
            IEnemyDamageable enemy = _hitColliders[i].GetComponentInParent<IEnemyDamageable>();

            if(enemy == null || !enemy.IsAlive || !_hitTargets.Add(enemy)) continue;

            // 맞은 점은 한 번만 구해 대미지 숫자와 적중 이펙트가 같은 자리에 뜨게 한다.
            Vector3 hitPoint = GetHitPoint(step, _hitColliders[i], out Quaternion hitRotation);

            ApplyHit(enemy, action, step, isImpact, canReact, canHitLag, hitPoint, ref comboSkillTarget, ref comboSkillGroggyable);
            PlayHitEffect(step, hitPoint, hitRotation);
            hasHit = true;
        }

        if(hasHit && canHitLag) _status.BeginHitLag(step.HitLagTime);

        // 여러 적을 맞혀도 게이지 · 연출 · 효과음이 겹쳐 커지지 않게 판정마다 한 번만 얻고 알린다.
        if(hasHit)
        {
            _status.AddEnergy(action.EnergyGain);
            _status.AddUltimateGauge(action.UltimateGaugeGain);
            BattleFeedback.Raise(isImpact ? BattleFeedback.Moment.ImpactHit : BattleFeedback.Moment.NormalHit);
            SfxRequest.Play(action.HitSound);
        }

        // 같은 판정의 다른 적도 결과를 받도록 판정을 모두 마친 뒤 콤보 스킬 선택을 연다.
        if(comboSkillTarget != null) ComboSelector.TryBeginChain(comboSkillTarget, comboSkillGroggyable);

        return hasHit;
    }

    // 히트스톱은 플레이어 히트렉과 같은 시간이라 둘이 함께 멈췄다 함께 풀린다.
    // 그로기를 쌓은 뒤 콤보 스킬을 확인하므로, 게이지를 채운 강타가 바로 콤보 스킬 선택을 연다.
    private void ApplyHit(IEnemyDamageable enemy, ActionData action, ActionStep step, bool isImpact, bool canReact, bool canHitLag, Vector3 hitPoint, ref IEnemyTargetable comboSkillTarget, ref IEnemyGroggyable comboSkillGroggyable)
    {
        float damage = _controller.Character.RollDamage(action.Power, out bool isCritical);

        // 그로기 배율은 적이 적용하므로, 숫자는 적이 실제로 받은 대미지로 보여준다.
        float dealt = enemy.TakeDamage(damage, isImpact);

        BattleFeedback.RaiseDamage(enemy.transform, hitPoint, dealt, isImpact, isCritical);

        // 그로기 적이 강타로 처치되면 콤보 스킬보다 처치가 먼저다.
        if(!enemy.IsAlive) return;

        if(canReact && action.StaggerTime > 0f && enemy.transform.TryGetComponent(out IEnemyStaggerable staggerable)) staggerable.Stagger(action.StaggerTime, action.StaggerLevel);

        // 경직을 건 뒤 멈춰, 맞는 모션의 첫 자세에서 멈췄다가 이어지게 한다.
        if(canHitLag) enemy.BeginHitStop(step.HitLagTime);

        bool hasGroggy = enemy.transform.TryGetComponent(out IEnemyGroggyable groggyable);

        if(hasGroggy && groggyable.AddGroggy(action.GroggyGain)) ComboSelector.OnGroggyEntered(groggyable);

        if(!isImpact) return;

        // 콤보 스킬 중의 강타는 새로 시작하지 않고 다음 연계를 잇게 한다.
        if(!_actionTrigger.CanStartComboSkill)
        {
            _actionTrigger.OnComboSkillImpactHit(enemy);
            return;
        }

        // 퇴장 중인 이전 캐릭터의 강타는 발동 기회를 소모하지 않는다.
        if(!_input.IsControlled) return;

        // 한 번의 판정에서 콤보 스킬은 적 하나에만 발동한다. 기회는 콤보 스킬 선택이 실제로 열릴 때 소모한다.
        if(comboSkillTarget != null || !hasGroggy) return;
        if(!groggyable.HasComboSkillChance && !ComboSelector.IsAwaitingStrikeOn(groggyable)) return;

        enemy.transform.TryGetComponent(out comboSkillTarget);
        comboSkillGroggyable = groggyable;
    }

    // 높이는 콜라이더 중심이 아닌 판정 범위 중심을 쓴다. 큰 적(보스)을 아래에서 때려도 이펙트가 공중에 뜨지 않게 하기 위해서다.
    private Vector3 GetHitPoint(ActionStep step, Collider hitCollider, out Quaternion rotation)
    {
        Bounds bounds = hitCollider.bounds;
        float height = Mathf.Clamp(transform.TransformPoint(step.Range.Center).y, bounds.min.y, bounds.max.y);
        Vector3 attackerPoint = new Vector3(transform.position.x, height, transform.position.z);
        Vector3 direction = HorizontalVector.Between(attackerPoint, bounds.center);

        // 캐릭터와 적의 중심이 겹치면 방향을 알 수 없으므로 캐릭터 정면을 쓴다.
        if(direction == Vector3.zero) direction = transform.forward;

        rotation = Quaternion.LookRotation(direction);
        return hitCollider.ClosestPoint(attackerPoint);
    }

    // 목록에 없는 번호는 클립과 데이터가 어긋난 것이라 경고만 한다. 다단 히트가 한 점에 겹치지 않게 좌우 · 위아래로 흩뜨린다.
    private void PlayHitEffect(ActionStep step, Vector3 hitPoint, Quaternion rotation)
    {
        if(!step.HasHitEffects) return;

        HitEffectSetting setting = step.GetHitEffect(_hitEffectIndex);

        if(setting == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[적중 이펙트] 동작 '{step.AnimationState}' 에 {_hitEffectIndex} 번 적중 이펙트가 없습니다. 판정 시작 · 강타 끝 이벤트의 Int 값과 동작의 적중 이펙트 목록을 확인하세요.", this);
#endif
            return;
        }

        GameObject prefab = setting.Prefab;

        if(prefab == null) return;

        Vector2 spread = _controller.Common.HitEffectSpread;
        Vector3 offset = rotation * new Vector3(UnityEngine.Random.Range(-spread.x, spread.x), UnityEngine.Random.Range(-spread.y, spread.y), 0f);

        // 기울기는 공격 방향(이펙트의 앞쪽 축)을 축으로 돌린다.
        BattleEffect.Play(prefab, hitPoint + offset, rotation * Quaternion.Euler(0f, 0f, setting.PickRollAngle()));
    }

    private void CacheComponent()
    {
        _status = GetComponent<PlayerStatus>();
        _actionTrigger = GetComponent<PlayerActionTrigger>();
        _input = GetComponent<PlayerInputReader>();
        _controller = GetComponent<PlayerController>();
    }
}

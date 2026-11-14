using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어 공격의 타격 판정. 동작의 판정 범위 안에 있는 적을 찾아(범위 찾기), 찾은 적마다 결과를 적용한다(맞은 적 처리).
// 판정 시점은 모션의 애니메이션 이벤트가 정한다.
//   판정 시작 : 구간 판정 동작은 판정을 켜고, 한 순간 판정 동작은 이 순간 한 번 판정한다.
//   판정 끝 : 구간 끝까지 남은 타격을 마치고 판정을 끈다.
//   강타 끝 : 구간 끝 전의 타격을 마치고, 이 순간 강타 판정을 한 번 한 뒤 판정을 끈다. 구간의 마지막 타격을 강타로 만들 때 쓴다.
// 구간 판정은 시작부터의 타격 시점(간격 또는 횟수로 정함)마다 범위 안의 적을 모두 맞힌다. 시점 사이에 범위로 들어온 적은 다음 시점에 맞는다.
//   시점은 캐릭터 시간으로 재므로, 히트렉으로 멈춘 동안은 다음 시점이 오지 않는다.
// 적중하면 동작의 히트렉 시간 동안 공격한 캐릭터만 멈추도록 상태 기능에 요청한다.
// 동작이 "첫 타격에만 반응"을 켜면, 구간(판정 시작 한 번)에서 처음 적중한 타격에만 경직 · 히트렉을 건다. 강타는 항상 건다.
// 범위 찾기 : 동작의 모양(상자 · 구 · 캡슐)대로 캐릭터 기준 범위를 확인한다.
// 맞은 적 처리 : 체력 감소 · 에너지 · 필살기 게이지 획득 · 경직 · 그로기 누적 · 콤보 스킬 발동과 연계.
// 두 과정을 나눠 둔 이유는, 나중에 검기처럼 날아가며 판정하는 공격도 같은 맞은 적 처리를 쓰게 하기 위해서다.
// 범위를 찾을 때 이벤트로 알려, 디버그 기능이 범위를 표시한다. 디버그 기능이 없어도 판정은 그대로다.
[RequireComponent(typeof(PlayerStatus), typeof(PlayerActionTrigger))]
public class PlayerAttackJudge : MonoBehaviour
{
    // 한 번의 범위 찾기에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_HIT_COLLIDERS = 16;
    // 타격 시점과 구간 끝을 비교할 때 허용하는 오차(초). 구간 끝 시점의 타격이 계산 오차로 빠지지 않게 한다.
    private const float TIME_EPSILON = 0.0001f;

    private readonly HashSet<IEnemyDamageable> _hitTargets = new HashSet<IEnemyDamageable>();
    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    private PlayerStatus _status;
    private PlayerActionTrigger _actionTrigger;
    private ActionData _windowAction;
    private ActionStep _windowStep;
    // 다음 판정 시작에 쓸 구간 길이와 강타로 끝나는지. 이벤트 전달 기능이 판정 시작 직전에 알려 준다.
    private float _preparedLength = -1f;
    private bool _preparedEndsWithImpact;
    // 지금 구간의 시작 시각(캐릭터 시계), 길이(모르면 -1), 강타로 끝나는지, 다음 타격 번호, 이미 경직 · 히트렉을 걸었는지
    private float _windowStartTime;
    private float _windowLength;
    private bool _windowEndsWithImpact;
    private int _nextTickIndex;
    private bool _hasReacted;

    // 판정 범위를 찾을 때마다 알린다. 전달 값 : 범위를 찾은 동작
    public event Action<ActionStep> RangeChecked;

    // 구간 판정이 켜져 있는지
    public bool IsWindowOpen { get; private set; }

    private void Awake() => CacheComponent();
    private void Update() => UpdateWindow();

    // 다음 판정 시작의 구간 길이(초, 모르면 -1)와 강타로 끝나는지를 미리 받아 둔다. 이벤트 전달 기능이 판정 시작 이벤트 직전에 부른다.
    public void PrepareWindow(float length, bool endsWithImpact)
    {
        _preparedLength = length;
        _preparedEndsWithImpact = endsWithImpact;
    }

    // 판정 시작 이벤트. 구간 판정 동작이면 판정을 켜고 시작 시점의 타격을 바로 한다. 한 순간 판정 동작이면 이 순간 한 번만 판정한다.
    // 판정 범위 · 방식은 동작(step), 위력 · 게이지 같은 타격 값은 액션(action)에서 읽는다.
    public void BeginHit(ActionData action, ActionStep step)
    {
        EndHit();

        if(step.Mode == ActionStep.HitMode.Instant)
        {
            HitInRange(action, step, false, true);
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

    // 판정 끝 이벤트. 구간 끝 시점까지 아직 하지 않은 타격을 마치고 판정을 끈다.
    // 이벤트가 프레임 사이에 와도 끝 시점의 타격이 빠지지 않게 하기 위해서다.
    public void FinishHit()
    {
        if(IsWindowOpen) HitDueTicks(GetWindowEndTime());

        EndHit();
    }

    // 판정을 바로 끈다. 상태가 바뀌거나 다음 동작으로 넘어갈 때 부른다. 남은 타격은 하지 않는다.
    public void EndHit()
    {
        IsWindowOpen = false;
        _windowStep = null;
        _windowAction = null;
    }

    // 강타 끝 이벤트. 구간 끝 전의 타격을 마치고, 이 순간 범위 안의 적에게 강타 판정을 한 번 한 뒤 판정을 끈다.
    // 강타는 범위 안의 적마다 한 번씩 맞고 항상 경직 · 히트렉을 건다.
    // 그로기 적에게 맞으면 판정을 모두 마친 뒤 콤보 스킬을 발동한다. 콤보 스킬 중이면 새로 발동하지 않고, 콤보 스킬 대상에게 맞았을 때 다음 연계를 잇게 한다.
    public void PerformImpact(ActionData action, ActionStep step)
    {
        if(IsWindowOpen) HitDueTicks(GetWindowEndTime());

        EndHit();
        HitInRange(action, step, true, true);
    }

    // 캡슐의 양 끝 구 중심. 캐릭터 앞쪽 방향으로 전체 길이만큼 뻗고, 길이가 반지름의 두 배보다 짧으면 두 점이 겹쳐 구가 된다.
    // 디버그 기능이 같은 모양으로 범위를 그릴 때도 쓴다.
    public void GetCapsulePoints(ActionStep step, out Vector3 start, out Vector3 end)
    {
        Vector3 center = transform.TransformPoint(step.HitCenter);
        float halfSegment = Mathf.Max(0f, step.HitLength * 0.5f - step.HitRadius);

        start = center - transform.forward * halfSegment;
        end = center + transform.forward * halfSegment;
    }

    // 구간 판정이 켜져 있으면 지금까지 지난 타격 시점의 타격을 한다.
    private void UpdateWindow()
    {
        if(!IsWindowOpen) return;

        HitDueTicks(_status.LocalTime - _windowStartTime);
    }

    // 구간 시작부터 elapsed(초)까지 지난 타격 시점 중 아직 하지 않은 타격을 순서대로 한다.
    private void HitDueTicks(float elapsed)
    {
        while(IsWindowOpen && HasTick(_nextTickIndex) && GetTickTime(_nextTickIndex) <= elapsed + TIME_EPSILON)
        {
            _nextTickIndex++;

            // 첫 타격에만 반응하는 동작이면, 이 구간에서 이미 경직 · 히트렉을 걸었을 때 이번 타격은 반응 없이 맞힌다.
            bool canReact = !_windowStep.ReactOnFirstHitOnly || !_hasReacted;

            if(HitInRange(_windowAction, _windowStep, false, canReact) && canReact) _hasReacted = true;
        }
    }

    // index 번째 타격이 이 구간에 있는지.
    //   횟수 : 강타로 끝나면 끝 시점은 강타가 맡으므로 횟수보다 하나 적게, 아니면 횟수만큼 한다. 길이를 모르면 시작 시점 한 번만 한다.
    //   간격 : 길이를 모르면 판정이 꺼질 때까지 계속, 알면 구간 끝(강타로 끝나면 끝 직전)까지만 한다.
    private bool HasTick(int index)
    {
        if(_windowStep.Timing == ActionStep.WindowTiming.Count)
        {
            if(_windowLength < 0f) return index == 0;

            int count = _windowEndsWithImpact ? _windowStep.HitCount - 1 : _windowStep.HitCount;

            return index < count;
        }

        if(_windowLength < 0f) return true;

        float tickTime = GetTickTime(index);

        return _windowEndsWithImpact ? tickTime < _windowLength - TIME_EPSILON : tickTime <= _windowLength + TIME_EPSILON;
    }

    // index 번째 타격 시점(구간 시작부터 초).
    //   간격 : 간격 × 번호 / 횟수 : 시작과 끝을 포함해 구간을 (횟수 - 1) 칸으로 나눈 간격 × 번호. 1 타면 시작 시점이다
    private float GetTickTime(int index)
    {
        if(_windowStep.Timing == ActionStep.WindowTiming.Interval) return _windowStep.HitInterval * index;

        int gaps = _windowStep.HitCount - 1;

        return gaps > 0 && _windowLength > 0f ? _windowLength / gaps * index : 0f;
    }

    // 판정 끝 · 강타 끝 이벤트가 왔을 때 남은 타격을 어디까지 할지. 구간 길이를 알면 그 길이, 모르면 지금까지 지난 시간이다.
    private float GetWindowEndTime() => _windowLength >= 0f ? _windowLength : _status.LocalTime - _windowStartTime;

    // 범위 안의 적을 찾아 맞히고, 한 명이라도 맞았으면 true 를 돌려준다.
    // canReact 이면 맞은 적을 경직시키고, 한 번의 찾기에서 여러 적을 맞혀도 히트렉은 한 번만 건다.
    private bool HitInRange(ActionData action, ActionStep step, bool isImpact, bool canReact)
    {
        int count = FindHitColliders(step);
        IEnemyTargetable comboSkillTarget = null;
        int comboSkillMaxChain = 0;
        bool hasHit = false;

        RangeChecked?.Invoke(step);

        // 콜라이더가 여러 개인 적도 한 번의 찾기에서 한 번만 맞도록 이번에 맞은 적을 기억한다.
        _hitTargets.Clear();

        for(int i = 0; i < count; i++)
        {
            IEnemyDamageable enemy = _hitColliders[i].GetComponentInParent<IEnemyDamageable>();

            if(enemy == null || !_hitTargets.Add(enemy)) continue;

            ApplyHit(enemy, action, isImpact, canReact, ref comboSkillTarget, ref comboSkillMaxChain);
            hasHit = true;
        }

        if(hasHit && canReact) _status.BeginHitLag(step.HitLagTime);

        // 판정을 모두 마친 뒤 상태를 바꿔, 같은 판정의 다른 적도 에너지 · 게이지 · 경직을 받게 한다.
        if(comboSkillTarget != null) _actionTrigger.BeginComboSkill(comboSkillTarget, comboSkillMaxChain);

        return hasHit;
    }

    // 동작의 판정 모양대로 판정 범위 안의 콜라이더를 찾아 버퍼에 담고, 찾은 수를 돌려준다.
    private int FindHitColliders(ActionStep step)
    {
        switch(step.Shape)
        {
            case ActionStep.HitShape.Sphere:
                return Physics.OverlapSphereNonAlloc(transform.TransformPoint(step.HitCenter), step.HitRadius, _hitColliders, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            case ActionStep.HitShape.Capsule:
                GetCapsulePoints(step, out Vector3 start, out Vector3 end);
                return Physics.OverlapCapsuleNonAlloc(start, end, step.HitRadius, _hitColliders, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            default:
                return Physics.OverlapBoxNonAlloc(transform.TransformPoint(step.HitCenter), step.HitSize * 0.5f, _hitColliders, transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        }
    }

    // 맞은 적 하나에 결과를 적용한다. 체력은 액션의 위력만큼 줄고, 에너지와 필살기 게이지를 얻는다.
    // canReact 이고 액션의 경직 시간이 0보다 크면, 경직 저항 레벨이 액션의 경직 레벨 이하인 적을 경직시킨다.
    // 그로기 게이지는 반응 여부와 상관없이 모든 적중마다 액션의 그로기 값만큼 쌓는다.
    // 강타가 그로기 적에게 맞으면 콤보 스킬 대상으로 고른다. 한 번의 판정에서 콤보 스킬은 하나의 적에게만 발동한다.
    // 그로기를 쌓은 뒤 콤보 스킬을 확인하므로, 게이지를 채운 강타가 바로 콤보 스킬을 발동한다.
    private void ApplyHit(IEnemyDamageable enemy, ActionData action, bool isImpact, bool canReact, ref IEnemyTargetable comboSkillTarget, ref int comboSkillMaxChain)
    {
        enemy.TakeDamage(action.Power, isImpact);
        _status.AddEnergy(action.EnergyGain);
        _status.AddUltimateGauge(action.UltimateGaugeGain);

        // 이 타격으로 처치된 적은 경직 · 콤보 스킬 대상이 아니다. 그로기 적이 강타로 처치되면 콤보 스킬보다 처치가 먼저다.
        if(!enemy.IsAlive) return;

        // 경직 · 그로기 기능(EnemyReaction)이 없는 적은 경직 · 그로기 · 콤보 스킬 대상이 아니다.
        if(canReact && action.StaggerTime > 0f && enemy.transform.TryGetComponent(out IEnemyStaggerable staggerable)) staggerable.Stagger(action.StaggerTime, action.StaggerLevel);

        bool hasGroggy = enemy.transform.TryGetComponent(out IEnemyGroggyable groggyable);

        if(hasGroggy) groggyable.AddGroggy(action.GroggyGain);

        if(!isImpact) return;

        // 콤보 스킬 중의 강타는 새로 발동하지 않고, 콤보 스킬 대상에게 맞았으면 다음 연계를 잇게 한다.
        if(!_actionTrigger.CanStartComboSkill)
        {
            _actionTrigger.OnComboSkillImpactHit(enemy);
            return;
        }

        // 발동 기회는 실제로 발동할 적에게서만 소모한다. 그로기 기능이 없는 적은 콤보 스킬을 받을 수 없다.
        if(comboSkillTarget != null) return;

        if(!hasGroggy || !groggyable.TryConsumeComboSkillChance()) return;

        enemy.transform.TryGetComponent(out comboSkillTarget);
        comboSkillMaxChain = groggyable.MaxComboChain;
    }

    private void CacheComponent()
    {
        _status = GetComponent<PlayerStatus>();
        _actionTrigger = GetComponent<PlayerActionTrigger>();
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어 공격의 타격 판정. 동작의 판정 범위 안에 있는 적을 찾아(범위 찾기), 찾은 적마다 결과를 적용한다(맞은 적 처리).
// 판정 시점은 모션의 애니메이션 이벤트가 정한다.
//   판정 시작 : 구간 판정 동작은 판정을 켜고, 한 순간 판정 동작은 이 순간 한 번 판정한다.
//   판정 끝 : 켜 둔 판정을 끈다.
//   강타 끝 : 이 순간 강타 판정을 한 번 하고 판정을 끈다. 구간의 마지막 타격을 강타로 만들 때 쓴다.
// 구간 판정이 켜져 있는 동안 매 프레임 범위를 찾고, 같은 적은 동작의 간격(캐릭터 시간)마다 최대 횟수까지 맞는다.
// 적중하면 동작의 히트렉 시간 동안 공격한 캐릭터만 멈추도록 상태 기능에 요청한다.
// 동작이 "첫 타격에만 반응"을 켜면, 구간에서 각 적의 첫 타격만 경직 · 히트렉을 건다.
// 범위 찾기 : 동작의 모양(상자 · 구 · 캡슐)대로 캐릭터 기준 범위를 확인한다.
// 맞은 적 처리 : 체력 감소 · 에너지 · 필살기 게이지 획득 · 경직 · 콤보 스킬 발동과 연계.
// 두 과정을 나눠 둔 이유는, 나중에 검기처럼 날아가며 판정하는 공격도 같은 맞은 적 처리를 쓰게 하기 위해서다.
// 범위를 찾을 때와 적을 맞혔을 때 이벤트로 알려, 디버그 기능이 범위 표시 · 적중 로그를 남긴다.
[RequireComponent(typeof(PlayerStatus), typeof(PlayerActionTrigger))]
public class PlayerAttackJudge : MonoBehaviour
{
    // 한 번의 범위 찾기에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_HIT_COLLIDERS = 16;

    // 구간 판정 동안 적 하나가 맞은 횟수와 마지막으로 맞은 캐릭터 시계 시간
    private struct HitRecord
    {
        public int Count;
        public float LastTime;
    }

    private readonly Dictionary<IEnemyDamageable, HitRecord> _hitRecords = new Dictionary<IEnemyDamageable, HitRecord>();
    private readonly HashSet<IEnemyDamageable> _hitTargets = new HashSet<IEnemyDamageable>();
    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    private PlayerStatus _status;
    private PlayerActionTrigger _actionTrigger;
    private PlayerAttackState.AttackType _windowType;
    private ActionData _windowAction;
    private ActionStep _windowStep;

    // 판정 범위를 찾을 때마다 알린다. 전달 값 : 범위를 찾은 동작
    public event Action<ActionStep> RangeChecked;
    // 적을 맞혔을 때마다 알린다. 전달 값 : 공격 종류, 액션, 강타인지
    public event Action<PlayerAttackState.AttackType, ActionData, bool> EnemyHit;

    // 구간 판정이 켜져 있는지
    public bool IsWindowOpen { get; private set; }

    private void Awake() => CacheComponent();
    private void Update() => UpdateWindow();

    // 판정 시작 이벤트. 구간 판정 동작이면 판정을 켜고 바로 한 번 찾는다. 한 순간 판정 동작이면 이 순간 한 번만 판정한다.
    // 판정 범위 · 방식은 동작(step), 위력 · 게이지 같은 타격 값은 액션(action)에서 읽는다.
    public void BeginHit(PlayerAttackState.AttackType type, ActionData action, ActionStep step)
    {
        EndHit();

        if(step.Mode == ActionStep.HitMode.Instant)
        {
            HitInRange(type, action, step, false, false);
            return;
        }

        _windowType = type;
        _windowAction = action;
        _windowStep = step;
        _hitRecords.Clear();
        IsWindowOpen = true;

        UpdateWindow();
    }

    // 판정 끝 이벤트. 켜 둔 구간 판정을 끈다. 상태가 바뀌거나 다음 동작으로 넘어갈 때도 부른다.
    public void EndHit()
    {
        IsWindowOpen = false;
        _windowStep = null;
        _windowAction = null;
    }

    // 강타 끝 이벤트. 이 순간 범위 안의 적에게 강타 판정을 한 번 하고 판정을 끈다.
    // 강타는 구간의 간격 · 최대 횟수와 따로, 범위 안의 적마다 한 번씩 맞고 항상 경직 · 히트렉을 건다.
    // 그로기 적에게 맞으면 판정을 모두 마친 뒤 콤보 스킬을 발동한다. 콤보 스킬 중이면 새로 발동하지 않고, 콤보 스킬 대상에게 맞았을 때 다음 연계를 잇게 한다.
    public void PerformImpact(PlayerAttackState.AttackType type, ActionData action, ActionStep step)
    {
        EndHit();
        HitInRange(type, action, step, true, false);
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

    // 구간 판정이 켜져 있으면 범위 안의 적 중 다시 맞을 수 있는 적만 맞힌다.
    // 히트렉으로 캐릭터가 멈춘 동안은 찾지 않는다. 멈춘 사이 범위로 들어온 적이 맞지 않게 하기 위해서다.
    private void UpdateWindow()
    {
        if(!IsWindowOpen || _status.TimeScale <= 0f) return;

        HitInRange(_windowType, _windowAction, _windowStep, false, true);
    }

    // 범위 안의 적을 찾아 맞힌다. useHitLimit 이면 구간 판정의 간격 · 최대 횟수 · 첫 타격 반응 규칙을 지킨다.
    private void HitInRange(PlayerAttackState.AttackType type, ActionData action, ActionStep step, bool isImpact, bool useHitLimit)
    {
        int count = FindHitColliders(step);
        IEnemyTargetable comboSkillTarget = null;
        int comboSkillMaxChain = 0;
        bool hasReactHit = false;

        RangeChecked?.Invoke(step);

        // 콜라이더가 여러 개인 적도 한 번의 찾기에서 한 번만 맞도록 이번에 맞은 적을 기억한다.
        _hitTargets.Clear();

        for(int i = 0; i < count; i++)
        {
            IEnemyDamageable enemy = _hitColliders[i].GetComponentInParent<IEnemyDamageable>();

            if(enemy == null || !_hitTargets.Add(enemy)) continue;

            int hitCount = 1;

            if(useHitLimit && !TryRecordHit(enemy, step, out hitCount)) continue;

            // 첫 타격에만 반응하는 동작이면 그 적의 두 번째 타격부터는 경직 · 히트렉을 걸지 않는다.
            bool canReact = !step.ReactOnFirstHitOnly || hitCount == 1;

            ApplyHit(enemy, action, isImpact, canReact, ref comboSkillTarget, ref comboSkillMaxChain);
            EnemyHit?.Invoke(type, action, isImpact);
            hasReactHit |= canReact;
        }

        // 한 번의 찾기에서 여러 적을 맞혀도 히트렉은 한 번만 건다.
        if(hasReactHit) _status.BeginHitLag(step.HitLagTime);

        // 판정을 모두 마친 뒤 상태를 바꿔, 같은 판정의 다른 적도 에너지 · 게이지 · 경직을 받게 한다.
        if(comboSkillTarget != null) _actionTrigger.BeginComboSkill(comboSkillTarget, comboSkillMaxChain);
    }

    // 구간 판정에서 이 적을 지금 맞힐 수 있으면 횟수 · 시간을 기록하고 true 를 돌려준다. hitCount 는 이번이 몇 번째 타격인지다.
    // 최대 횟수에 닿았거나, 마지막으로 맞은 뒤 간격(캐릭터 시간)이 지나지 않았으면 false 다.
    private bool TryRecordHit(IEnemyDamageable enemy, ActionStep step, out int hitCount)
    {
        float now = _status.LocalTime;

        hitCount = 0;

        if(_hitRecords.TryGetValue(enemy, out HitRecord record))
        {
            if(record.Count >= step.MaxHitsPerTarget || now - record.LastTime < step.HitInterval) return false;
        }

        record.Count++;
        record.LastTime = now;
        _hitRecords[enemy] = record;
        hitCount = record.Count;
        return true;
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
    // 강타가 그로기 적에게 맞으면 콤보 스킬 대상으로 고른다. 한 번의 판정에서 콤보 스킬은 하나의 적에게만 발동한다.
    private void ApplyHit(IEnemyDamageable enemy, ActionData action, bool isImpact, bool canReact, ref IEnemyTargetable comboSkillTarget, ref int comboSkillMaxChain)
    {
        enemy.TakeDamage(action.Power, isImpact);
        _status.AddEnergy(action.EnergyGain);
        _status.AddUltimateGauge(action.UltimateGaugeGain);

        // 이 타격으로 처치된 적은 경직 · 콤보 스킬 대상이 아니다. 그로기 적이 강타로 처치되면 콤보 스킬보다 처치가 먼저다.
        if(!enemy.IsAlive) return;

        // 경직 · 그로기 기능(EnemyReaction)이 없는 적은 경직 · 그로기 · 콤보 스킬 대상이 아니다.
        if(canReact && action.StaggerTime > 0f && enemy.transform.TryGetComponent(out IEnemyStaggerable staggerable)) staggerable.Stagger(action.StaggerTime, action.StaggerLevel);

        if(!isImpact) return;

        // 콤보 스킬 중의 강타는 새로 발동하지 않고, 콤보 스킬 대상에게 맞았으면 다음 연계를 잇게 한다.
        if(!_actionTrigger.CanStartComboSkill)
        {
            _actionTrigger.OnComboSkillImpactHit(enemy);
            return;
        }

        // 발동 기회는 실제로 발동할 적에게서만 소모한다. 그로기 기능이 없는 적은 콤보 스킬을 받을 수 없다.
        if(comboSkillTarget != null) return;

        if(!enemy.transform.TryGetComponent(out IEnemyGroggyable groggyable) || !groggyable.TryConsumeComboSkillChance()) return;

        enemy.transform.TryGetComponent(out comboSkillTarget);
        comboSkillMaxChain = groggyable.MaxComboChain;
    }

    private void CacheComponent()
    {
        _status = GetComponent<PlayerStatus>();
        _actionTrigger = GetComponent<PlayerActionTrigger>();
    }
}

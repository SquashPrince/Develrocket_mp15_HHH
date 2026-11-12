using System.Collections.Generic;
using UnityEngine;

// 플레이어 공격의 타격 판정. 공격 설정의 판정 범위 안에 있는 적을 찾아(범위 찾기), 찾은 적마다 결과를 적용한다(맞은 적 처리).
// 범위 찾기 : 공격 설정의 모양(기본 상자 · 상자 · 구 · 캡슐)대로 캐릭터 기준 범위를 확인한다.
// 맞은 적 처리 : 체력 감소 · 에너지 · 필살기 게이지 획득 · 경직 · 콤보 스킬 발동과 연계.
// 두 과정을 나눠 둔 이유는, 나중에 검기처럼 날아가며 판정하는 공격도 같은 맞은 적 처리를 쓰게 하기 위해서다.
[RequireComponent(typeof(PlayerEnergy), typeof(PlayerUltimate), typeof(PlayerComboSkill))]
public class PlayerAttackJudge : MonoBehaviour
{
    // 한 번의 타격 판정에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_HIT_COLLIDERS = 16;

    [Header("기본 판정 범위")]
    [Tooltip("판정 모양이 기본인 공격이 쓰는 판정 상자의 중심 (캐릭터 기준 위치)")]
    [SerializeField] private Vector3 _defaultHitCenter = new Vector3(0f, 1f, 1f);
    [Tooltip("판정 모양이 기본인 공격이 쓰는 판정 상자의 크기 (좌우 · 위아래 · 앞뒤)")]
    [SerializeField] private Vector3 _defaultHitSize = new Vector3(1.5f, 1.5f, 2f);

    private readonly HashSet<IEnemyDamageable> _hitTargets = new HashSet<IEnemyDamageable>();
    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    private PlayerEnergy _energy;
    private PlayerUltimate _ultimate;
    private PlayerComboSkill _comboSkill;

    private void Awake() => CacheComponent();
    private void OnDrawGizmosSelected() => DrawDefaultHitBox();

    // 공격 설정의 판정 범위 안에 있는 적에게 한 번씩 적중시킨다.
    // isImpact : 강타 신호로 판정했는지. 판정은 일반 타격과 같고, 그로기 적에게 맞으면 판정이 끝난 뒤 콤보 스킬을 발동한다.
    //            콤보 스킬 중이면 새로 발동하지 않고, 콤보 스킬 대상에게 맞았을 때 다음 연계를 잇게 한다.
    public void PerformHit(PlayerAttackState.AttackType type, PlayerAttackSetting setting, bool isImpact = false)
    {
        int count = FindHitColliders(setting);

        ApplyHits(count, type, setting, isImpact);
    }

    // 공격 설정의 모양대로 판정 범위 안의 콜라이더를 찾아 버퍼에 담고, 찾은 수를 돌려준다.
    private int FindHitColliders(PlayerAttackSetting setting)
    {
        switch(setting.Shape)
        {
            case PlayerAttackSetting.HitShape.Box:
                return Physics.OverlapBoxNonAlloc(transform.TransformPoint(setting.HitCenter), setting.HitSize * 0.5f, _hitColliders, transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            case PlayerAttackSetting.HitShape.Sphere:
                return Physics.OverlapSphereNonAlloc(transform.TransformPoint(setting.HitCenter), setting.HitRadius, _hitColliders, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            case PlayerAttackSetting.HitShape.Capsule:
                GetCapsulePoints(setting, out Vector3 start, out Vector3 end);
                return Physics.OverlapCapsuleNonAlloc(start, end, setting.HitRadius, _hitColliders, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            default:
                return Physics.OverlapBoxNonAlloc(transform.TransformPoint(_defaultHitCenter), _defaultHitSize * 0.5f, _hitColliders, transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        }
    }

    // 버퍼에 담긴 콜라이더 중 적에게 결과를 적용한다. 맞은 적의 체력은 공격 설정의 위력만큼 줄고, 적중한 적마다 에너지와 필살기 게이지를 얻는다.
    // 공격 설정의 경직 시간이 0보다 크면, 맞은 적 중 경직 저항 레벨이 공격의 경직 레벨 이하인 적을 경직시킨다.
    private void ApplyHits(int count, PlayerAttackState.AttackType type, PlayerAttackSetting setting, bool isImpact)
    {
        IEnemyTargetable comboSkillTarget = null;
        int comboSkillMaxChain = 0;

        // 콜라이더가 여러 개인 적도 한 번만 맞도록 이번 판정에서 맞은 적을 기억한다.
        _hitTargets.Clear();

        for(int i = 0; i < count; i++)
        {
            IEnemyDamageable enemy = _hitColliders[i].GetComponentInParent<IEnemyDamageable>();

            if(enemy == null || !_hitTargets.Add(enemy)) continue;

            enemy.TakeDamage(setting.Power, isImpact);
            _energy.Add(setting.EnergyGain);

#if UNITY_EDITOR
            Debug.Log($"[{(isImpact ? "강타" : "적중")}] {type} 위력 {setting.Power} / 에너지 +{setting.EnergyGain} → {_energy.CurrentEnergy:F1}");
#endif

            _ultimate.AddGauge(setting.UltimateGaugeGain);

            // 이 타격으로 처치된 적은 경직 · 콤보 스킬 대상이 아니다. 그로기 적이 강타로 처치되면 콤보 스킬보다 처치가 먼저다.
            if(!enemy.IsAlive) continue;

            // 경직 · 그로기 기능(EnemyReaction)이 없는 적은 경직 · 그로기 · 콤보 스킬 대상이 아니다.
            if(setting.StaggerTime > 0f && enemy.transform.TryGetComponent(out IEnemyStaggerable staggerable)) staggerable.Stagger(setting.StaggerTime, setting.StaggerLevel);

            if(!isImpact) continue;

            // 콤보 스킬 중의 강타는 새로 발동하지 않고, 콤보 스킬 대상에게 맞았으면 다음 연계를 잇게 한다.
            if(!_comboSkill.CanStart)
            {
                _comboSkill.OnImpactHit(enemy);
                continue;
            }

            // 한 번의 판정에서 콤보 스킬은 하나의 그로기 적에게만 발동한다. 발동 기회는 실제로 발동할 적에게서만 소모한다.
            // 그로기 기능이 없는 적은 콤보 스킬을 받을 수 없다.
            if(comboSkillTarget != null) continue;

            if(!enemy.transform.TryGetComponent(out IEnemyGroggyable groggyable) || !groggyable.TryConsumeComboSkillChance()) continue;

            enemy.transform.TryGetComponent(out comboSkillTarget);
            comboSkillMaxChain = groggyable.MaxComboChain;
        }

        // 판정을 모두 마친 뒤 상태를 바꿔, 같은 판정의 다른 적도 에너지 · 게이지 · 경직을 받게 한다.
        if(comboSkillTarget != null) _comboSkill.Begin(comboSkillTarget, comboSkillMaxChain);
    }

    // 캡슐의 양 끝 구 중심. 캐릭터 앞쪽 방향으로 전체 길이만큼 뻗고, 길이가 반지름의 두 배보다 짧으면 두 점이 겹쳐 구가 된다.
    private void GetCapsulePoints(PlayerAttackSetting setting, out Vector3 start, out Vector3 end)
    {
        Vector3 center = transform.TransformPoint(setting.HitCenter);
        float halfSegment = Mathf.Max(0f, setting.HitLength * 0.5f - setting.HitRadius);

        start = center - transform.forward * halfSegment;
        end = center + transform.forward * halfSegment;
    }

    // Scene 창에서 플레이어를 선택하면 기본 판정 범위를 빨간 상자로 보여준다. 공격별 범위 표시는 공격 데이터 에셋에서 한다.
    private void DrawDefaultHitBox()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(_defaultHitCenter), transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, _defaultHitSize);
    }

    private void CacheComponent()
    {
        _energy = GetComponent<PlayerEnergy>();
        _ultimate = GetComponent<PlayerUltimate>();
        _comboSkill = GetComponent<PlayerComboSkill>();
    }
}

using UnityEngine;

// 콤보 스킬(체인 스킬) 수치와 발동. 그로기 적에게 강타가 맞으면 타격 판정이 발동을 요청한다. 그로기마다 한 번 발동한다.
// 한 번의 콤보 스킬은 필살기처럼 단계(동작) 여러 개를 순서대로 이어 쓴다. 단계 수와 연계 수는 관계없다.
// 콤보 스킬 중 대상에게 강타가 맞으면 다음 연계를 잇는다. 최대 연계 수는 대상 적이 정한다.
// 발동할 때 적 둘레로 비켜 서는 순간 이동은 콤보 스킬 액션의 순간 이동 옵션이 정한다.
// 코어 전투에서는 1인이라 같은 콤보 스킬을 다시 쓴다. 파티 전투에서는 다음 캐릭터가 이어받도록 확장한다.
[RequireComponent(typeof(PlayerController))]
public class PlayerComboSkill : MonoBehaviour
{
    private PlayerController _controller;
    private PlayerComboSkillState _comboSkillState;

    public ActionData Action => _controller.Character.ComboSkill;
    // 콤보 스킬 중에는 다른 그로기 적에게 강타가 맞아도 다시 발동하지 않는다.
    public bool CanStart => !_comboSkillState.IsActive;

    private void Awake() => CacheComponent();
    private void Start() => Init();

    // 그로기 적을 대상으로 콤보 스킬 상태로 넘긴다. 진행 중이던 공격 · 필살기는 끊긴다.
    // maxChain : 대상 적의 최대 연계 수
    public void Begin(IEnemyTargetable target, int maxChain)
    {
#if UNITY_EDITOR
        Debug.Log($"[콤보 스킬] {target.transform.name} (최대 연계 {maxChain})");
#endif

        _comboSkillState.Setup(target, maxChain);
        _controller.EnterState(_comboSkillState);
    }

    // 콤보 스킬 중 강타가 맞은 적을 알린다. 콤보 스킬 대상이면 다음 연계를 잇는다.
    public void OnImpactHit(IEnemyDamageable enemy) => _comboSkillState.QueueChain(enemy.transform);

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
    }

    private void Init()
    {
        _comboSkillState = new PlayerComboSkillState(_controller, this);
    }
}

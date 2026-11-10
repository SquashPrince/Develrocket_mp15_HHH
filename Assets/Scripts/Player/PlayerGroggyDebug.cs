using UnityEngine;
using UnityEngine.InputSystem;

// [코어 전투 테스트] 그로기 테스트 입력. 누르면 록온 대상, 없으면 자동 조준 대상을 그로기로 만든다.
// 그로기 게이지가 없는 코어 전투에서 콤보 스킬을 시험하기 위한 기능이다. 파티 전투에서 그로기 게이지를 만들면 지운다.
[RequireComponent(typeof(PlayerTargeting))]
public class PlayerGroggyDebug : MonoBehaviour
{
    [SerializeField] private InputActionReference _groggyAction;

    private PlayerTargeting _targeting;

    private void Awake() => CacheComponent();
    private void Update() => TryEnterGroggy();

    // 공격하지 않는 더미 적은 공격 기능이 없어 그로기가 될 수 없으므로 무시한다.
    private void TryEnterGroggy()
    {
        if(!_groggyAction.action.WasPressedThisFrame()) return;

        DummyEnemy target = _targeting.LockTarget != null ? _targeting.LockTarget : _targeting.FindTarget(_targeting.AttackRange);

        if(target == null || !target.TryGetComponent(out DummyEnemyAttacker attacker)) return;

        attacker.EnterGroggy();
    }

    private void CacheComponent()
    {
        _targeting = GetComponent<PlayerTargeting>();
    }
}

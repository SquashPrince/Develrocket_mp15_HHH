using UnityEngine;
using UnityEngine.InputSystem;

// 패링 입력과 수치. 적의 패링 가능 시간이 열려 있을 때 패링 키를 누르면 어느 상태에서든 패링 상태로 넘긴다.
// 코어 전투에서는 이전 · 다음 캐릭터 키 모두 현재 캐릭터가 직접 패링한다. 교체 방향 구분은 파티 전투 단계에서 한다.
// 이미 패링 중이거나, 시전 중 무적인 공격(패링 추격 포함)을 휘두르는 중이면 입력을 무시한다.
// 패링 가능 시간 밖의 입력도 무시한다. 파티 전투에서는 이 입력이 그냥 교체가 된다.
// 패링을 시작하면 패링 공격을 하는 적 앞으로 순간 이동한다. 실제로 이동하면 벽 등에 걸리기 때문이다. 도착 지점이 막혀 있으면 제자리에서 패링한다.
// 패링 공격을 하는 적이 여럿이면 공격 대상 기준(록온 대상 → 자동 조준 대상)을 따르고, 둘 다 아니면 가장 최근에 예고를 시작한 적을 고른다.
[RequireComponent(typeof(PlayerController), typeof(CharacterController), typeof(PlayerTargeting))]
[RequireComponent(typeof(PlayerUltimate))]
public class PlayerParry : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] private InputActionReference _parryPreviousAction;
    [SerializeField] private InputActionReference _parryNextAction;

    [Header("패링")]
    [Tooltip("패링 준비 자세를 유지하는 시간(초). 적 공격이 끊겨도 끝까지 유지한다")]
    [SerializeField, Min(0.01f)] private float _readyTime = 1f;
    [Tooltip("패링을 시작할 때 패링 공격을 하는 적의 정면으로 이 거리만큼 떨어진 곳에 순간 이동한다")]
    [SerializeField, Min(0f)] private float _teleportDistance = 1.5f;
    [Tooltip("순간 이동할 자리가 막혀 있는지 검사할 Layer. Player · Enemy 는 빼고 벽 · 지형만 넣는다")]
    [SerializeField] private LayerMask _teleportBlockLayers = 1;
    [Tooltip("패링 성공 후 공격 반대 방향으로 밀려나는 거리")]
    [SerializeField, Min(0f)] private float _reactDistance = 2f;
    [Tooltip("패링 성공 후 반응 시간(초). 이 시간에 걸쳐 점점 느려지며 밀려나고, 이 동안 닿은 노란 공격은 자동으로 패링된다. 이 동안 공격 키를 누르면 끝난 뒤 추격 공격이 나간다")]
    [SerializeField, Min(0.01f)] private float _reactTime = 1f;

    [Header("패링 추격")]
    [Tooltip("반응 중 공격 입력으로 나가는 추격 공격. 시전 중 무적을 켜 두어야 기획대로 추격 중 무적이 된다")]
    [SerializeField] private PlayerAttackSetting _parryChase = new PlayerAttackSetting();

    private PlayerController _controller;
    private CharacterController _characterController;
    private PlayerTargeting _targeting;
    private PlayerUltimate _ultimate;
    private PlayerParryState _parryState;

    public float ReadyTime => _readyTime;
    public float ReactDistance => _reactDistance;
    public float ReactTime => _reactTime;
    public PlayerAttackSetting ParryChase => _parryChase;

    private bool WasParryPressed => _parryPreviousAction.action.WasPressedThisFrame() || _parryNextAction.action.WasPressedThisFrame();

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update() => TryStartParry();

    // 패링 준비 · 반응 중 노란 공격이 닿으면 패링 성공으로 처리하고 true 를 돌려준다. 체력 기능이 맞았을 때 부른다.
    public bool TryParry(EnemyAttackSetting attack, DummyEnemyAttacker attacker)
    {
        if(!_parryState.IsActive || !attack.IsParryable) return false;

#if UNITY_EDITOR
        Debug.Log($"[패링 성공] {attack.Name}");
#endif

        attacker.Stagger(attack.ParryStaggerTime);
        _ultimate.AddParryGauge();
        _parryState.OnParrySuccess(attacker.transform.position);
        return true;
    }

    // 패링 공격을 하는 적의 정면, 정해진 거리만큼 떨어진 곳으로 순간 이동하고 적 쪽을 본다.
    // 적은 예고를 시작할 때 플레이어 쪽으로 돌기 때문에, 적의 정면은 플레이어가 있던 쪽이다.
    // 도착 높이는 적의 기준점 높이를 쓰므로, 적의 기준점은 플레이어처럼 발바닥에 있어야 한다.
    // 도착 지점이 벽 · 지형에 겹치거나, 적과 도착 지점 사이가 막혀 있으면 이동하지 않고 제자리에서 패링한다.
    public void TeleportToAttacker()
    {
        DummyEnemyAttacker attacker = SelectAttacker();

        if(attacker == null) return;

        Vector3 attackerPosition = attacker.transform.position;
        Vector3 destination = attackerPosition + attacker.transform.forward * _teleportDistance;

        if(IsBlocked(attackerPosition, destination)) return;

        // CharacterController 가 켜져 있으면 위치를 직접 바꿔도 덮어쓰므로 잠깐 끄고 옮긴다.
        _characterController.enabled = false;
        transform.position = destination;
        _characterController.enabled = true;

        Vector3 toAttacker = attackerPosition - destination;
        toAttacker.y = 0f;
        _controller.FaceImmediately(toAttacker);
    }

    // 패링 가능 시간을 연 적 중에서 공격 대상 기준으로 고른다. 록온 대상 → 자동 조준 대상 → 가장 최근에 예고를 시작한 적 순서다.
    private DummyEnemyAttacker SelectAttacker()
    {
        DummyEnemyAttacker lockAttacker = GetOpenAttacker(_targeting.LockTarget);

        if(lockAttacker != null) return lockAttacker;

        DummyEnemyAttacker aimAttacker = GetOpenAttacker(_targeting.FindTarget(_targeting.AttackRange));

        return aimAttacker != null ? aimAttacker : ParryWindow.LatestAttacker;
    }

    // 대상이 패링 가능 시간을 연 적이면 그 공격 기능을, 아니면 null 을 돌려준다.
    private static DummyEnemyAttacker GetOpenAttacker(DummyEnemy target)
    {
        if(target == null) return null;

        DummyEnemyAttacker attacker = target.GetComponent<DummyEnemyAttacker>();

        return attacker != null && ParryWindow.IsOpenBy(attacker) ? attacker : null;
    }


    private void TryStartParry()
    {
        if(!WasParryPressed || !ParryWindow.IsOpen) return;
        if(_parryState.IsActive || _controller.Health.IsAttackInvincible) return;

        _controller.EnterState(_parryState);
    }

    // 도착 지점에 플레이어 몸(캡슐)이 들어갈 자리가 있는지, 적에서 도착 지점까지 벽이 가로막지 않는지 본다.
    // 캡슐은 바닥에 닿은 것을 겹침으로 보지 않도록 피부 두께만큼 띄워서 검사한다.
    private bool IsBlocked(Vector3 attackerPosition, Vector3 destination)
    {
        Vector3 center = destination + _characterController.center + Vector3.up * _characterController.skinWidth;
        float halfHeight = Mathf.Max(0f, _characterController.height * 0.5f - _characterController.radius);
        Vector3 bottom = center - Vector3.up * halfHeight;
        Vector3 top = center + Vector3.up * halfHeight;

        if(Physics.CheckCapsule(bottom, top, _characterController.radius, _teleportBlockLayers, QueryTriggerInteraction.Ignore)) return true;

        return Physics.Linecast(attackerPosition + _characterController.center, center, _teleportBlockLayers, QueryTriggerInteraction.Ignore);
    }

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _characterController = GetComponent<CharacterController>();
        _targeting = GetComponent<PlayerTargeting>();
        _ultimate = GetComponent<PlayerUltimate>();
    }

    private void Init()
    {
        _parryState = new PlayerParryState(_controller, this);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어 캐릭터의 입력을 한곳에서 읽는다. 다른 플레이어 기능은 입력 장치를 직접 읽지 않고 여기서 결과만 읽는다.
// 조작 중이 아니면 모든 입력이 눌리지 않은 것으로 보인다.
//   파티 전투에서 여러 캐릭터가 필드에 있어도, 조작 중인 캐릭터만 입력에 반응하게 하기 위해서다.
// 시점 · 일시정지 같은 카메라 · 화면 입력은 캐릭터 입력이 아니므로 여기서 읽지 않는다.
public class PlayerInputReader : MonoBehaviour
{
    [Tooltip("조작 중인 캐릭터인지. 끄면 이 캐릭터는 입력에 반응하지 않는다. 파티 전투에서 교체 기능이 바꾼다")]
    [SerializeField] private bool _isControlled = true;

    [Header("이동 · 행동")]
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private InputActionReference _dashAction;
    [SerializeField] private InputActionReference _attackAction;
    [SerializeField] private InputActionReference _skillAction;
    [SerializeField] private InputActionReference _ultimateAction;

    [Header("패링 · 교체")]
    [SerializeField] private InputActionReference _parryPreviousAction;
    [SerializeField] private InputActionReference _parryNextAction;

    [Header("타겟팅")]
    [SerializeField] private InputActionReference _lockOnAction;

    [Header("테스트")]
    [Tooltip("[코어 전투 테스트] 그로기 테스트 입력. 그로기 게이지를 만들면 지운다")]
    [SerializeField] private InputActionReference _groggyDebugAction;

    public bool IsControlled
    {
        get => _isControlled;
        set => _isControlled = value;
    }

    public Vector2 MoveInput => _isControlled ? _moveAction.action.ReadValue<Vector2>() : Vector2.zero;
    public bool WasDashPressed => WasPressed(_dashAction);
    public bool WasAttackPressed => WasPressed(_attackAction);
    public bool IsAttackHeld => _isControlled && _attackAction.action.IsPressed();
    public bool WasSkillPressed => WasPressed(_skillAction);
    public bool WasUltimatePressed => WasPressed(_ultimateAction);
    // 패링 두 키는 지금은 같은 동작이다. 파티 전투에서 교체 방향을 나눌 때 각각 쓴다.
    public bool WasParryPreviousPressed => WasPressed(_parryPreviousAction);
    public bool WasParryNextPressed => WasPressed(_parryNextAction);
    public bool WasParryPressed => WasParryPreviousPressed || WasParryNextPressed;
    public bool WasLockOnPressed => WasPressed(_lockOnAction);
    public bool WasGroggyDebugPressed => WasPressed(_groggyDebugAction);

    private bool WasPressed(InputActionReference actionReference) => _isControlled && actionReference.action.WasPressedThisFrame();
}

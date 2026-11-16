using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어 캐릭터의 입력을 한곳에서 읽는다. 다른 플레이어 기능은 입력 장치를 직접 읽지 않고 여기서 결과만 읽는다.
// 조작 중이 아니면 모든 입력이 눌리지 않은 것으로 보인다.
//   파티 전투에서 여러 캐릭터가 필드에 있어도, 조작 중인 캐릭터만 입력에 반응하게 하기 위해서다.
// 입력 액션은 모든 캐릭터가 같이 쓰는 공통 설정에서 읽는다. 조작 방법의 기본은 캐릭터가 바뀌어도 같기 때문이다.
// 시점 · 일시정지 같은 카메라 · 화면 입력은 캐릭터 입력이 아니므로 여기서 읽지 않는다.
[RequireComponent(typeof(PlayerController))]
public class PlayerInputReader : MonoBehaviour
{
    [Tooltip("조작 중인 캐릭터인지. 끄면 이 캐릭터는 입력에 반응하지 않는다. 실행 중에는 파티가 조작 캐릭터를 정할 때 바꾼다")]
    [SerializeField] private bool _isControlled = true;

    private PlayerController _controller;

    public bool IsControlled
    {
        get => _isControlled;
        set => _isControlled = value;
    }

    public Vector2 MoveInput => CanRead ? Common.MoveAction.action.ReadValue<Vector2>() : Vector2.zero;
    public bool WasDashPressed => WasPressed(Common.DashAction);
    public bool WasAttackPressed => WasPressed(Common.AttackAction);
    public bool IsAttackHeld => CanRead && Common.AttackAction.action.IsPressed();
    public bool WasSkillPressed => WasPressed(Common.SkillAction);
    public bool WasUltimatePressed => WasPressed(Common.UltimateAction);
    private CommonCharacterSettings Common => _controller.Common;
    // 조작 중이고, 파티가 콤보 스킬 캐릭터 선택으로 입력을 막고 있지 않을 때만 입력을 읽는다.
    private bool CanRead => _isControlled && !_controller.Party.IsInputBlocked;

    private void Awake() => CacheComponent();

    private bool WasPressed(InputActionReference actionReference) => CanRead && actionReference.action.WasPressedThisFrame();

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
    }
}

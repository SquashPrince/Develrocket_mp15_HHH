using UnityEngine;
using UnityEngine.InputSystem;

// 플레이 시작 시 커서를 잠그고, Pause 입력(Esc · 게임패드 Start)마다 잠금과 해제를 번갈아 바꾼다.
public class CursorLock : MonoBehaviour
{
    [SerializeField] private InputActionReference _pauseAction;

    // 에디터는 Esc 를 누르면 스스로 커서 잠금을 풀기 때문에, Cursor.lockState 대신 직접 상태를 기억한다.
    private bool _isLocked;

    private void Start() => Init();
    private void Update() => ToggleOnPause();

    private void ToggleOnPause()
    {
        if(!_pauseAction.action.WasPressedThisFrame()) return;

        SetLocked(!_isLocked);
    }

    private void SetLocked(bool isLocked)
    {
        _isLocked = isLocked;
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }

    private void Init()
    {
        SetLocked(true);
    }
}

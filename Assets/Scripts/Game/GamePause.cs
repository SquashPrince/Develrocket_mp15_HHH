using UnityEngine;
using UnityEngine.InputSystem;

// 일시정지. 일시정지 입력(Esc · 게임패드 Start)으로 게임을 멈추고, 다시 누르면 이어간다. 메뉴 화면은 알림을 받아 열고 닫는다.
// 멈출 때 게임 시간 배율을 0 으로 두고, 이어갈 때 1 로 되돌린다.
// 멈출 수 없는 때 : 게임 시간 배율이 1 이 아닐 때(히트스톱 · 콤보 선택 중)와 결과 화면이 뜬 뒤.
//   히트스톱 · 콤보 선택은 끝날 때 시간 배율을 되돌리므로, 그 사이에 멈추면 메뉴가 열린 채 게임이 다시 움직이기 때문이다.
//   히트스톱은 아주 짧고 콤보 선택은 몇 초 안에 끝나 입력을 무시해도 거의 느껴지지 않는다. 멈춘 동안에는 둘 다 새로 시작될 수 없다.
//   완벽 회피 슬로모션은 적 전용 시간을 쓰므로 멈출 수 있다.
// 커서 : 게임 씬은 시작할 때 커서를 잠그고 숨긴다. 멈추면 풀어 보이고, 이어가면 다시 잠근다.
//   씬을 떠날 때(재시작 · 타이틀) 풀어 둔다. 타이틀은 커서가 보여야 하고, 다시 불러온 게임 씬은 시작할 때 다시 잠근다.
// 카메라 : 멈춘 동안 카메라 입력 컨트롤러(Cinemachine Input Axis Controller)를 끈다. Cinemachine 이 게임 시간 배율을 무시하게 설정되어 있어, 멈춘 동안에도 마우스로 카메라가 움직이기 때문이다.
//   입력 동작만 끄면 컨트롤러가 스스로 다시 켤 수 있어(Auto Enable Inputs) 컨트롤러를 끈다.
// 플레이어 입력(이동 · 공격 · 교체 · 록온)은 파티가 멈춤 여부를 보고 막는다.
public class GamePause : MonoBehaviour
{
    [SerializeField] private InputActionReference _pauseAction;
    [Tooltip("멈춘 동안 끌 카메라 입력 컨트롤러. 일반 · 록온 카메라의 Cinemachine Input Axis Controller 를 연결한다")]
    [SerializeField] private Behaviour[] _cameraInputControllers;
    [SerializeField] private GameFlow _gameFlow;

    // 지금 멈춰 있는지. 파티가 플레이어 입력을 막는 데 쓴다. 씬에 하나만 둔다.
    public static bool IsPaused { get; private set; }

    // 멈추거나 이어갈 때 알린다. true 면 멈춤이다.
    public event System.Action<bool> PausedChanged;

    private void Start() => Init();
    private void Update() => ReadPauseInput();
    // 씬을 떠날 때 멈춤 상태와 커서를 처음으로 돌린다. 시간 배율은 게임 흐름이 씬을 바꾸기 전에 돌린다.
    private void OnDestroy()
    {
        IsPaused = false;
        SetCursorLocked(false);
    }

    public void Pause()
    {
        if(IsPaused || !CanPause()) return;

        SetPaused(true);
    }

    // 메뉴의 계속하기 버튼도 부른다.
    public void Resume()
    {
        if(!IsPaused) return;

        SetPaused(false);
    }

    private void ReadPauseInput()
    {
        if(!_pauseAction.action.WasPressedThisFrame()) return;

        if(IsPaused) Resume();
        else Pause();
    }

    private bool CanPause() => Time.timeScale == 1f && !_gameFlow.IsEnded;

    private void SetPaused(bool isPaused)
    {
        IsPaused = isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        SetCameraInputEnabled(!isPaused);
        SetCursorLocked(!isPaused);
        PausedChanged?.Invoke(isPaused);
    }

    private void SetCameraInputEnabled(bool isEnabled)
    {
        foreach(Behaviour controller in _cameraInputControllers)
        {
            controller.enabled = isEnabled;
        }
    }

    private static void SetCursorLocked(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }

    private void Init()
    {
        SetCursorLocked(true);
    }

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 멈춤 상태가 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad() => IsPaused = false;
}

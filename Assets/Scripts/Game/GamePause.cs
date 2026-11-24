using UnityEngine;
using UnityEngine.InputSystem;

// 일시정지. 게임 시간 배율을 0 으로 두고 카메라 입력 · 커서를 함께 다룬다. 메뉴 화면은 알림을 받아 열고 닫는다.
// 플레이어 입력은 파티가 멈춤 여부를 보고 막는다.
public class GamePause : MonoBehaviour
{
    [SerializeField] private InputActionReference _pauseAction;
    [Tooltip("멈춘 동안 끌 카메라 입력 컨트롤러. 일반 · 록온 카메라의 Cinemachine Input Axis Controller 를 연결한다")]
    [SerializeField] private Behaviour[] _cameraInputControllers;
    [SerializeField] private GameFlow _gameFlow;

    // 씬에 하나만 둔다.
    public static bool IsPaused { get; private set; }

    public event System.Action<bool> PausedChanged;

    private void Start() => Init();
    private void OnEnable() => BindGameFlowEvent();
    private void OnDisable() => UnbindGameFlowEvent();
    private void Update() => ReadPauseInput();
    // 타이틀은 커서가 보여야 하므로 씬을 떠날 때 풀어 둔다. 시간 배율은 게임 흐름이 씬을 바꾸기 전에 돌린다.
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

    // 히트스톱 · 콤보 선택은 끝날 때 시간 배율을 되돌리므로, 그 사이(배율 1 이 아닐 때) 멈추면 메뉴가 열린 채 게임이 움직인다.
    // 로딩 중에 멈추면 화면이 보였을 때 멈춰 있으므로 막는다.
    private bool CanPause() => Time.timeScale == 1f && !_gameFlow.IsEnded && !CommonUI.Instance.SceneLoader.IsLoading;

    private void SetPaused(bool isPaused)
    {
        IsPaused = isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        SetCameraInputEnabled(!isPaused);
        SetCursorLocked(!isPaused);
        PausedChanged?.Invoke(isPaused);
    }

    // Cinemachine 이 게임 시간 배율을 무시해 멈춘 동안에도 카메라가 돌므로 끈다.
    // 입력 동작만 끄면 컨트롤러가 스스로 다시 켤 수 있어(Auto Enable Inputs) 컨트롤러를 끈다.
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

    // 끝난 뒤에는 일시정지할 수 없어 다시 켜지지 않는다.
    private void OnRunEnded() => SetCameraInputEnabled(false);

    private void BindGameFlowEvent()
    {
        _gameFlow.RunEnded += OnRunEnded;
    }

    private void UnbindGameFlowEvent()
    {
        _gameFlow.RunEnded -= OnRunEnded;
    }

    private void Init()
    {
        SetCursorLocked(true);
    }

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 멈춤 상태가 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad() => IsPaused = false;
}

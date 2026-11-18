using UnityEngine;

// 씬이 바뀌어도 유지되는 공용 UI. 설정 화면처럼 어느 씬에서나 쓰는 화면과 볼륨 반영 · EventSystem 을 담는다.
// 공용 UI 프리팹을 씬마다 놓아 둔다. 처음 생긴 하나만 남겨 씬 전환에도 유지하고, 다음 씬에 놓인 것은 바로 지운다.
//   어느 씬을 먼저 열고 플레이해도 공용 UI 가 있게 하기 위해서다.
// 씬에 놓인 복사본은 지워질 수 있으므로, 씬 오브젝트는 Inspector 로 연결하지 않고 Instance 로 찾는다.
// 다른 기능이 Awake · OnEnable 에서 찾을 수 있도록 먼저 실행한다.
// 게임 씬 전용 화면(Esc 메뉴 · 결과 화면 · 전투 HUD)은 게임 흐름 · 파티를 직접 연결하므로 게임 씬에 둔다.
[DefaultExecutionOrder(-100)]
public class CommonUI : MonoBehaviour
{
    [SerializeField] private SettingsView _settings;

    public static CommonUI Instance { get; private set; }

    public SettingsView Settings => _settings;

    private void Awake() => Init();
    private void OnDestroy()
    {
        if(Instance == this) Instance = null;
    }

    // 이미 남아 있는 공용 UI 가 있으면 자신을 지운다. 지우는 것은 프레임 끝이라, 그 전에 꺼 두어 자식 화면이 동작하지 않게 한다.
    private void Init()
    {
        if(Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}

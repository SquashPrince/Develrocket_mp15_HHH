using UnityEngine;

// 씬이 바뀌어도 유지되는 공용 UI(설정 화면 · 씬 이동 · 볼륨 반영 · EventSystem).
// 어느 씬을 먼저 열어도 있게 씬마다 놓아 두고, 처음 생긴 하나만 남긴다. 그래서 다른 기능은 Inspector 대신 Instance 로 찾는다.
// 다른 기능이 Awake · OnEnable 에서 찾을 수 있도록 먼저 실행한다.
// 자세한 흐름 : Docs/Technical/UI/CommonUI.md
[DefaultExecutionOrder(-100)]
public class CommonUI : MonoBehaviour
{
    [SerializeField] private SettingsView _settings;
    [SerializeField] private SceneLoader _sceneLoader;

    public static CommonUI Instance { get; private set; }

    public SettingsView Settings => _settings;
    public SceneLoader SceneLoader => _sceneLoader;

    private void Awake() => Init();
    private void OnDestroy()
    {
        if(Instance == this) Instance = null;
    }

    // Destroy 는 프레임 끝에 지우므로, 그 전에 꺼 두어 자식 화면이 동작하지 않게 한다.
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

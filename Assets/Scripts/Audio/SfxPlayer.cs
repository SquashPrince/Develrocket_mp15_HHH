using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 효과음 재생. 공용 UI 에 하나만 두고, 전투 순간 알림 · 재생 요청 · 버튼 클릭음을 같은 AudioSource 에서 2D 로 겹쳐 낸다.
// 버튼마다 컴포넌트를 붙이지 않도록 씬이 열릴 때 그 씬의 모든 버튼에 클릭음을 연결한다. 실행 중에 새로 만드는 버튼은 연결되지 않는다.
[RequireComponent(typeof(AudioSource))]
public class SfxPlayer : MonoBehaviour
{
    [SerializeField] private SfxSettings _settings;

    private AudioSource _source;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void OnEnable() => BindEvent();
    private void OnDisable() => UnbindEvent();

    public void Play(SoundGroup sound)
    {
        if(!sound.HasClip) return;

        _source.PlayOneShot(sound.PickClip(), sound.Volume);
    }

    private void OnMoment(BattleFeedback.Moment moment, Vector3 direction)
    {
        if(!_settings.TryGetSound(moment, out SfxSettings.MomentSound sound)) return;

        // 기본 소리와 추가 겹을 동시에 낸다. 비어 있는 겹은 Play 가 건너뛴다.
        Play(sound.Sound);

        if(sound.ExtraLayers == null) return;

        foreach(SoundGroup layer in sound.ExtraLayers)
        {
            Play(layer);
        }
    }

    private void PlayButtonClick() => Play(_settings.ButtonClick);

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            ConnectButtons(root);
        }
    }

    // root 아래의 모든 버튼에 클릭음을 연결한다. 같은 버튼에 두 번 연결되지 않게 먼저 뺀다.
    private void ConnectButtons(GameObject root)
    {
        foreach(Button button in root.GetComponentsInChildren<Button>(true))
        {
            button.onClick.RemoveListener(PlayButtonClick);
            button.onClick.AddListener(PlayButtonClick);
        }
    }

    private void CacheComponent()
    {
        _source = GetComponent<AudioSource>();
    }

    // 공용 UI 는 씬 전환에도 남으므로, 공용 UI 자신의 버튼은 씬 알림이 아닌 시작할 때 연결한다.
    private void Init()
    {
        ConnectButtons(transform.root.gameObject);
    }

    private void BindEvent()
    {
        BattleFeedback.Occurred += OnMoment;
        SfxRequest.Requested += Play;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void UnbindEvent()
    {
        BattleFeedback.Occurred -= OnMoment;
        SfxRequest.Requested -= Play;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}

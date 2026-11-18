using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 효과음 재생. 씬이 바뀌어도 유지되는 공용 UI 에 하나만 둔다.
// 전투 순간 : 전투 연출 알림을 받아 효과음 설정에서 그 순간의 소리를 골라 재생한다.
// 재생 요청 : 액션의 휘두름 · 적중 소리처럼 소리를 직접 들고 있는 쪽의 요청을 받아 재생한다.
// 버튼 클릭 : 씬이 열릴 때마다 그 씬의 모든 버튼(꺼진 메뉴 안의 버튼 포함)에 클릭음을 연결한다. 공용 UI 자신의 버튼은 시작할 때 한 번 연결한다.
//   버튼마다 컴포넌트를 붙이지 않아도 되고, 버튼의 실제 클릭 판정을 그대로 쓴다. 실행 중에 새로 만드는 버튼은 연결되지 않는다.
// 모두 위치와 상관없는 2D 소리로, 같은 AudioSource 에서 겹쳐 재생한다. AudioSource 의 출력은 Mixer 의 SFX 그룹으로 둔다.
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
        if(_settings.TryGetSound(moment, out SoundGroup sound)) Play(sound);
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

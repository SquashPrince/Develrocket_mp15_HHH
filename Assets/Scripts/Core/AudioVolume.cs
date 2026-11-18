using UnityEngine;
using UnityEngine.Audio;

// 저장된 전체(Master) · 배경음 · 효과음 볼륨을 AudioMixer 에 반영한다. 설정이 바뀌면 바로 다시 반영한다.
// 볼륨(0 ~ 1)을 Mixer 가 쓰는 데시벨로 바꿔 넣는다. 사람이 듣는 크기는 로그에 가까워, 슬라이더를 고르게 움직이면 소리도 고르게 줄게 하기 위해서다.
// 0 이면 들리지 않는 최저값으로 둔다.
// Mixer 의 노출 변수는 Awake 에서 바꾸면 적용되지 않아 Start 에서 처음 반영한다.
// 볼륨을 쓰는 씬(타이틀 · 게임)마다 하나씩 둔다.
public class AudioVolume : MonoBehaviour
{
    private const float MIN_DECIBEL = -80f;
    // 이 값보다 작은 볼륨은 0 으로 본다. 로그에 0 을 넣을 수 없기 때문이다.
    private const float MIN_VOLUME = 0.0001f;

    [SerializeField] private AudioMixer _mixer;
    [Tooltip("Master 그룹 볼륨의 노출 변수 이름. Mixer 에 등록한 이름과 같아야 한다")]
    [SerializeField] private string _masterParameter = "MasterVolume";
    [Tooltip("배경음 그룹 볼륨의 노출 변수 이름. Mixer 에 등록한 이름과 같아야 한다")]
    [SerializeField] private string _bgmParameter = "BgmVolume";
    [Tooltip("효과음 그룹 볼륨의 노출 변수 이름. Mixer 에 등록한 이름과 같아야 한다")]
    [SerializeField] private string _sfxParameter = "SfxVolume";

    private void Start() => Apply();
    private void OnEnable() => BindSettingsEvent();
    private void OnDisable() => UnbindSettingsEvent();

    private void Apply()
    {
        _mixer.SetFloat(_masterParameter, ToDecibel(SettingsStore.Current.MasterVolume));
        _mixer.SetFloat(_bgmParameter, ToDecibel(SettingsStore.Current.BgmVolume));
        _mixer.SetFloat(_sfxParameter, ToDecibel(SettingsStore.Current.SfxVolume));
    }

    private static float ToDecibel(float volume) => volume < MIN_VOLUME ? MIN_DECIBEL : Mathf.Log10(volume) * 20f;

    private void BindSettingsEvent()
    {
        SettingsStore.Changed += Apply;
    }

    private void UnbindSettingsEvent()
    {
        SettingsStore.Changed -= Apply;
    }
}

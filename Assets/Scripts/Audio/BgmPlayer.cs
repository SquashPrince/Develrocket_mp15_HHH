using UnityEngine;

// 배경음 재생. 씬이 바뀌어도 유지되는 공용 UI 에 하나만 둔다. 씬이 바뀌어도 살아 있어야 곡을 끊지 않고 이어서 넘길 수 있기 때문이다.
// 곡 변경 요청을 받아 그 종류의 곡으로 바꾼다. 곡을 비워 둔 종류는 소리를 끈다(결과 화면 등).
// 크로스페이드 : AudioSource 두 개를 번갈아 쓴다. 새 곡은 0 에서 올리고 이전 곡은 지금 크기에서 내리며, 다 내리면 멈춘다.
//   바뀌는 도중 또 바뀌면 지금 올라가던 곡을 이전 곡으로 보고 그 크기부터 내린다.
// 이미 같은 곡이 나오고 있으면 처음부터 다시 틀지 않는다. 재시작 · 재도전으로 씬을 다시 불러와도 같은 곡이 끊기지 않게 하기 위해서다.
// 실제 시간으로 넘긴다. 히트스톱 · 일시정지와 상관없이 진행되고, 일시정지 중에도 배경음은 그대로 나온다.
// 두 AudioSource 는 반복 재생(Loop) · 2D 로 두고, 출력은 Mixer 의 BGM 그룹으로 둔다.
public class BgmPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource _sourceA;
    [SerializeField] private AudioSource _sourceB;

    [Header("곡")]
    [SerializeField] private AudioClip _titleClip;
    [SerializeField] private AudioClip _waveClip;
    [SerializeField] private AudioClip _bossClip;
    [Tooltip("비워 두면 결과 화면에서 배경음을 끈다")]
    [SerializeField] private AudioClip _resultClip;

    [Header("재생")]
    [Tooltip("곡의 기본 볼륨(0 ~ 1). 배경음 · 전체 볼륨 설정과 곱해진다")]
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;
    [Tooltip("곡이 바뀔 때 겹쳐 넘기는 시간(초, 실제 시간)")]
    [SerializeField, Min(0f)] private float _fadeTime = 1f;

    // 지금 올라가는(나오는) 곡의 AudioSource 와 내려가는 이전 곡의 AudioSource
    private AudioSource _current;
    private AudioSource _previous;
    private AudioClip _currentClip;
    // 이전 곡이 내려가기 시작한 크기
    private float _previousStartVolume;
    private float _fadeTimer;
    private bool _isFading;

    private void Awake() => Init();
    private void OnEnable() => BindRequestEvent();
    private void OnDisable() => UnbindRequestEvent();
    private void Update() => UpdateFade();

    private void OnRequested(BgmTrack track)
    {
        AudioClip clip = GetClip(track);

        if(clip == _currentClip) return;

        _previous = _current;
        _previousStartVolume = _previous.volume;
        _current = _current == _sourceA ? _sourceB : _sourceA;
        _currentClip = clip;
        _current.Stop();
        _current.volume = 0f;
        _current.clip = clip;

        if(clip != null) _current.Play();

        _fadeTimer = 0f;
        _isFading = true;
    }

    private void UpdateFade()
    {
        if(!_isFading) return;

        _fadeTimer += Time.unscaledDeltaTime;

        float rate = _fadeTime > 0f ? Mathf.Clamp01(_fadeTimer / _fadeTime) : 1f;

        _current.volume = _volume * rate;
        _previous.volume = _previousStartVolume * (1f - rate);

        if(rate < 1f) return;

        _previous.Stop();
        _isFading = false;
    }

    private AudioClip GetClip(BgmTrack track)
    {
        switch(track)
        {
            case BgmTrack.Title: return _titleClip;
            case BgmTrack.Wave: return _waveClip;
            case BgmTrack.Boss: return _bossClip;
            default: return _resultClip;
        }
    }

    private void Init()
    {
        _current = _sourceA;
        _previous = _sourceB;
    }

    private void BindRequestEvent()
    {
        BgmRequest.Requested += OnRequested;
    }

    private void UnbindRequestEvent()
    {
        BgmRequest.Requested -= OnRequested;
    }
}

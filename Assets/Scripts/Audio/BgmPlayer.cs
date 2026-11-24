using UnityEngine;

// 배경음 재생. 씬이 바뀌어도 곡을 끊지 않게 공용 UI 에 하나만 두고, AudioSource 두 개로 크로스페이드한다.
// 재시작으로 씬을 다시 불러와도 끊기지 않게, 이미 같은 곡이 나오고 있으면 다시 틀지 않는다.
// 콤보 선택 중에는 선택 화면 · 효과음이 앞으로 나오게 배경음을 낮추고 먹먹하게 만든다. 실제 시간으로 바뀐다.
// 자세한 흐름 : Docs/Technical/Audio/Sound.md
public class BgmPlayer : MonoBehaviour
{
    // 먹먹하게 만들지 않을 때의 필터 기준 주파수(Hz). 사람이 듣는 범위의 끝이라 필터가 없는 것과 같다.
    private const float OPEN_CUTOFF = 22000f;

    [SerializeField] private AudioSource _sourceA;
    [SerializeField] private AudioSource _sourceB;

    [Header("곡")]
    [SerializeField] private AudioClip _titleClip;
    [SerializeField] private AudioClip _waveClip;
    [SerializeField] private AudioClip _bossClip;
    [Tooltip("비워 두면 결과 화면에서 배경음을 끈다")]
    [SerializeField] private AudioClip _resultClip;
    [Tooltip("로딩 화면 곡. 비워 두면 로딩 중에는 소리를 끈다")]
    [SerializeField] private AudioClip _loadingClip;

    [Header("재생")]
    [Tooltip("곡의 기본 볼륨(0 ~ 1). 배경음 · 전체 볼륨 설정과 곱해진다")]
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;
    [Tooltip("곡이 바뀔 때 겹쳐 넘기는 시간(초, 실제 시간)")]
    [SerializeField, Min(0f)] private float _fadeTime = 1f;

    [Header("콤보 선택 중")]
    [Tooltip("콤보 선택 중 배경음 볼륨 배율. 1 이면 그대로")]
    [SerializeField, Range(0f, 1f)] private float _comboSelectVolumeRate = 0.4f;
    [Tooltip("콤보 선택 중 먹먹함. 이 주파수(Hz)보다 높은 소리를 깎는다. 낮을수록 더 먹먹하다. Low Pass Filter 가 있을 때만 쓴다")]
    [SerializeField, Range(100f, 22000f)] private float _comboSelectCutoff = 800f;
    [Tooltip("콤보 선택 효과가 켜지고 꺼지는 데 걸리는 시간(초, 실제 시간)")]
    [SerializeField, Min(0f)] private float _comboSelectBlendTime = 0.3f;

    // 지금 올라가는(나오는) 곡의 AudioSource 와 내려가는 이전 곡의 AudioSource
    private AudioSource _current;
    private AudioSource _previous;
    private AudioClip _currentClip;
    // [선택] 두 AudioSource 오브젝트에 붙은 Low Pass Filter. 없으면 먹먹하게 만들지 않는다.
    private AudioLowPassFilter _filterA;
    private AudioLowPassFilter _filterB;
    // 크로스페이드 진행 : 지금 곡의 크기 비율(0 ~ 1)과, 이전 곡이 내려가기 시작한 크기 비율
    private float _currentRate = 1f;
    private float _previousStartRate;
    private float _fadeTimer;
    // 콤보 선택 효과 비율(0 = 평소, 1 = 콤보 선택 중)과 목표
    private float _comboSelectRate;
    private float _comboSelectTarget;
    private bool _isFading;

    private void Awake()
    {
        CacheComponent();
        Init();
    }
    private void OnEnable() => BindEvent();
    private void OnDisable() => UnbindEvent();
    private void Update()
    {
        UpdateFade();
        UpdateComboSelectBlend();
        ApplyVolume();
    }

    private void OnRequested(BgmTrack track)
    {
        AudioClip clip = GetClip(track);

        if(clip == _currentClip) return;

        _previous = _current;
        _previousStartRate = _currentRate;
        _current = _current == _sourceA ? _sourceB : _sourceA;
        _currentClip = clip;
        _current.Stop();
        _current.clip = clip;

        if(clip != null) _current.Play();

        _currentRate = 0f;
        _fadeTimer = 0f;
        _isFading = true;
    }

    private void OnMoment(BattleFeedback.Moment moment, Vector3 direction)
    {
        if(moment == BattleFeedback.Moment.ComboSelectOpened) _comboSelectTarget = 1f;
        else if(moment == BattleFeedback.Moment.ComboSelectClosed) _comboSelectTarget = 0f;
    }

    private void UpdateFade()
    {
        if(!_isFading) return;

        _fadeTimer += Time.unscaledDeltaTime;
        _currentRate = _fadeTime > 0f ? Mathf.Clamp01(_fadeTimer / _fadeTime) : 1f;

        if(_currentRate < 1f) return;

        _previous.Stop();
        _isFading = false;
    }

    private void UpdateComboSelectBlend()
    {
        float step = _comboSelectBlendTime > 0f ? Time.unscaledDeltaTime / _comboSelectBlendTime : 1f;

        _comboSelectRate = Mathf.MoveTowards(_comboSelectRate, _comboSelectTarget, step);
    }

    // 크로스페이드 비율과 콤보 선택 효과를 함께 반영한다.
    private void ApplyVolume()
    {
        float comboVolume = Mathf.Lerp(1f, _comboSelectVolumeRate, _comboSelectRate);
        float previousRate = _isFading ? _previousStartRate * (1f - _currentRate) : 0f;

        _current.volume = _volume * _currentRate * comboVolume;
        _previous.volume = _volume * previousRate * comboVolume;

        float cutoff = Mathf.Lerp(OPEN_CUTOFF, _comboSelectCutoff, _comboSelectRate);

        if(_filterA != null) _filterA.cutoffFrequency = cutoff;
        if(_filterB != null && _filterB != _filterA) _filterB.cutoffFrequency = cutoff;
    }

    private AudioClip GetClip(BgmTrack track)
    {
        switch(track)
        {
            case BgmTrack.Title: return _titleClip;
            case BgmTrack.Wave: return _waveClip;
            case BgmTrack.Boss: return _bossClip;
            case BgmTrack.Result: return _resultClip;
            case BgmTrack.Loading: return _loadingClip;
            default: return null;
        }
    }

    // 두 AudioSource 가 같은 오브젝트에 있으면 필터도 하나라 같은 것을 가리킨다.
    private void CacheComponent()
    {
        _filterA = _sourceA.GetComponent<AudioLowPassFilter>();
        _filterB = _sourceB.GetComponent<AudioLowPassFilter>();
    }

    private void Init()
    {
        _current = _sourceA;
        _previous = _sourceB;
    }

    private void BindEvent()
    {
        BgmRequest.Requested += OnRequested;
        BattleFeedback.Occurred += OnMoment;
    }

    private void UnbindEvent()
    {
        BgmRequest.Requested -= OnRequested;
        BattleFeedback.Occurred -= OnMoment;
    }
}

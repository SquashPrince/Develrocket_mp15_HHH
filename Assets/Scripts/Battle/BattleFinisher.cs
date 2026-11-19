using System;
using UnityEngine;

// 마지막 일격 연출. 웨이브의 마지막 적을 처치하면 게임 전체를 잠깐 느리게, 보스전의 보스를 처치하면 게임 전체를 잠깐 멈춘다.
// 언제 할지는 게임 흐름이 정해 부르고, 이 기능은 시간 배율과 그 길이만 맡는다. 카메라 줌은 순간 알림을 받은 카메라 연출 기능이 낸다.
// 보스 정지가 끝나면 알려, 게임 흐름이 그때 클리어 결과를 띄우게 한다.
// 길이는 실제 시간으로 잰다. 시간 배율을 바꾼 채로 게임 시간으로 재면 느린 만큼 길어지고, 멈추면 끝나지 않기 때문이다.
// 연출 중 다른 기능(콤보 선택 등)이 시간 배율을 바꿨다면 그 값을 따르고 되돌리지 않는다. (히트스톱과 같은 규칙)
public class BattleFinisher : MonoBehaviour
{
    [Header("웨이브 마지막 적")]
    [Tooltip("느려질 때의 게임 시간 배율")]
    [SerializeField, Range(0.01f, 1f)] private float _waveSlowScale = 0.3f;
    [Tooltip("느려지는 시간(실제 시간, 초)")]
    [SerializeField, Min(0f)] private float _waveSlowDuration = 1f;

    [Header("보스")]
    [Tooltip("멈추는 시간(실제 시간, 초). 끝나면 클리어 결과가 뜬다")]
    [SerializeField, Min(0f)] private float _bossStopDuration = 1f;

    // 연출 전의 시간 배율과 연출이 건 배율. 끝날 때 배율이 연출이 건 값 그대로일 때만 되돌린다.
    private float _previousTimeScale = 1f;
    private float _appliedTimeScale = 1f;
    private float _timer;
    private bool _isBossFinish;

    public bool IsPlaying => _timer > 0f;

    // 보스 정지가 끝나면 알린다.
    public event Action BossFinishEnded;

    private void Update() => UpdateTimer();

    private void OnDisable()
    {
        if(IsPlaying) RestoreTimeScale();

        _timer = 0f;
    }

    public void PlayWaveFinish()
    {
        BattleFeedback.Raise(BattleFeedback.Moment.WaveFinish);
        Begin(_waveSlowScale, _waveSlowDuration, false);
    }

    public void PlayBossFinish()
    {
        BattleFeedback.Raise(BattleFeedback.Moment.BossFinish);
        Begin(0f, _bossStopDuration, true);
    }

    // 진행 중인 연출이 있으면 그 배율을 먼저 되돌린 뒤 새로 시작한다.
    // 길이가 0 이면 시간 배율은 바꾸지 않고, 보스 정지면 바로 끝을 알린다.
    private void Begin(float timeScale, float duration, bool isBossFinish)
    {
        if(IsPlaying) RestoreTimeScale();

        _isBossFinish = isBossFinish;

        if(duration <= 0f)
        {
            _timer = 0f;
            Finish();
            return;
        }

        _previousTimeScale = Time.timeScale;
        _appliedTimeScale = timeScale;
        _timer = duration;
        Time.timeScale = timeScale;
    }

    private void UpdateTimer()
    {
        if(!IsPlaying) return;

        _timer -= Time.unscaledDeltaTime;

        if(_timer > 0f) return;

        _timer = 0f;
        RestoreTimeScale();
        Finish();
    }

    private void Finish()
    {
        if(_isBossFinish) BossFinishEnded?.Invoke();
    }

    // 연출 중 다른 기능이 시간 배율을 바꿨으면 그쪽 값을 그대로 둔다.
    private void RestoreTimeScale()
    {
        if(Time.timeScale == _appliedTimeScale) Time.timeScale = _previousTimeScale;
    }
}

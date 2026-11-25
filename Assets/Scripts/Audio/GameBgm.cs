using UnityEngine;

// 게임 씬의 배경음. 게임 흐름의 알림에 맞춰 웨이브 · 보스 · 결과 곡을 요청한다.
// 알림은 켜질 때 구독하고, 첫 곡은 화면이 다 보인 뒤 그때의 단계로 정한다.
public class GameBgm : MonoBehaviour
{
    [SerializeField] private GameFlow _gameFlow;

    private void Start() => Init();
    private void OnEnable() => BindGameFlowEvent();
    private void OnDisable() => UnbindGameFlowEvent();

    private void OnBossPhaseStarted() => BgmRequest.Play(BgmTrack.Boss);
    private void OnEnded() => BgmRequest.Play(BgmTrack.Result);
    private void OnBossPhaseEnded(GameFlow.BossOutcome outcome) => OnEnded();

    // 첫 곡은 로딩이 끝나 게임 화면이 다 보인 뒤에 정한다. 그 사이 보스전이 시작됐어도 그때 단계로 고른다.
    private void Init()
    {
        CommonUI.Instance.SceneLoader.WhenShown(PlayStageTrack);
    }

    private void PlayStageTrack()
    {
        BgmRequest.Play(_gameFlow.IsBossPhase ? BgmTrack.Boss : BgmTrack.Wave);
    }

    private void BindGameFlowEvent()
    {
        _gameFlow.BossPhaseStarted += OnBossPhaseStarted;
        _gameFlow.WaveWiped += OnEnded;
        _gameFlow.BossPhaseEnded += OnBossPhaseEnded;
    }

    private void UnbindGameFlowEvent()
    {
        _gameFlow.BossPhaseStarted -= OnBossPhaseStarted;
        _gameFlow.WaveWiped -= OnEnded;
        _gameFlow.BossPhaseEnded -= OnBossPhaseEnded;
    }
}

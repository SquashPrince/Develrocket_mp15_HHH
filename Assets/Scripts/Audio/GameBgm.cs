using UnityEngine;

// 게임 씬의 배경음. 게임 흐름의 알림에 맞춰 곡 변경을 요청한다.
// 시작 : 웨이브 곡. 로딩 화면이 닫히고 게임 화면이 다 보인 뒤에 튼다. 보스전 재도전으로 다시 불러온 씬은 시작하자마자 보스전이라 보스 곡이다.
// 보스전 시작 : 보스 곡. 웨이브 전멸 · 보스전 종료(결과 화면) : 결과 곡.
// 게임 흐름은 시작하면서 보스전 알림을 보낼 수 있으므로, 알림 구독은 켜질 때 하고 시작할 때는 지금 단계로 곡을 정한다.
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

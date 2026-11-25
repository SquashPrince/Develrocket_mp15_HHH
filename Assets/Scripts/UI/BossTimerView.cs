using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보스전 남은 제한 시간을 시:분:초 글자와 막대로 보여준다. 초는 올려서, 0 이 되는 순간 00:00:00 이 된다.
// 이 오브젝트를 끄면 보스전 시작 알림을 받지 못하므로 표시 묶음은 자식으로 둔다.
public class BossTimerView : MonoBehaviour
{
    private const int SECONDS_PER_MINUTE = 60;
    private const int SECONDS_PER_HOUR = 3600;

    [SerializeField] private GameFlow _gameFlow;
    [Tooltip("보스전 전에는 숨길 표시 묶음. 이 오브젝트의 자식을 연결한다")]
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private Slider _timeSlider;

    // 보이는 초가 바뀔 때만 글자를 다시 써 문자열을 매 프레임 만들지 않는다.
    private int _shownSeconds = -1;

    private void Start() => Init();
    private void OnEnable() => BindGameFlowEvent();
    private void OnDisable() => UnbindGameFlowEvent();
    private void Update() => Refresh();

    private void OnBossPhaseStarted() => _root.SetActive(true);

    private void Refresh()
    {
        if(!_gameFlow.IsBossPhase) return;

        _timeSlider.value = _gameFlow.TimeLeft / _gameFlow.TimeLimit;

        int seconds = Mathf.CeilToInt(_gameFlow.TimeLeft);

        if(seconds == _shownSeconds) return;

        _shownSeconds = seconds;
        _timeText.text = $"{seconds / SECONDS_PER_HOUR:00}:{seconds % SECONDS_PER_HOUR / SECONDS_PER_MINUTE:00}:{seconds % SECONDS_PER_MINUTE:00}";
    }

    private void Init()
    {
        _root.SetActive(false);
    }

    private void BindGameFlowEvent()
    {
        _gameFlow.BossPhaseStarted += OnBossPhaseStarted;
    }

    private void UnbindGameFlowEvent()
    {
        _gameFlow.BossPhaseStarted -= OnBossPhaseStarted;
    }
}

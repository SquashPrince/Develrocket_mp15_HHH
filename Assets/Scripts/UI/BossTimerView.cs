using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보스전 제한 시간 표시. 보스전(제한 시간을 쓰는 웨이브)이 시작되면 켜지고, 남은 시간을 00:02:54(시:분:초) 글자와 줄어드는 막대(Slider 0 ~ 1)로 보여준다.
// 남은 시간은 매 프레임 줄어드므로 매 프레임 읽는다. 글자는 보이는 초가 바뀔 때만 다시 써 문자열을 매 프레임 만들지 않는다.
// 초는 올림으로 보여준다. 시작할 때 00:03:00 으로 보이고, 남은 시간이 0 이 되는 순간 00:00:00 이 된다.
// 표시 묶음은 이 컴포넌트가 붙은 오브젝트의 자식으로 둔다. 이 오브젝트를 끄면 보스전 시작 알림을 받지 못한다.
// 위치 · 꾸미기는 UI 작업 때 정한다.
public class BossTimerView : MonoBehaviour
{
    private const int SECONDS_PER_MINUTE = 60;
    private const int SECONDS_PER_HOUR = 3600;

    [SerializeField] private GameFlow _gameFlow;
    [Tooltip("보스전 전에는 숨길 표시 묶음. 이 오브젝트의 자식을 연결한다")]
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private Slider _timeSlider;

    // 지금 글자로 보여주는 초. 처음에는 어떤 값과도 다르게 둔다.
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

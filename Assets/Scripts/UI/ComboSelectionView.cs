using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 콤보 스킬 캐릭터 선택 화면. 선택 중에만 이전 · 다음 캐릭터 초상화와 남은 시간 · 캔슬 안내를 보여준다.
// 선택 상태는 콤보 스킬 선택 기능이 들고 있고, 이 기능은 선택 중에만 매 프레임 읽어 그린다. 남은 시간은 선택 중 매 프레임 바뀌기 때문이다.
// 게임 시간이 느려져도 남은 시간은 실제 시간으로 줄어드는 값을 그대로 보여준다.
// 이 컴포넌트는 항상 켜져 있는 오브젝트에 붙이고, 켜고 끌 화면은 따로 연결한다.
public class ComboSelectionView : MonoBehaviour
{
    [SerializeField] private ComboSkillSelector _selector;
    [Tooltip("선택 중에만 켜는 화면 묶음")]
    [SerializeField] private GameObject _root;
    [Tooltip("이전 캐릭터 초상화 (이전 키 : C · 좌클릭 · Q)")]
    [SerializeField] private Image _previousPortrait;
    [Tooltip("다음 캐릭터 초상화 (다음 키 : Space · 우클릭 · E)")]
    [SerializeField] private Image _nextPortrait;
    [Tooltip("[선택] 남은 시간 숫자")]
    [SerializeField] private TMP_Text _timeText;
    [Tooltip("[선택] 남은 시간 막대 (Slider 0 ~ 1)")]
    [SerializeField] private Slider _timeSlider;

    private void Update() => Refresh();

    private void Refresh()
    {
        bool isSelecting = _selector.IsSelecting;

        if(_root.activeSelf != isSelecting) _root.SetActive(isSelecting);

        if(!isSelecting) return;

        _previousPortrait.sprite = _selector.PreviousCandidate.Character.Portrait;
        _nextPortrait.sprite = _selector.NextCandidate.Character.Portrait;

        if(_timeText != null) _timeText.text = Mathf.Max(0f, _selector.RemainingTime).ToString("0.0");
        if(_timeSlider != null) _timeSlider.value = _selector.RemainingRatio;
    }
}

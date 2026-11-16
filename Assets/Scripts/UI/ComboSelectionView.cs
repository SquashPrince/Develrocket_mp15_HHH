using TMPro;
using UnityEngine;

// [임시 표시] 콤보 스킬 캐릭터 선택 화면. 선택 중에만 이전 · 다음 캐릭터 이름과 남은 시간 · 캔슬 키를 보여준다.
// 기본 글꼴에 한글이 없어 임시 안내는 영어로 쓴다.
// 선택 상태는 콤보 스킬 선택 기능이 들고 있고, 이 기능은 매 프레임 읽어 화면에 그리기만 한다.
// 게임 시간이 느려져도 남은 시간은 실제 시간으로 줄어드는 값을 그대로 보여준다. 꾸미는 것은 최소 UI 작업에서 한다.
// 이 컴포넌트는 항상 켜져 있는 오브젝트에 붙이고, 켜고 끌 화면은 따로 연결한다.
public class ComboSelectionView : MonoBehaviour
{
    [SerializeField] private ComboSkillSelector _selector;
    [Tooltip("선택 중에만 켜는 화면 묶음")]
    [SerializeField] private GameObject _root;
    [Tooltip("이전 캐릭터 이름 (이전 키 : C · 좌클릭 · Q)")]
    [SerializeField] private TMP_Text _previousText;
    [Tooltip("다음 캐릭터 이름 (다음 키 : Space · 우클릭 · E)")]
    [SerializeField] private TMP_Text _nextText;
    [Tooltip("남은 시간과 캔슬 키 안내")]
    [SerializeField] private TMP_Text _timeText;

    private void Update() => Refresh();

    private void Refresh()
    {
        bool isSelecting = _selector.IsSelecting;

        if(_root.activeSelf != isSelecting) _root.SetActive(isSelecting);

        if(!isSelecting) return;

        _previousText.text = $"< {_selector.PreviousCandidate.Character.name}";
        _nextText.text = $"{_selector.NextCandidate.Character.name} >";
        _timeText.text = $"{Mathf.Max(0f, _selector.RemainingTime):0.0}\nF : Cancel";
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 콤보 스킬 캐릭터 선택 화면. 선택 중에만 이전 · 다음 캐릭터 초상화와 남은 시간 · 캔슬 안내를 보여준다.
// 선택 상태는 콤보 스킬 선택 기능이 들고 있고, 이 기능은 선택 중에만 매 프레임 읽어 그린다. 남은 시간은 선택 중 매 프레임 바뀌기 때문이다.
// 게임 시간이 느려져도 남은 시간은 실제 시간으로 줄어드는 값을 그대로 보여준다.
// 이 컴포넌트는 항상 켜져 있는 오브젝트에 붙이고, 켜고 끌 화면은 따로 연결한다.
// 선택이 시작되면 전투 HUD 를 숨기고, 연계 대상을 고르거나 캔슬해 선택이 끝나면 다시 보여준다.
//   파티 · 버튼 HUD 는 CanvasGroup 투명도로 숨긴다. 오브젝트를 끄면 HUD 기능의 알림 구독이 풀려, 선택이 끝나는 순간의 교체 등을 놓칠 수 있기 때문이다.
//   적 바는 실행 중에 생겨 연결할 수 없으므로 공용 알림(BattleHudVisibility)으로 스스로 숨게 한다.
public class ComboSelectionView : MonoBehaviour
{
    [SerializeField] private ComboSkillSelector _selector;
    [Tooltip("선택 중에만 켜는 화면 묶음")]
    [SerializeField] private GameObject _root;
    [Tooltip("선택 중 숨길 화면 HUD (파티 HUD · 버튼 HUD 의 CanvasGroup)")]
    [SerializeField] private CanvasGroup[] _hiddenWhileSelecting;
    [Tooltip("이전 캐릭터 초상화 (이전 키 : C · 좌클릭 · Q)")]
    [SerializeField] private Image _previousPortrait;
    [Tooltip("다음 캐릭터 초상화 (다음 키 : Space · 우클릭 · E)")]
    [SerializeField] private Image _nextPortrait;
    [Tooltip("[선택] 이전 캐릭터 상징색을 곱할 그래픽 (초상화 테두리 · 게이지 채움 등)")]
    [SerializeField] private Graphic[] _previousThemeGraphics;
    [Tooltip("[선택] 다음 캐릭터 상징색을 곱할 그래픽 (초상화 테두리 · 게이지 채움 등)")]
    [SerializeField] private Graphic[] _nextThemeGraphics;
    [Tooltip("[선택] 남은 시간 숫자")]
    [SerializeField] private TMP_Text _timeText;
    [Tooltip("[선택] 남은 시간 막대 (Slider 0 ~ 1)")]
    [SerializeField] private Slider _timeSlider;

    private void Update() => Refresh();

    private void Refresh()
    {
        bool isSelecting = _selector.IsSelecting;

        if(_root.activeSelf != isSelecting) SetSelectingView(isSelecting);

        if(!isSelecting) return;

        CharacterData previous = _selector.PreviousCandidate.Character;
        CharacterData next = _selector.NextCandidate.Character;

        // 같은 값을 다시 넣으면 UI 는 다시 그리지 않으므로 매 프레임 넣어도 비용이 없다.
        _previousPortrait.sprite = previous.Portrait;
        _nextPortrait.sprite = next.Portrait;

        SetThemeColor(_previousThemeGraphics, previous.ThemeColor);
        SetThemeColor(_nextThemeGraphics, next.ThemeColor);

        if(_timeText != null) _timeText.text = FormatTime(Mathf.Max(0f, _selector.RemainingTime));
        if(_timeSlider != null) _timeSlider.value = _selector.RemainingRatio;
    }

    // 선택 화면을 켜고 끄면서, 선택 중에는 전투 HUD 를 숨기고 끝나면 다시 보여준다.
    private void SetSelectingView(bool isSelecting)
    {
        _root.SetActive(isSelecting);

        foreach(CanvasGroup hud in _hiddenWhileSelecting)
        {
            hud.alpha = isSelecting ? 0f : 1f;
        }

        BattleHudVisibility.SetVisible(!isSelecting);
    }

    private static void SetThemeColor(Graphic[] graphics, Color color)
    {
        foreach(Graphic graphic in graphics)
        {
            graphic.color = color;
        }
    }

    // 남은 시간을 "초 : 1/100 초 : 그 아래 두 자리" 로 보여준다. (예 : 2.5238 초 → 02:52:38)
    // 소수점 아래는 버린다. 반올림하면 남은 시간보다 많게 보일 수 있기 때문이다.
    private static string FormatTime(float time)
    {
        int seconds = (int)time;
        int centiseconds = (int)(time * 100f) % 100;
        int belowCentiseconds = (int)(time * 10000f) % 100;

        return $"{seconds:00}:{centiseconds:00}:{belowCentiseconds:00}";
    }
}

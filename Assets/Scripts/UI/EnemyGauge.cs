using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 적 하나의 체력 · 그로기 막대(Slider 0 ~ 1). 일반 적 머리 위 바와 화면 위쪽 보스 바가 함께 쓴다.
// 적 바는 적(EnemyController)이 등장할 때 그 적이 연결한 HUD 아래에 직접 만들고 대상을 넘겨준다(SetTarget).
//   보스 바는 화면 Canvas, 머리 위 바는 적 몸에 붙은 월드 공간 Canvas 아래에 만들어진다.
//   대상은 넘겨받을 때만 정한다. 만들어지는 순간에는 자식(잔상 막대 등)이 아직 준비되지 않았을 수 있어, 그때 그리면 오류가 나기 때문이다.
// 체력 · 그로기 게이지는 적 기능의 알림이 올 때만 다시 그린다.
// 그로기가 아닐 때는 쌓인 그로기 게이지를 퍼센트 숫자로도 보여주고, 그로기 중에는 숫자를 숨긴다.
// 그로기 중에는 그로기 막대가 남은 그로기 시간을 보여주므로, 그로기 중에만 매 프레임 읽는다.
// 그로기 중 막대 채움 색은 콤보 스킬 연계가 남았는지에 따라 두 색 중 하나로 덮는다. 연계를 쓴 순간을 알리는 알림이 없어, 그로기 중 매 프레임 읽을 때 함께 확인한다.
// 체력 막대 뒤의 잔상 막대는 맞아서 줄어든 체력만 잔상으로 남긴다. 처음 그릴 때는 잔상 없이 바로 맞춘다.
// 대상이 처치되거나, 콤보 스킬 선택으로 전투 HUD 를 숨기는 동안(BattleHudVisibility) 막대 묶음을 숨긴다.
// 초상화 : 초상화 Image 를 연결한 막대(보스 바)만 적 데이터의 초상화를 띄운다. 비워 두면 초상화 없이 동작한다 (일반 적 머리 위 바).
public class EnemyGauge : MonoBehaviour
{
    [Tooltip("처치되거나 콤보 스킬 선택 중일 때 숨길 막대 묶음")]
    [SerializeField] private GameObject _root;
    [Tooltip("[선택] 적 데이터의 초상화를 띄울 Image. 비워 두면 초상화 없이 동작한다")]
    [SerializeField] private Image _portraitImage;
    [SerializeField] private Slider _healthSlider;
    [Tooltip("체력 막대 뒤에 겹친 잔상 막대")]
    [SerializeField] private GaugeTrail _healthTrail;
    [SerializeField] private Slider _groggySlider;
    [Tooltip("[선택] 그로기 게이지 퍼센트 숫자. 그로기가 아닐 때만 보인다")]
    [SerializeField] private TMP_Text _groggyPercentText;
    [Tooltip("그로기 중, 콤보 스킬 연계가 남아 있을 때 그로기 막대 채움 영역에 덮어 띄울 색. 그로기가 끝나면 원래 색으로 돌아간다")]
    [FormerlySerializedAs("_groggyFillColor")]
    [SerializeField] private Color _comboReadyFillColor = new Color(1f, 0.5f, 0f);
    [Tooltip("그로기 중, 콤보 스킬 연계를 모두 썼거나 이어받을 대기 캐릭터가 없을 때 그로기 막대 채움 영역에 덮어 띄울 색")]
    [SerializeField] private Color _comboUsedFillColor = new Color(0.5f, 0.5f, 0.5f);

    // 보여줄 적. 바를 만든 적이 넘겨준다.
    private EnemyState _target;
    // 그로기 막대 채움 영역의 이미지와 원래 색
    private Image _groggyFillImage;
    private Color _normalFillColor;

    // HUD 숨김 알림은 바가 있는 동안 내내 받는다. 막대 묶음이 바 자신이면 숨길 때 이 컴포넌트도 꺼지므로, 켜짐 · 꺼짐에 묶으면 다시 보여 달라는 알림을 받지 못한다.
    private void Awake()
    {
        CacheComponent();
        BindHudVisibilityEvent();
    }

    private void OnDestroy() => UnbindHudVisibilityEvent();
    // 만들어지며 처음 켜질 때는 아직 대상이 없으므로 넘겨받을 때(SetTarget) 구독한다. 다시 켜질 때는 대상이 있어 여기서 구독한다.
    private void OnEnable()
    {
        if(_target != null) BindEnemyEvent();
    }

    // 다른 오브젝트의 Awake 가 모두 끝난 뒤 처음 값을 한 번 더 그린다. OnEnable 이 대상의 처음 값보다 먼저 돌 수 있기 때문이다.
    private void Start() => RefreshAll();
    private void OnDisable() => UnbindEnemyEvent();
    private void Update() => RefreshGroggyTime();

    // 만든 쪽이 보여줄 적을 넘겨준다. 만든 직후 한 번만 부른다.
    public void SetTarget(EnemyState target)
    {
        _target = target;
        BindEnemyEvent();
    }

    private void OnDamaged(bool isImpact) => RefreshHealth(false);

    // isSnap : 잔상 없이 바로 맞출지. 대미지 알림이 아닌 처음 그리기에서 켠다.
    private void RefreshHealth(bool isSnap)
    {
        float ratio = _target.CurrentHealth / _target.MaxHealth;
        _healthSlider.value = ratio;

        if(isSnap) _healthTrail.Snap(ratio);
        else _healthTrail.SetValue(ratio);
    }

    // 그로기가 아닐 때는 쌓인 게이지, 그로기 중에는 남은 시간을 보여준다.
    // 퍼센트는 소수점 아래를 버린다. 반올림하면 가득 차기 전에 100 으로 보일 수 있기 때문이다.
    // 항상 두 자리(00 ~ 99)로 보여준다. 100 이 되면 그로기에 들어가 숫자가 숨겨진다.
    private void RefreshGroggyGauge()
    {
        if(_target.IsGroggy) return;

        float ratio = _target.CurrentGroggy / _target.MaxGroggy;
        _groggySlider.value = ratio;

        if(_groggyPercentText != null) _groggyPercentText.text = Mathf.FloorToInt(ratio * 100f).ToString("00");
    }

    // 그로기 중에만 남은 시간과 남은 연계 여부에 따른 막대 색을 그린다.
    private void RefreshGroggyTime()
    {
        if(!_target.IsGroggy) return;

        _groggySlider.value = _target.GroggyTimeRatio;
        _groggyFillImage.color = _target.HasRemainingComboChain ? _comboReadyFillColor : _comboUsedFillColor;
    }

    // 그로기에 들어가거나 풀릴 때 퍼센트 숫자를 켜고 끄고, 막대를 그 상태에 맞게 다시 그린다.
    private void RefreshGroggyState()
    {
        if(_groggyPercentText != null) _groggyPercentText.gameObject.SetActive(!_target.IsGroggy);
        if(!_target.IsGroggy) _groggyFillImage.color = _normalFillColor;

        RefreshGroggyGauge();
        RefreshGroggyTime();
    }

    private void RefreshPortrait()
    {
        if(_portraitImage != null) _portraitImage.sprite = _target.GetComponent<EnemyController>().Data.Portrait;
    }

    // 처치된 적이거나 전투 HUD 를 숨기는 중(콤보 스킬 선택)이면 막대 묶음을 숨긴다. 대상을 넘겨받기 전에는 그리지 않는다.
    private void RefreshVisibility()
    {
        if(_target != null) _root.SetActive(_target.IsAlive && BattleHudVisibility.IsVisible);
    }

    // 구독할 때 현재 값으로 한 번 그린다.
    private void BindEnemyEvent()
    {
        _target.Damaged += OnDamaged;
        _target.Died += RefreshVisibility;
        _target.GroggyGaugeChanged += RefreshGroggyGauge;
        _target.GroggyEntered += RefreshGroggyState;
        _target.Recovered += RefreshGroggyState;
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshVisibility();
        RefreshPortrait();
        RefreshHealth(true);
        RefreshGroggyState();
    }

    private void UnbindEnemyEvent()
    {
        _target.Damaged -= OnDamaged;
        _target.Died -= RefreshVisibility;
        _target.GroggyGaugeChanged -= RefreshGroggyGauge;
        _target.GroggyEntered -= RefreshGroggyState;
        _target.Recovered -= RefreshGroggyState;
    }

    private void BindHudVisibilityEvent()
    {
        BattleHudVisibility.Changed += RefreshVisibility;
    }

    private void UnbindHudVisibilityEvent()
    {
        BattleHudVisibility.Changed -= RefreshVisibility;
    }

    private void CacheComponent()
    {
        _groggyFillImage = _groggySlider.fillRect.GetComponent<Image>();
        _normalFillColor = _groggyFillImage.color;
    }
}

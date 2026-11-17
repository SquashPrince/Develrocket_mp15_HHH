using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 적 하나의 체력 · 그로기 막대(Slider 0 ~ 1). 일반 적 머리 위 바와 화면 위쪽 보스 바가 함께 쓴다.
// 대상을 비워 두면 부모에서 찾는다. 일반 적 Prefab 의 자식으로 붙이면 따로 연결하지 않아도 된다. 보스 바는 씬의 보스를 연결한다.
// 체력 · 그로기 게이지는 적 기능의 알림이 올 때만 다시 그린다.
// 그로기가 아닐 때는 쌓인 그로기 게이지를 퍼센트 숫자로도 보여주고, 그로기 중에는 숫자를 숨긴다.
// 그로기 중에는 그로기 막대가 남은 그로기 시간을 보여주므로, 그로기 중에만 매 프레임 읽는다.
// 그로기 중 막대 채움 색은 콤보 스킬 연계가 남았는지에 따라 두 색 중 하나로 덮는다. 연계를 쓴 순간을 알리는 알림이 없어, 그로기 중 매 프레임 읽을 때 함께 확인한다.
// 체력 막대 뒤의 잔상 막대는 맞아서 줄어든 체력만 잔상으로 남긴다. 처음 그릴 때는 잔상 없이 바로 맞춘다.
// 대상이 처치되면 막대 묶음을 숨긴다.
public class EnemyGauge : MonoBehaviour
{
    [Tooltip("[선택] 보여줄 적. 비워 두면 부모에서 찾는다 (일반 적 머리 위 바)")]
    [SerializeField] private EnemyState _target;
    [Tooltip("처치되면 숨길 막대 묶음")]
    [SerializeField] private GameObject _root;
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

    // 그로기 막대 채움 영역의 이미지와 원래 색
    private Image _groggyFillImage;
    private Color _normalFillColor;

    private void Awake() => CacheComponent();
    private void OnEnable() => BindEnemyEvent();
    // 다른 오브젝트의 Awake 가 모두 끝난 뒤 처음 값을 한 번 더 그린다. OnEnable 이 대상의 처음 값보다 먼저 돌 수 있기 때문이다.
    private void Start() => RefreshAll();
    private void OnDisable() => UnbindEnemyEvent();
    private void Update() => RefreshGroggyTime();

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

    private void Hide() => _root.SetActive(false);

    // 구독할 때 현재 값으로 한 번 그린다.
    private void BindEnemyEvent()
    {
        _target.Damaged += OnDamaged;
        _target.Died += Hide;
        _target.GroggyGaugeChanged += RefreshGroggyGauge;
        _target.GroggyEntered += RefreshGroggyState;
        _target.Recovered += RefreshGroggyState;
        RefreshAll();
    }

    private void RefreshAll()
    {
        _root.SetActive(_target.IsAlive);
        RefreshHealth(true);
        RefreshGroggyState();
    }

    private void UnbindEnemyEvent()
    {
        _target.Damaged -= OnDamaged;
        _target.Died -= Hide;
        _target.GroggyGaugeChanged -= RefreshGroggyGauge;
        _target.GroggyEntered -= RefreshGroggyState;
        _target.Recovered -= RefreshGroggyState;
    }

    private void CacheComponent()
    {
        if(_target == null) _target = GetComponentInParent<EnemyState>();

        _groggyFillImage = _groggySlider.fillRect.GetComponent<Image>();
        _normalFillColor = _groggyFillImage.color;
    }
}

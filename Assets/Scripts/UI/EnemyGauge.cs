using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 적 하나의 체력 · 그로기 막대. 머리 위 바와 보스 바가 함께 쓰고, 적이 등장할 때 만들어 대상을 넘겨준다.
// 그로기 중에는 남은 그로기 시간과 연계가 남았는지(네온 순환 / 회색)를 보여준다. 연계를 쓴 순간의 알림이 없어 매 프레임 읽는다.
// 깜빡임 · 순환은 콤보 선택으로 게임 시간이 느려져도 같은 속도로 바뀌게 실제 시간으로 잰다.
// 자세한 흐름 : Docs/Technical/UI/BattleHud.md
public class EnemyGauge : MonoBehaviour
{
    [Tooltip("처치되거나 콤보 스킬 선택 중일 때 숨길 막대 묶음")]
    [SerializeField] private GameObject _root;
    [Tooltip("[선택] 적 데이터의 초상화를 띄울 Image. 비워 두면 초상화 없이 동작한다")]
    [SerializeField] private Image _portraitImage;
    [Tooltip("[선택] 적 데이터의 이름을 띄울 글자")]
    [SerializeField] private TMP_Text _nameText;
    [Tooltip("[선택] 현재 / 최대 체력 숫자. 소수점 아래는 올려서 보여준다")]
    [SerializeField] private TMP_Text _healthText;
    [SerializeField] private Slider _healthSlider;
    [Tooltip("체력 막대 뒤에 겹친 잔상 막대")]
    [SerializeField] private GaugeTrail _healthTrail;
    [SerializeField] private Slider _groggySlider;
    [Tooltip("[선택] 그로기 게이지 퍼센트 숫자. 그로기가 아닐 때만 보인다")]
    [SerializeField] private TMP_Text _groggyPercentText;
    [Header("그로기 직전 깜빡임")]
    [Tooltip("그로기 게이지가 이 비율 이상이면 막대 채움을 원래 색 ↔ 흰색으로 깜빡인다 (0.99 = 99%)")]
    [SerializeField, Range(0f, 1f)] private float _nearFullThreshold = 0.99f;
    [Tooltip("깜빡임 속도 (초당 원래 색 → 흰색 → 원래 색 횟수, 실제 시간)")]
    [SerializeField, Min(0.1f)] private float _nearFullBlinkSpeed = 4f;

    [Header("그로기 중 막대 색")]
    [Tooltip("그로기 중, 콤보 스킬 연계가 남아 있을 때 순서대로 바뀔 막대 색. Mode 를 Fixed 로 두고 네온색 사이에 흰색 키를 넣으면 색이 뚝뚝 바뀌며 흰색으로 번쩍인다. 키 사이 폭이 그 색이 보이는 시간이다")]
    [SerializeField] private Gradient _comboReadyColors = CreateNeonFlashGradient();
    [Tooltip("색 순서를 한 바퀴 도는 시간(초, 실제 시간)")]
    [SerializeField, Min(0.05f)] private float _comboReadyCycleTime = 0.6f;
    [Tooltip("그로기 중, 콤보 스킬 연계를 모두 썼거나 이어받을 대기 캐릭터가 없을 때 그로기 막대 채움 영역에 덮어 띄울 색")]
    [SerializeField] private Color _comboUsedFillColor = new Color(0.5f, 0.5f, 0.5f);

    // 바를 만든 적이 넘겨준다.
    private EnemyState _target;
    private Graphic _groggyFillImage;
    private Color _normalFillColor;

    // 막대 묶음이 바 자신이면 숨길 때 이 컴포넌트도 꺼지므로, HUD 숨김 · 다시 살아남 알림은 켜짐 · 꺼짐이 아닌 바가 있는 동안 내내 받는다.
    private void Awake()
    {
        CacheComponent();
        BindHudVisibilityEvent();
    }

    private void OnDestroy()
    {
        UnbindHudVisibilityEvent();
        UnbindReviveEvent();
    }
    // 처음 켜질 때는 대상이 없어 SetTarget 에서 구독하고, 다시 켜질 때는 여기서 구독한다.
    private void OnEnable()
    {
        if(_target != null) BindEnemyEvent();
    }

    // OnEnable 이 대상의 처음 값보다 먼저 돌 수 있어 Start 에서 한 번 더 그린다.
    private void Start() => RefreshAll();
    private void OnDisable() => UnbindEnemyEvent();
    private void Update()
    {
        BlinkNearFull();
        RefreshGroggyTime();
    }

    // 만들어지는 순간에는 자식(잔상 막대 등)이 준비되지 않았을 수 있어, 대상은 넘겨받을 때 정하고 그린다.
    public void SetTarget(EnemyState target)
    {
        _target = target;
        BindEnemyEvent();
        BindReviveEvent();
    }

    private void OnDamaged(bool isImpact) => RefreshHealth(false);

    // 처치되어 숨긴 바를 다시 보이고 처음 값으로 그린다. 막대 묶음이 바 자신이면 다시 켜지며 적 알림도 다시 구독한다.
    private void OnRevived()
    {
        RefreshVisibility();
        RefreshAll();
    }

    // isSnap : 처음 그리기에서 잔상 없이 바로 맞춘다.
    private void RefreshHealth(bool isSnap)
    {
        float ratio = _target.CurrentHealth / _target.MaxHealth;
        _healthSlider.value = ratio;

        // 체력이 조금이라도 남았는데 0 으로 보이지 않게 올린다. SetText 는 맞을 때마다 문자열을 만들지 않는다.
        if(_healthText != null) _healthText.SetText("{0} / {1}", Mathf.CeilToInt(_target.CurrentHealth), Mathf.CeilToInt(_target.MaxHealth));

        if(isSnap) _healthTrail.Snap(ratio);
        else _healthTrail.SetValue(ratio);
    }

    // 퍼센트는 반올림하면 가득 차기 전에 100 으로 보일 수 있어 버린다. 100 이 되면 그로기라 숫자가 숨겨진다.
    private void RefreshGroggyGauge()
    {
        if(_target.IsGroggy) return;

        float ratio = _target.CurrentGroggy / _target.MaxGroggy;
        _groggySlider.value = ratio;

        if(_groggyPercentText != null) _groggyPercentText.text = Mathf.FloorToInt(ratio * 100f).ToString("00");
    }

    private void RefreshGroggyTime()
    {
        if(!_target.IsGroggy) return;

        _groggySlider.value = _target.GroggyTimeRatio;

        Color color = _target.HasRemainingComboChain ? _comboReadyColors.Evaluate(Mathf.Repeat(Time.unscaledTime / _comboReadyCycleTime, 1f)) : _comboUsedFillColor;

        _groggyFillImage.color = color;
    }

    // 곧 그로기에 들어간다는 신호. 같은 색을 다시 넣으면 UI 는 다시 그리지 않으므로 매 프레임 원래 색을 넣어도 비용이 없다.
    private void BlinkNearFull()
    {
        if(_target.IsGroggy) return;

        if(_groggySlider.value < _nearFullThreshold)
        {
            _groggyFillImage.color = _normalFillColor;
            return;
        }

        // PingPong 은 2 마다 0 → 1 → 0 을 한 번 오가므로, 시간에 2 × 속도를 곱해 초당 속도만큼 오가게 한다.
        float rate = Mathf.PingPong(Time.unscaledTime * _nearFullBlinkSpeed * 2f, 1f);

        _groggyFillImage.color = Color.Lerp(_normalFillColor, Color.white, rate);
    }

    private void RefreshGroggyState()
    {
        if(_groggyPercentText != null) _groggyPercentText.gameObject.SetActive(!_target.IsGroggy);
        if(!_target.IsGroggy) _groggyFillImage.color = _normalFillColor;

        RefreshGroggyGauge();
        RefreshGroggyTime();
    }

    private void RefreshIdentity()
    {
        EnemyData data = _target.GetComponent<EnemyController>().Data;

        if(_portraitImage != null) _portraitImage.sprite = data.Portrait;
        if(_nameText != null) _nameText.text = data.DisplayName;
    }

    private void RefreshVisibility()
    {
        if(_target != null) _root.SetActive(_target.IsAlive && BattleHudVisibility.IsVisible);
    }

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
        RefreshIdentity();
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

    // 다시 살아남 알림은 바가 꺼져 있을 때도 받아야 하므로 바가 있는 동안 내내 구독한다.
    private void BindReviveEvent()
    {
        _target.Revived += OnRevived;
    }

    private void UnbindReviveEvent()
    {
        if(_target != null) _target.Revived -= OnRevived;
    }

    private void BindHudVisibilityEvent()
    {
        BattleHudVisibility.Changed += RefreshVisibility;
    }

    private void UnbindHudVisibilityEvent()
    {
        BattleHudVisibility.Changed -= RefreshVisibility;
    }

    // Fixed 모드라 네온색(0.2) 사이에 짧은 흰색(0.05)이 번쩍인다. Gradient 의 색 키는 8 개까지다.
    private static Gradient CreateNeonFlashGradient()
    {
        Color pink = new Color(1f, 0.18f, 0.6f);
        Color cyan = new Color(0f, 0.94f, 1f);
        Color lime = new Color(0.71f, 1f, 0f);
        Color yellow = new Color(1f, 0.9f, 0f);
        Gradient gradient = new Gradient { mode = GradientMode.Fixed };

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(pink, 0.2f),
                new GradientColorKey(Color.white, 0.25f),
                new GradientColorKey(cyan, 0.45f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(lime, 0.7f),
                new GradientColorKey(Color.white, 0.75f),
                new GradientColorKey(yellow, 0.95f),
                new GradientColorKey(Color.white, 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        return gradient;
    }

    // 색만 바꾸므로 Image · RawImage 모두 쓸 수 있게 공통 부모(Graphic)로 찾는다.
    private void CacheComponent()
    {
        _groggyFillImage = _groggySlider.fillRect.GetComponent<Graphic>();
        _normalFillColor = _groggyFillImage.color;
    }
}

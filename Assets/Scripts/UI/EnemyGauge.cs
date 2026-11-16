using UnityEngine;
using UnityEngine.UI;

// 적 하나의 체력 · 그로기 막대(Slider 0 ~ 1). 일반 적 머리 위 바와 화면 위쪽 보스 바가 함께 쓴다.
// 대상을 비워 두면 부모에서 찾는다. 일반 적 Prefab 의 자식으로 붙이면 따로 연결하지 않아도 된다. 보스 바는 씬의 보스를 연결한다.
// 체력 · 그로기 게이지는 적 기능의 알림이 올 때만 다시 그린다.
// 그로기 중에는 그로기 막대가 남은 그로기 시간을 보여주므로, 그로기 중에만 매 프레임 읽는다. 그로기 중에는 막대 채움 색을 그로기 색으로 바꿔 덮는다.
// 대상이 처치되면 막대 묶음을 숨긴다.
public class EnemyGauge : MonoBehaviour
{
    [Tooltip("[선택] 보여줄 적. 비워 두면 부모에서 찾는다 (일반 적 머리 위 바)")]
    [SerializeField] private EnemyHealth _target;
    [Tooltip("처치되면 숨길 막대 묶음")]
    [SerializeField] private GameObject _root;
    [SerializeField] private Slider _healthSlider;
    [SerializeField] private Slider _groggySlider;
    [Tooltip("그로기 중 그로기 막대 채움 영역에 덮어 띄울 색. 그로기가 끝나면 원래 색으로 돌아간다")]
    [SerializeField] private Color _groggyFillColor = new Color(1f, 0.5f, 0f);

    private EnemyReaction _reaction;
    // 그로기 막대 채움 영역의 이미지와 원래 색
    private Image _groggyFillImage;
    private Color _normalFillColor;

    private void Awake() => CacheComponent();
    private void OnEnable() => BindEnemyEvent();
    // 다른 오브젝트의 Awake 가 모두 끝난 뒤 처음 값을 한 번 더 그린다. OnEnable 이 대상의 처음 값보다 먼저 돌 수 있기 때문이다.
    private void Start() => RefreshAll();
    private void OnDisable() => UnbindEnemyEvent();
    private void Update() => RefreshGroggyTime();

    private void RefreshHealth(bool isImpact) => _healthSlider.value = _target.CurrentHealth / _target.MaxHealth;

    // 그로기가 아닐 때는 쌓인 게이지, 그로기 중에는 남은 시간을 보여준다.
    private void RefreshGroggyGauge()
    {
        if(_reaction.IsGroggy) return;

        _groggySlider.value = _reaction.CurrentGroggy / _reaction.MaxGroggy;
    }

    private void RefreshGroggyTime()
    {
        if(_reaction.IsGroggy) _groggySlider.value = _reaction.GroggyTimeRatio;
    }

    private void RefreshGroggyState()
    {
        _groggyFillImage.color = _reaction.IsGroggy ? _groggyFillColor : _normalFillColor;

        RefreshGroggyGauge();
        RefreshGroggyTime();
    }

    private void Hide() => _root.SetActive(false);

    // 구독할 때 현재 값으로 한 번 그린다.
    private void BindEnemyEvent()
    {
        _target.Damaged += RefreshHealth;
        _target.Died += Hide;
        _reaction.GroggyGaugeChanged += RefreshGroggyGauge;
        _reaction.GroggyEntered += RefreshGroggyState;
        _reaction.Recovered += RefreshGroggyState;
        RefreshAll();
    }

    private void RefreshAll()
    {
        _root.SetActive(_target.IsAlive);
        RefreshHealth(false);
        RefreshGroggyState();
    }

    private void UnbindEnemyEvent()
    {
        _target.Damaged -= RefreshHealth;
        _target.Died -= Hide;
        _reaction.GroggyGaugeChanged -= RefreshGroggyGauge;
        _reaction.GroggyEntered -= RefreshGroggyState;
        _reaction.Recovered -= RefreshGroggyState;
    }

    private void CacheComponent()
    {
        if(_target == null) _target = GetComponentInParent<EnemyHealth>();

        _reaction = _target.GetComponent<EnemyReaction>();
        _groggyFillImage = _groggySlider.fillRect.GetComponent<Image>();
        _normalFillColor = _groggyFillImage.color;
    }
}

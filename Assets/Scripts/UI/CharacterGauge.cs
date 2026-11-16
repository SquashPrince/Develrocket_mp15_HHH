using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 칸 하나. 초상화 · 체력 · 에너지 막대와 [선택] 필살기 게이지 숫자를 보여준다. 조작 캐릭터 칸과 대기 캐릭터 칸이 함께 쓴다.
// 체력 · 필살기 게이지는 상태 기능의 알림이 올 때만 다시 그리고, 에너지는 시간이 지나며 계속 차므로 매 프레임 읽는다.
// 맡은 캐릭터가 바뀌면 이전 캐릭터의 알림을 해제하고 새 캐릭터의 알림을 구독한다. 해제하지 않으면 다른 캐릭터의 값으로 그려지기 때문이다.
// 막대는 Slider(0 ~ 1)로 그린다. 채움 영역의 크기가 줄어드는 방식이라 막대 끝 장식(9-slice)이 줄어들어도 유지된다.
// 체력 막대 뒤의 잔상 막대는 맞아서 줄어든 체력만 잔상으로 남긴다. 맡은 캐릭터가 바뀌거나 처음 그릴 때는 잔상 없이 바로 맞춘다.
// 필살기 게이지는 막대 대신 현재 값을 1 의 자리까지 숫자로 보여준다. 조작 캐릭터 칸에만 두어, 교체하면 필드에 있는 캐릭터의 값으로 바뀐다.
// 필살기 숫자는 게이지 값이 단계 기준을 넘을 때마다 단계 색으로 바뀌고, 단계가 오르는 순간 잠깐 커졌다 돌아온다.
// 단계 기준은 게이지 값 그대로(예 : 1000)라, 화면에 보이는 숫자와 같은 기준으로 정한다.
// 맡은 캐릭터가 바뀌거나 처음 그릴 때는 그 값의 단계 색만 칠하고 강조하지 않는다. 강조 연출은 실제 시간으로 흐른다.
public class CharacterGauge : MonoBehaviour
{
    // 필살기 숫자 단계 하나. 게이지 값이 기준 이상이면 이 단계다.
    [Serializable]
    private class UltimateGaugeTier
    {
        [Tooltip("이 단계가 시작되는 게이지 값. 화면에 보이는 숫자와 같은 기준이다 (예 : 1000)")]
        [SerializeField, Min(0f)] private float _minValue;
        [SerializeField] private Color _color = Color.white;

        public float MinValue => _minValue;
        public Color Color => _color;
    }

    [SerializeField] private Image _portraitImage;
    [SerializeField] private Slider _healthSlider;
    [Tooltip("체력 막대 뒤에 겹친 잔상 막대")]
    [SerializeField] private GaugeTrail _healthTrail;
    [SerializeField] private Slider _energySlider;
    [Tooltip("[선택] 필살기 게이지 현재 값을 보여줄 글자. 대기 캐릭터 칸처럼 보이지 않을 칸은 비워 둔다")]
    [SerializeField] private TMP_Text _ultimateGaugeText;

    [Header("필살기 숫자 단계")]
    [Tooltip("[선택] 필살기 숫자 단계 목록. 기준 값이 낮은 단계부터 차례로 둔다. 비워 두면 글자 색을 바꾸지 않는다")]
    [SerializeField] private UltimateGaugeTier[] _ultimateTiers;
    [Tooltip("단계가 오르는 순간 숫자 크기 배율. 이 크기에서 원래 크기로 돌아온다")]
    [SerializeField, Min(1f)] private float _tierUpScale = 1.3f;
    [Tooltip("원래 크기로 돌아오는 데 걸리는 시간(초, 실제 시간)")]
    [SerializeField, Min(0.01f)] private float _tierUpDuration = 0.25f;

    private PlayerController _character;
    private PlayerStatus _status;
    // 지금 보여주는 필살기 숫자 단계 번호. 단계 목록에서 기준을 넘은 마지막 단계이며, 어느 단계도 넘지 않았으면 -1 이다.
    private int _ultimateTier = -1;
    // 필살기 숫자의 원래 색 · 크기. 어느 단계도 넘지 않았을 때와 강조가 끝났을 때 쓴다.
    private Color _ultimateTextBaseColor;
    private Vector3 _ultimateTextBaseScale;
    private float _tierUpTimer;

    private void Awake() => CacheComponent();
    private void OnEnable() => BindStatusEvent();
    // 다른 오브젝트의 Awake 가 모두 끝난 뒤 처음 값을 한 번 더 그린다. OnEnable 이 캐릭터의 처음 값보다 먼저 돌 수 있기 때문이다.
    private void Start() => RefreshAll();
    private void OnDisable() => UnbindStatusEvent();
    private void Update()
    {
        RefreshEnergy();
        UpdateTierUpScale();
    }

    // 이 칸이 보여줄 캐릭터를 정한다. 이전 캐릭터의 알림은 해제하고 새 캐릭터 값으로 바로 그린다.
    public void Bind(PlayerController character)
    {
        UnbindStatusEvent();

        _character = character;
        _status = character.Status;
        _portraitImage.sprite = character.Character.Portrait;

        // 꺼져 있는 칸은 켜질 때(OnEnable) 구독한다. 여기서도 구독하면 두 번 구독된다.
        if(isActiveAndEnabled) BindStatusEvent();
    }

    private void OnHealthChanged() => RefreshHealth(false);

    // isSnap : 잔상 없이 바로 맞출지. 체력 변경 알림이 아닌 처음 그리기 · 캐릭터 바뀜에서 켠다.
    private void RefreshHealth(bool isSnap)
    {
        float ratio = _status.CurrentHealth / _status.MaxHealth;
        _healthSlider.value = ratio;

        if(isSnap) _healthTrail.Snap(ratio);
        else _healthTrail.SetValue(ratio);
    }

    private void RefreshEnergy()
    {
        if(_status == null) return;

        _energySlider.value = _status.CurrentEnergy / _status.MaxEnergy;
    }

    private void OnUltimateGaugeChanged() => RefreshUltimateGauge(false);

    // 현재 값을 소수점 아래를 버리고 1 의 자리까지 보여준다.
    // 조작 중인 캐릭터일 때만 그린다. 교체되어 나가는 캐릭터가 필드에 남아 공격하면 그 캐릭터의 게이지가 차는데, 그 값으로 덮어 그리지 않기 위해서다.
    // isSnap : 단계가 올라도 강조하지 않을지. 게이지 변경 알림이 아닌 처음 그리기 · 캐릭터 바뀜에서 켠다.
    private void RefreshUltimateGauge(bool isSnap)
    {
        if(_ultimateGaugeText == null || _character != _character.Party.Controlled) return;

        _ultimateGaugeText.text = Mathf.FloorToInt(_status.CurrentUltimateGauge).ToString();
        RefreshUltimateTier(isSnap);
    }

    // 게이지 값으로 단계를 다시 정하고 단계 색을 칠한다. 어느 단계도 넘지 않았으면 원래 색으로 돌린다.
    // 단계가 올랐고 강조할 때면 크기 강조를 처음부터 시작한다.
    private void RefreshUltimateTier(bool isSnap)
    {
        if(_ultimateTiers == null || _ultimateTiers.Length == 0) return;

        int tier = FindUltimateTier(_status.CurrentUltimateGauge);

        if(tier > _ultimateTier && !isSnap) _tierUpTimer = _tierUpDuration;

        _ultimateTier = tier;

        _ultimateGaugeText.color = tier >= 0 ? _ultimateTiers[tier].Color : _ultimateTextBaseColor;
    }

    // value 가 기준을 넘은 마지막 단계 번호. 어느 단계도 넘지 않았으면 -1 이다.
    private int FindUltimateTier(float value)
    {
        int tier = -1;

        for(int i = 0; i < _ultimateTiers.Length; i++)
        {
            if(value >= _ultimateTiers[i].MinValue) tier = i;
        }

        return tier;
    }

    // 남은 강조 시간의 비율만큼 강조 크기에서 원래 크기 쪽으로 돌아온다.
    private void UpdateTierUpScale()
    {
        if(_tierUpTimer <= 0f) return;

        _tierUpTimer = Mathf.Max(0f, _tierUpTimer - Time.unscaledDeltaTime);
        _ultimateGaugeText.transform.localScale = _ultimateTextBaseScale * Mathf.Lerp(1f, _tierUpScale, _tierUpTimer / _tierUpDuration);
    }

    private void RefreshAll()
    {
        if(_status == null) return;

        RefreshHealth(true);
        RefreshUltimateGauge(true);
        RefreshEnergy();
    }

    // 구독할 때 현재 값으로 한 번 그려, 알림이 오기 전에도 막대가 맞게 보이게 한다.
    private void BindStatusEvent()
    {
        if(_status == null) return;

        _status.HealthChanged += OnHealthChanged;
        _status.UltimateGaugeChanged += OnUltimateGaugeChanged;
        RefreshAll();
    }

    private void UnbindStatusEvent()
    {
        if(_status == null) return;

        _status.HealthChanged -= OnHealthChanged;
        _status.UltimateGaugeChanged -= OnUltimateGaugeChanged;
    }

    private void CacheComponent()
    {
        if(_ultimateGaugeText == null) return;

        _ultimateTextBaseColor = _ultimateGaugeText.color;
        _ultimateTextBaseScale = _ultimateGaugeText.transform.localScale;
    }
}

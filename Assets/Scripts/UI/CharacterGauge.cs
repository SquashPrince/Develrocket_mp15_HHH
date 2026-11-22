using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 칸 하나. 초상화 · 체력 · 에너지 막대와 [선택] 필살기 게이지 숫자를 보여준다. 조작 캐릭터 칸과 대기 캐릭터 칸이 함께 쓴다.
// [선택] 이름 · 체력 숫자 · 에너지 숫자 글자와, 캐릭터 상징색을 곱할 이미지(칸 틀 · 이름표 등)를 둘 수 있다. 맡은 캐릭터가 바뀌면 함께 바뀐다.
// 체력 · 필살기 게이지는 상태 기능의 알림이 올 때만 다시 그리고, 에너지는 시간이 지나며 계속 차므로 매 프레임 읽는다.
// 쓰러진 캐릭터는 초상화를 회색으로 어둡게 보여준다. 체력이 바뀔 때 함께 정하므로, 교체로 칸의 캐릭터가 바뀌어도 맞게 보인다.
// 맡은 캐릭터가 바뀌면 이전 캐릭터의 알림을 해제하고 새 캐릭터의 알림을 구독한다. 해제하지 않으면 다른 캐릭터의 값으로 그려지기 때문이다.
// 막대는 Slider(0 ~ 1)로 그린다. 채움 영역은 사선 마스크(Mask)로 쓰고, 그 안의 채움 이미지는 막대 전체 길이로 고정한다.
// 채움 영역이 줄면 이미지가 찌그러지지 않고 사선으로 잘려, 0 에 가까워도 뒤의 틀과 모양이 맞는다.
// 체력 막대 뒤의 잔상 막대는 맞아서 줄어든 체력만 잔상으로 남긴다. 맡은 캐릭터가 바뀌거나 처음 그릴 때는 잔상 없이 바로 맞춘다.
// 필살기 게이지는 막대 대신 현재 값을 1 의 자리까지 숫자로 보여준다. 조작 캐릭터 칸에만 두어, 교체하면 필드에 있는 캐릭터의 값으로 바뀐다.
// 필살기 숫자는 게이지 값이 단계 기준을 넘을 때마다 단계 색 그라데이션(TMP Color Gradient)으로 바뀌고, 단계가 오르는 순간 잠깐 커졌다 돌아온다.
// 단계 기준은 게이지 값 그대로(예 : 1000)라, 화면에 보이는 숫자와 같은 기준으로 정한다.
// 맡은 캐릭터가 바뀌거나 처음 그릴 때는 그 값의 단계 색만 칠하고 강조하지 않는다. 강조 연출은 실제 시간으로 흐른다.
// 에너지 : 특수 스킬을 쓸 만큼 에너지가 찼으면 막대 채움 색을 스킬 사용 가능 색으로 덮는다. 버튼 HUD 의 스킬 아이콘과 같은 기준이다.
//   [선택] 눈금을 연결하면 맡은 캐릭터의 특수 스킬 에너지 요구량 위치에 놓는다. 캐릭터마다 요구량이 달라 맡은 캐릭터가 바뀔 때 다시 놓는다.
public class CharacterGauge : MonoBehaviour
{
    // 필살기 숫자 단계 하나. 게이지 값이 기준 이상이면 이 단계다.
    [Serializable]
    private class UltimateGaugeTier
    {
        [Tooltip("이 단계가 시작되는 게이지 값. 화면에 보이는 숫자와 같은 기준이다 (예 : 1000)")]
        [SerializeField, Min(0f)] private float _minValue;
        [Tooltip("이 단계의 숫자 색 그라데이션 (Create > TextMeshPro > Color Gradient)")]
        [SerializeField] private TMP_ColorGradient _gradient;

        public float MinValue => _minValue;
        public TMP_ColorGradient Gradient => _gradient;
    }

    [SerializeField] private Image _portraitImage;
    [Tooltip("쓰러진 캐릭터의 초상화에 곱할 색. 회색으로 어둡게 보인다")]
    [SerializeField] private Color _downedPortraitColor = new Color(0.35f, 0.35f, 0.35f);
    [SerializeField] private Slider _healthSlider;
    [Tooltip("체력 막대 뒤에 겹친 잔상 막대")]
    [SerializeField] private GaugeTrail _healthTrail;
    [SerializeField] private Slider _energySlider;
    [Tooltip("에너지 막대의 채움 이미지(채움 영역 마스크 안의 자식). 스킬을 쓸 만큼 차면 색이 바뀐다")]
    [SerializeField] private Image _energyFillImage;
    [Tooltip("[선택] 필살기 게이지 현재 값을 보여줄 글자. 대기 캐릭터 칸처럼 보이지 않을 칸은 비워 둔다")]
    [SerializeField] private TMP_Text _ultimateGaugeText;

    [Header("이름 · 숫자 · 상징색")]
    [Tooltip("[선택] 캐릭터 이름 글자")]
    [SerializeField] private TMP_Text _nameText;
    [Tooltip("[선택] 체력 숫자 글자 (현재 / 최대)")]
    [SerializeField] private TMP_Text _healthText;
    [Tooltip("[선택] 에너지 숫자 글자 (현재 / 최대)")]
    [SerializeField] private TMP_Text _energyText;
    [Tooltip("[선택] 캐릭터 상징색을 곱할 이미지들 (칸 틀 · 이름표 · 체력 막대 채움 등). 흰색 · 회색으로 그린 이미지에 쓴다")]
    [SerializeField] private Graphic[] _themeGraphics;

    [Header("에너지")]
    [Tooltip("특수 스킬을 쓸 만큼 에너지가 찼을 때 에너지 막대 채움 영역에 덮어 띄울 색. 모자라면 원래 색으로 돌아간다")]
    [SerializeField] private Color _skillReadyFillColor = new Color(1f, 0.85f, 0.2f);
    [Tooltip("[선택] 특수 스킬 에너지 요구량 위치에 놓을 눈금. 막대 가로 길이를 가득 채운 오브젝트(Fill Area 등)의 자식으로 둔다")]
    [SerializeField] private RectTransform _energyCostMarker;

    [Header("필살기 숫자 단계")]
    [Tooltip("[선택] 필살기 숫자 단계 목록. 기준 값이 낮은 단계부터 차례로 둔다. 비워 두면 글자 색을 바꾸지 않는다")]
    [SerializeField] private UltimateGaugeTier[] _ultimateTiers;
    [Tooltip("단계가 오르는 순간 숫자 크기 배율. 이 크기에서 원래 크기로 돌아온다")]
    [SerializeField, Min(1f)] private float _tierUpScale = 1.3f;
    [Tooltip("원래 크기로 돌아오는 데 걸리는 시간(초, 실제 시간)")]
    [SerializeField, Min(0.01f)] private float _tierUpDuration = 0.25f;

    private PlayerController _character;
    private PlayerStatus _status;
    // 초상화의 원래 색. 쓰러지지 않은 캐릭터에 쓴다.
    private Color _portraitBaseColor;
    // 에너지 막대 채움 이미지의 원래 색
    private Color _energyBaseFillColor;
    // 지금 보여주는 필살기 숫자 단계 번호. 단계 목록에서 기준을 넘은 마지막 단계이며, 어느 단계도 넘지 않았으면 -1 이다.
    private int _ultimateTier = -1;
    // 필살기 숫자의 원래 색 · 크기. 어느 단계도 넘지 않았을 때와 강조가 끝났을 때 쓴다.
    private Color _ultimateTextBaseColor;
    // 필살기 숫자의 원래 그라데이션 설정. 어느 단계에도 못 미칠 때 되돌린다.
    private TMP_ColorGradient _ultimateTextBaseGradient;
    private bool _ultimateTextBaseGradientEnabled;
    private Vector3 _ultimateTextBaseScale;
    private float _tierUpTimer;
    // 지금 보여주는 에너지 숫자. 에너지는 매 프레임 읽으므로, 보이는 숫자가 바뀔 때만 글자를 다시 쓴다.
    private int _shownEnergy = -1;

    // 맡은 캐릭터의 특수 스킬 에너지 요구량
    private float SkillEnergyCost => _character.Character.SpecialSkill.EnergyCost;

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
        _shownEnergy = -1;
        RefreshIdentity();

        // 꺼져 있는 칸은 켜질 때(OnEnable) 구독한다. 여기서도 구독하면 두 번 구독된다.
        if(isActiveAndEnabled) BindStatusEvent();
    }

    private void OnHealthChanged() => RefreshHealth(false);

    // isSnap : 잔상 없이 바로 맞출지. 체력 변경 알림이 아닌 처음 그리기 · 캐릭터 바뀜에서 켠다.
    private void RefreshHealth(bool isSnap)
    {
        float ratio = _status.CurrentHealth / _status.MaxHealth;
        _healthSlider.value = ratio;
        _portraitImage.color = _status.IsDowned ? _downedPortraitColor : _portraitBaseColor;

        if(isSnap) _healthTrail.Snap(ratio);
        else _healthTrail.SetValue(ratio);

        // 숫자는 소수점 아래를 올려, 체력이 조금이라도 남아 있으면 0 으로 보이지 않게 한다.
        if(_healthText != null) _healthText.SetText("{0} / {1}", Mathf.CeilToInt(_status.CurrentHealth), Mathf.CeilToInt(_status.MaxHealth));
    }

    private void RefreshEnergy()
    {
        if(_status == null) return;

        _energySlider.value = _status.CurrentEnergy / _status.MaxEnergy;
        _energyFillImage.color = _status.CurrentEnergy >= SkillEnergyCost ? _skillReadyFillColor : _energyBaseFillColor;
        RefreshEnergyText();
    }

    // 소수점 아래를 버린 숫자가 바뀔 때만 다시 쓴다. 에너지는 매 프레임 조금씩 차기 때문이다.
    private void RefreshEnergyText()
    {
        if(_energyText == null) return;

        int energy = Mathf.FloorToInt(_status.CurrentEnergy);

        if(energy == _shownEnergy) return;

        _shownEnergy = energy;
        _energyText.SetText("{0} / {1}", energy, Mathf.FloorToInt(_status.MaxEnergy));
    }

    // 맡은 캐릭터의 이름과 상징색을 칠한다.
    private void RefreshIdentity()
    {
        if(_nameText != null) _nameText.text = _character.Character.DisplayName;

        if(_themeGraphics == null) return;

        foreach(Graphic graphic in _themeGraphics)
        {
            graphic.color = _character.Character.ThemeColor;
        }
    }

    // 눈금을 맡은 캐릭터의 특수 스킬 에너지 요구량 위치(막대 비율)에 놓는다.
    private void RefreshEnergyCostMarker()
    {
        if(_energyCostMarker == null) return;

        float ratio = Mathf.Clamp01(SkillEnergyCost / _status.MaxEnergy);

        _energyCostMarker.anchorMin = new Vector2(ratio, _energyCostMarker.anchorMin.y);
        _energyCostMarker.anchorMax = new Vector2(ratio, _energyCostMarker.anchorMax.y);
        _energyCostMarker.anchoredPosition = new Vector2(0f, _energyCostMarker.anchoredPosition.y);
    }

    private void OnUltimateGaugeChanged() => RefreshUltimateGauge(false);

    // 현재 값을 소수점 아래를 버리고 1 의 자리까지 보여준다.
    // 조작 중인 캐릭터일 때만 그린다. 교체되어 나가는 캐릭터가 필드에 남아 공격하면 그 캐릭터의 게이지가 차는데, 그 값으로 덮어 그리지 않기 위해서다.
    // isSnap : 단계가 올라도 강조하지 않을지. 게이지 변경 알림이 아닌 처음 그리기 · 캐릭터 바뀜에서 켠다.
    private void RefreshUltimateGauge(bool isSnap)
    {
        if(_ultimateGaugeText == null || _character != _character.Party.Controlled) return;

        _ultimateGaugeText.text = Mathf.FloorToInt(_status.CurrentUltimateGauge).ToString() + "<size=40>PTS</size>";
        RefreshUltimateTier(isSnap);
    }

    // 게이지 값으로 단계를 다시 정하고 단계 그라데이션을 씌운다. 어느 단계도 넘지 않았으면 원래 색으로 돌린다.
    // 단계가 올랐고 강조할 때면 크기 강조를 처음부터 시작한다.
    private void RefreshUltimateTier(bool isSnap)
    {
        if(_ultimateTiers == null || _ultimateTiers.Length == 0) return;

        int tier = FindUltimateTier(_status.CurrentUltimateGauge);

        if(tier > _ultimateTier && !isSnap) _tierUpTimer = _tierUpDuration;

        _ultimateTier = tier;

        // 단계 안 : 글자 색을 흰색으로 두고 그라데이션을 씌운다(그라데이션에 글자 색이 곱해지기 때문). 단계 밖 : 원래 색 · 그라데이션으로 돌린다.
        bool isInTier = tier >= 0;

        _ultimateGaugeText.color = isInTier ? Color.white : _ultimateTextBaseColor;
        _ultimateGaugeText.colorGradientPreset = isInTier ? _ultimateTiers[tier].Gradient : _ultimateTextBaseGradient;
        _ultimateGaugeText.enableVertexGradient = isInTier || _ultimateTextBaseGradientEnabled;
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
        RefreshEnergyCostMarker();
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
        _portraitBaseColor = _portraitImage.color;
        _energyBaseFillColor = _energyFillImage.color;

        if(_ultimateGaugeText == null) return;

        _ultimateTextBaseColor = _ultimateGaugeText.color;
        _ultimateTextBaseGradient = _ultimateGaugeText.colorGradientPreset;
        _ultimateTextBaseGradientEnabled = _ultimateGaugeText.enableVertexGradient;
        _ultimateTextBaseScale = _ultimateGaugeText.transform.localScale;
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 칸 하나. 초상화 · 체력 · 에너지 막대와 필살기 게이지 숫자를 보여준다. 조작 · 대기 캐릭터 칸이 함께 쓴다.
// 체력 · 필살기 게이지는 알림이 올 때만, 에너지는 계속 차므로 매 프레임 그린다.
// 막대 채움 영역을 사선 마스크로 쓰고 채움 이미지는 전체 길이로 고정해, 줄어들어도 찌그러지지 않고 사선으로 잘린다.
// 자세한 흐름 : Docs/Technical/UI/BattleHud.md
public class CharacterGauge : MonoBehaviour
{
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
    private Color _portraitBaseColor;
    private Color _energyBaseFillColor;
    // 어느 단계도 넘지 않았으면 -1
    private int _ultimateTier = -1;
    // 어느 단계에도 못 미칠 때 되돌릴 원래 모양
    private Color _ultimateTextBaseColor;
    private TMP_ColorGradient _ultimateTextBaseGradient;
    private bool _ultimateTextBaseGradientEnabled;
    private Vector3 _ultimateTextBaseScale;
    private float _tierUpTimer;
    // 보이는 숫자가 바뀔 때만 글자를 다시 쓴다.
    private int _shownEnergy = -1;

    private float SkillEnergyCost => _character.Character.SpecialSkill.EnergyCost;

    private void Awake() => CacheComponent();
    private void OnEnable() => BindStatusEvent();
    // OnEnable 이 캐릭터의 처음 값보다 먼저 돌 수 있어 Start 에서 한 번 더 그린다.
    private void Start() => RefreshAll();
    private void OnDisable() => UnbindStatusEvent();
    private void Update()
    {
        RefreshEnergy();
        UpdateTierUpScale();
    }

    // 이전 캐릭터의 알림을 해제하지 않으면 다른 캐릭터의 값으로 그려진다.
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

    // isSnap : 처음 그리기 · 캐릭터 바뀜에서 잔상 없이 바로 맞춘다.
    private void RefreshHealth(bool isSnap)
    {
        float ratio = _status.CurrentHealth / _status.MaxHealth;
        _healthSlider.value = ratio;
        // 체력과 함께 정해, 교체로 칸의 캐릭터가 바뀌어도 쓰러짐이 맞게 보인다.
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
        // 버튼 HUD 의 스킬 아이콘과 같은 기준이다.
        _energyFillImage.color = _status.CurrentEnergy >= SkillEnergyCost ? _skillReadyFillColor : _energyBaseFillColor;
        RefreshEnergyText();
    }

    private void RefreshEnergyText()
    {
        if(_energyText == null) return;

        int energy = Mathf.FloorToInt(_status.CurrentEnergy);

        if(energy == _shownEnergy) return;

        _shownEnergy = energy;
        _energyText.SetText("{0} / {1}", energy, Mathf.FloorToInt(_status.MaxEnergy));
    }

    private void RefreshIdentity()
    {
        if(_nameText != null) _nameText.text = _character.Character.DisplayName;

        if(_themeGraphics == null) return;

        foreach(Graphic graphic in _themeGraphics)
        {
            graphic.color = _character.Character.ThemeColor;
        }
    }

    // 캐릭터마다 요구량이 달라 맡은 캐릭터가 바뀔 때 다시 놓는다.
    private void RefreshEnergyCostMarker()
    {
        if(_energyCostMarker == null) return;

        float ratio = Mathf.Clamp01(SkillEnergyCost / _status.MaxEnergy);

        _energyCostMarker.anchorMin = new Vector2(ratio, _energyCostMarker.anchorMin.y);
        _energyCostMarker.anchorMax = new Vector2(ratio, _energyCostMarker.anchorMax.y);
        _energyCostMarker.anchoredPosition = new Vector2(0f, _energyCostMarker.anchoredPosition.y);
    }

    private void OnUltimateGaugeChanged() => RefreshUltimateGauge(false);

    // 퇴장 중인 이전 캐릭터가 공격해 찬 게이지로 덮어 그리지 않게 조작 중인 캐릭터일 때만 그린다.
    // isSnap : 처음 그리기 · 캐릭터 바뀜에서는 단계가 올라도 강조하지 않는다.
    private void RefreshUltimateGauge(bool isSnap)
    {
        if(_ultimateGaugeText == null || _character != _character.Party.Controlled) return;

        _ultimateGaugeText.text = Mathf.FloorToInt(_status.CurrentUltimateGauge).ToString() + "<size=40>PTS</size>";
        RefreshUltimateTier(isSnap);
    }

    private void RefreshUltimateTier(bool isSnap)
    {
        if(_ultimateTiers == null || _ultimateTiers.Length == 0) return;

        int tier = FindUltimateTier(_status.CurrentUltimateGauge);

        if(tier > _ultimateTier && !isSnap) _tierUpTimer = _tierUpDuration;

        _ultimateTier = tier;

        // 그라데이션에 글자 색이 곱해지므로 단계 안에서는 글자 색을 흰색으로 둔다.
        bool isInTier = tier >= 0;

        _ultimateGaugeText.color = isInTier ? Color.white : _ultimateTextBaseColor;
        _ultimateGaugeText.colorGradientPreset = isInTier ? _ultimateTiers[tier].Gradient : _ultimateTextBaseGradient;
        _ultimateGaugeText.enableVertexGradient = isInTier || _ultimateTextBaseGradientEnabled;
    }

    private int FindUltimateTier(float value)
    {
        int tier = -1;

        for(int i = 0; i < _ultimateTiers.Length; i++)
        {
            if(value >= _ultimateTiers[i].MinValue) tier = i;
        }

        return tier;
    }

    private void UpdateTierUpScale()
    {
        if(_tierUpTimer <= 0f) return;

        ScalePop.Tick(ref _tierUpTimer, _tierUpDuration, _tierUpScale, _ultimateGaugeText.transform, _ultimateTextBaseScale);
    }

    private void RefreshAll()
    {
        if(_status == null) return;

        RefreshHealth(true);
        RefreshUltimateGauge(true);
        RefreshEnergy();
        RefreshEnergyCostMarker();
    }

    // 알림이 오기 전에도 맞게 보이도록 구독할 때 한 번 그린다.
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

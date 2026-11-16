using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 칸 하나. 초상화 · 체력 · 에너지 막대와 [선택] 필살기 게이지 숫자를 보여준다. 조작 캐릭터 칸과 대기 캐릭터 칸이 함께 쓴다.
// 체력 · 필살기 게이지는 상태 기능의 알림이 올 때만 다시 그리고, 에너지는 시간이 지나며 계속 차므로 매 프레임 읽는다.
// 맡은 캐릭터가 바뀌면 이전 캐릭터의 알림을 해제하고 새 캐릭터의 알림을 구독한다. 해제하지 않으면 다른 캐릭터의 값으로 그려지기 때문이다.
// 막대는 Slider(0 ~ 1)로 그린다. 채움 영역의 크기가 줄어드는 방식이라 막대 끝 장식(9-slice)이 줄어들어도 유지된다.
// 체력 막대 뒤의 잔상 막대는 맞아서 줄어든 체력만 잔상으로 남긴다. 맡은 캐릭터가 바뀌거나 처음 그릴 때는 잔상 없이 바로 맞춘다.
// 필살기 게이지는 막대 대신 현재 값을 1 의 자리까지 숫자로 보여준다. 조작 캐릭터 칸에만 두어, 교체하면 필드에 있는 캐릭터의 값으로 바뀐다.
public class CharacterGauge : MonoBehaviour
{
    [SerializeField] private Image _portraitImage;
    [SerializeField] private Slider _healthSlider;
    [Tooltip("체력 막대 뒤에 겹친 잔상 막대")]
    [SerializeField] private GaugeTrail _healthTrail;
    [SerializeField] private Slider _energySlider;
    [Tooltip("[선택] 필살기 게이지 현재 값을 보여줄 글자. 대기 캐릭터 칸처럼 보이지 않을 칸은 비워 둔다")]
    [SerializeField] private TMP_Text _ultimateGaugeText;

    private PlayerController _character;
    private PlayerStatus _status;

    private void OnEnable() => BindStatusEvent();
    // 다른 오브젝트의 Awake 가 모두 끝난 뒤 처음 값을 한 번 더 그린다. OnEnable 이 캐릭터의 처음 값보다 먼저 돌 수 있기 때문이다.
    private void Start() => RefreshAll();
    private void OnDisable() => UnbindStatusEvent();
    private void Update() => RefreshEnergy();

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

    // 현재 값을 소수점 아래를 버리고 1 의 자리까지 보여준다.
    // 조작 중인 캐릭터일 때만 그린다. 교체되어 나가는 캐릭터가 필드에 남아 공격하면 그 캐릭터의 게이지가 차는데, 그 값으로 덮어 그리지 않기 위해서다.
    private void RefreshUltimateGauge()
    {
        if(_ultimateGaugeText == null || _character != _character.Party.Controlled) return;

        _ultimateGaugeText.text = Mathf.FloorToInt(_status.CurrentUltimateGauge).ToString();
    }

    private void RefreshAll()
    {
        if(_status == null) return;

        RefreshHealth(true);
        RefreshUltimateGauge();
        RefreshEnergy();
    }

    // 구독할 때 현재 값으로 한 번 그려, 알림이 오기 전에도 막대가 맞게 보이게 한다.
    private void BindStatusEvent()
    {
        if(_status == null) return;

        _status.HealthChanged += OnHealthChanged;
        _status.UltimateGaugeChanged += RefreshUltimateGauge;
        RefreshAll();
    }

    private void UnbindStatusEvent()
    {
        if(_status == null) return;

        _status.HealthChanged -= OnHealthChanged;
        _status.UltimateGaugeChanged -= RefreshUltimateGauge;
    }
}

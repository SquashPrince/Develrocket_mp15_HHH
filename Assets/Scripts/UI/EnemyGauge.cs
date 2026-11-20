using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 적 하나의 체력 · 그로기 막대(Slider 0 ~ 1). 일반 적 머리 위 바와 화면 위쪽 보스 바가 함께 쓴다.
// 적 바는 적(EnemyController)이 등장할 때 그 적이 연결한 HUD 아래에 직접 만들고 대상을 넘겨준다(SetTarget).
//   보스 바는 화면 Canvas, 머리 위 바는 적 몸에 붙은 월드 공간 Canvas 아래에 만들어진다.
//   대상은 넘겨받을 때만 정한다. 만들어지는 순간에는 자식(잔상 막대 등)이 아직 준비되지 않았을 수 있어, 그때 그리면 오류가 나기 때문이다.
// 체력 · 그로기 게이지는 적 기능의 알림이 올 때만 다시 그린다.
// 그로기가 아닐 때는 쌓인 그로기 게이지를 퍼센트 숫자로도 보여주고, 그로기 중에는 숫자를 숨긴다.
// 그로기 중에는 그로기 막대가 남은 그로기 시간을 보여주므로, 그로기 중에만 매 프레임 읽는다.
// 그로기 중 막대 채움은 콤보 스킬 연계가 남았는지에 따라 바꾼다. 연계를 쓴 순간을 알리는 알림이 없어, 그로기 중 매 프레임 읽을 때 함께 확인한다.
//   연계가 남음 : 무지개 Gradient 로 만든 텍스처를 채움(RawImage)에 씌우고 UV 를 실제 시간으로 흘린다. 콤보 선택으로 게임 시간이 느려져도 흐른다.
//     UV 너비를 막대 값에 맞춰 줄여, 막대가 줄어도 무지개가 찌그러지지 않고 잘려 보이게 한다.
//   연계를 다 씀 : 텍스처를 빼고 회색으로 덮는다. 그로기가 끝나면 텍스처 없이 원래 색으로 돌아간다.
//   텍스처는 처음 켜질 때 한 번 만들고, 바가 파괴될 때 지운다.
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
    [Tooltip("그로기 중, 콤보 스킬 연계가 남아 있을 때 그로기 막대 채움에 흘릴 무지개. 끝 색과 처음 색을 같게 두면 이음매 없이 반복된다")]
    [SerializeField] private Gradient _comboReadyGradient = CreateRainbowGradient();
    [Tooltip("무지개가 흐르는 속도 (초당 무지개 몇 바퀴, 실제 시간). 음수면 반대로 흐른다")]
    [SerializeField] private float _comboReadyScrollSpeed = 0.5f;
    [Tooltip("막대가 가득 찼을 때 무지개가 반복되는 횟수")]
    [SerializeField, Min(0.1f)] private float _comboReadyRepeat = 1f;
    [Tooltip("그로기 중, 콤보 스킬 연계를 모두 썼거나 이어받을 대기 캐릭터가 없을 때 그로기 막대 채움 영역에 덮어 띄울 색")]
    [SerializeField] private Color _comboUsedFillColor = new Color(0.5f, 0.5f, 0.5f);

    // 보여줄 적. 바를 만든 적이 넘겨준다.
    private EnemyState _target;
    // 그로기 막대 채움 영역의 이미지와 원래 색, 무지개 텍스처와 지금까지 흐른 정도
    private RawImage _groggyFillImage;
    private Texture2D _comboReadyTexture;
    private Color _normalFillColor;
    private float _comboReadyOffset;

    // HUD 숨김 알림은 바가 있는 동안 내내 받는다. 막대 묶음이 바 자신이면 숨길 때 이 컴포넌트도 꺼지므로, 켜짐 · 꺼짐에 묶으면 다시 보여 달라는 알림을 받지 못한다.
    private void Awake()
    {
        CacheComponent();
        BindHudVisibilityEvent();
    }

    private void OnDestroy()
    {
        UnbindHudVisibilityEvent();
        Destroy(_comboReadyTexture);
    }
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

        if(_target.HasRemainingComboChain) ScrollComboReadyFill();
        else SetGroggyFillColor(_comboUsedFillColor);
    }

    // 무지개 텍스처를 씌우고, 실제 시간으로 흘린 위치에서 막대 값만큼의 너비를 보여준다.
    private void ScrollComboReadyFill()
    {
        _comboReadyOffset = Mathf.Repeat(_comboReadyOffset - _comboReadyScrollSpeed * Time.unscaledDeltaTime, 1f);

        _groggyFillImage.texture = _comboReadyTexture;
        _groggyFillImage.color = Color.white;
        _groggyFillImage.uvRect = new Rect(_comboReadyOffset, 0f, _groggySlider.value * _comboReadyRepeat, 1f);
    }

    // 텍스처 없이 단색으로 채운다.
    private void SetGroggyFillColor(Color color)
    {
        _groggyFillImage.texture = null;
        _groggyFillImage.color = color;
    }

    // 그로기에 들어가거나 풀릴 때 퍼센트 숫자를 켜고 끄고, 막대를 그 상태에 맞게 다시 그린다.
    private void RefreshGroggyState()
    {
        if(_groggyPercentText != null) _groggyPercentText.gameObject.SetActive(!_target.IsGroggy);
        if(!_target.IsGroggy) SetGroggyFillColor(_normalFillColor);

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

    // Gradient 를 가로 한 줄 텍스처로 굽는다. 반복 모드라 UV 가 1 을 넘으면 처음부터 다시 그려진다.
    private Texture2D CreateGradientTexture(Gradient gradient)
    {
        const int WIDTH = 256;

        Texture2D texture = new Texture2D(WIDTH, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        for(int x = 0; x < WIDTH; x++)
        {
            texture.SetPixel(x, 0, gradient.Evaluate(x / (WIDTH - 1f)));
        }

        texture.Apply();
        return texture;
    }

    // 무지개 기본값 : 빨강 → 노랑 → 초록 → 하늘 → 파랑 → 보라 → 빨강. Gradient 의 색 키는 8 개까지다.
    private static Gradient CreateRainbowGradient()
    {
        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.red, 0f),
                new GradientColorKey(Color.yellow, 1f / 6f),
                new GradientColorKey(Color.green, 2f / 6f),
                new GradientColorKey(Color.cyan, 3f / 6f),
                new GradientColorKey(Color.blue, 4f / 6f),
                new GradientColorKey(Color.magenta, 5f / 6f),
                new GradientColorKey(Color.red, 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        return gradient;
    }

    // 채움은 무지개 텍스처를 씌울 수 있도록 Image 가 아닌 RawImage 다.
    private void CacheComponent()
    {
        _groggyFillImage = _groggySlider.fillRect.GetComponent<RawImage>();
        _normalFillColor = _groggyFillImage.color;
        _comboReadyTexture = CreateGradientTexture(_comboReadyGradient);
    }
}

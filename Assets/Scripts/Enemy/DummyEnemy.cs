using UnityEngine;

// 공격하지 않는 더미 적. 맞으면 잠깐 색이 바뀌어 적중을 눈으로 확인할 수 있다.
// 체력은 적 체력 기능이 맡는다. 적 체력에서 처치되지 않게 설정해, 체력이 0 이 되어도 계속 맞힐 수 있게 둔다.
// 피격 반응(깜빡임)과 상태 표시(예고 · 경직 · 그로기 색)를 나눈다. 상태 표시는 공격 기능이 기본 색으로 정하고,
// 깜빡임은 끝나면 그때의 기본 색으로 돌아간다. 그래서 경직 · 그로기 중에 맞아도 상태 색이 사라지지 않는다.
[RequireComponent(typeof(EnemyHealth))]
public class DummyEnemy : MonoBehaviour
{
    [SerializeField] private Renderer _renderer;
    [SerializeField] private Color _hitColor = Color.red;
    [Tooltip("맞은 색을 유지하는 시간(초)")]
    [SerializeField, Min(0f)] private float _flashDuration = 0.15f;

    private EnemyHealth _health;
    private Color _originalColor;
    private Color _baseColor;
    private float _flashTimer;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void OnEnable() => BindHealthEvent();
    private void OnDisable() => UnbindHealthEvent();
    private void Update() => UpdateFlash();

    // 상태 표시 색을 정한다. 깜빡이는 중이면 깜빡임이 끝난 뒤 이 색으로 돌아간다.
    public void SetBaseColor(Color color)
    {
        _baseColor = color;

        if(_flashTimer <= 0f) _renderer.material.color = color;
    }

    // 상태 표시를 끝내고 처음 색으로 돌린다.
    public void ResetBaseColor() => SetBaseColor(_originalColor);

    // 적 체력이 맞았다고 알리면 깜빡인다. 강타도 같은 색으로 깜빡인다.
    private void OnDamaged(bool isImpact)
    {
        _renderer.material.color = _hitColor;
        _flashTimer = _flashDuration;
    }

    private void UpdateFlash()
    {
        if(_flashTimer <= 0f) return;

        // 완벽 회피 슬로모션 동안 느려지도록 적 전용 시간을 쓴다.
        _flashTimer -= EnemyTime.DeltaTime;

        if(_flashTimer <= 0f) _renderer.material.color = _baseColor;
    }

    private void CacheComponent()
    {
        _health = GetComponent<EnemyHealth>();
    }

    private void Init()
    {
        _originalColor = _renderer.material.color;
        _baseColor = _originalColor;
    }

    private void BindHealthEvent()
    {
        _health.Damaged += OnDamaged;
    }

    private void UnbindHealthEvent()
    {
        _health.Damaged -= OnDamaged;
    }
}

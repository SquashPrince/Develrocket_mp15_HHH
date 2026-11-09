using UnityEngine;

// 공격하지 않는 더미 적. 맞으면 잠깐 색이 바뀌어 적중을 눈으로 확인할 수 있다.
public class DummyEnemy : MonoBehaviour
{
    [SerializeField] private Renderer _renderer;
    [SerializeField] private Color _hitColor = Color.red;
    [Tooltip("맞은 색을 유지하는 시간(초)")]
    [SerializeField, Min(0f)] private float _flashDuration = 0.15f;

    private Color _originalColor;
    private float _flashTimer;

    private void Start() => Init();
    private void Update() => UpdateFlash();

    public void TakeHit()
    {
        _renderer.material.color = _hitColor;
        _flashTimer = _flashDuration;
    }

    private void UpdateFlash()
    {
        if(_flashTimer <= 0f) return;

        // 완벽 회피 슬로모션 동안 느려지도록 적 전용 시간을 쓴다.
        _flashTimer -= EnemyTime.DeltaTime;

        if(_flashTimer <= 0f) _renderer.material.color = _originalColor;
    }

    private void Init()
    {
        _originalColor = _renderer.material.color;
    }
}

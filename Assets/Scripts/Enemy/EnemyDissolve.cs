using UnityEngine;

// 처치된 적의 시체를 녹여 없애고, 녹기 시작할 때 처치 이펙트를 띄운다.
// 시체 시간이 끝나는 순간 다 녹도록 그보다 디졸브 시간만큼 먼저 시작하고, 시체 시간과 같은 적 전용 시간으로 흐른다.
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(BodyRenderer))]
public class EnemyDissolve : MonoBehaviour
{
    private EnemyState _state;
    private EnemyData _data;
    private BodyRenderer _bodyRenderer;
    // 모두 적 전용 시간
    private float _timer;
    private float _delay;
    private float _duration;
    private bool _isDying;
    private bool _isDissolving;

    private void Awake() => CacheComponent();
    private void OnEnable() => BindDiedEvent();
    private void OnDisable() => UnbindDiedEvent();
    private void Update() => UpdateDissolve();

    // 디졸브 시간이 시체 시간보다 길면 처치되자마자 시작해 시체 시간 안에 끝낸다.
    private void OnDied()
    {
        if(_data.DissolveMaterial == null && _data.DeathEffectPrefab == null) return;

        _duration = Mathf.Min(_data.DissolveDuration, _data.CorpseDuration);

        if(_duration <= 0f) return;

        _delay = _data.CorpseDuration - _duration;
        _timer = 0f;
        _isDying = true;
        _isDissolving = false;
    }

    // 풀에서 다시 꺼낼 때 생성기를 거쳐 부른다. 녹는 중이었으면 멈추고, 처음 Material 로 바꾸고, 녹은 정도를 지운다.
    public void Restore()
    {
        _isDying = false;
        _isDissolving = false;

        _bodyRenderer.RestoreMaterials();
    }

    private void UpdateDissolve()
    {
        if(!_isDying) return;

        _timer += _state.Clock.DeltaTime;

        if(_timer < _delay) return;

        if(!_isDissolving) BeginDissolve();

        if(_data.DissolveMaterial != null) _bodyRenderer.SetDissolveAmount(Mathf.Clamp01((_timer - _delay) / _duration));
    }

    private void BeginDissolve()
    {
        _isDissolving = true;
        BattleEffect.Play(_data.DeathEffectPrefab, transform.position, transform.rotation);

        if(_data.DissolveMaterial != null) _bodyRenderer.BeginDissolve(_data.DissolveMaterial);
    }

    // 처치 알림은 EnemyState 가 가진다. EnemyController 의 Awake 보다 먼저 켜질 수 있어, 컨트롤러를 거치지 않고 직접 찾는다.
    private void CacheComponent()
    {
        _state = GetComponent<EnemyState>();
        _data = GetComponent<EnemyController>().Data;
        _bodyRenderer = GetComponent<BodyRenderer>();
    }

    private void BindDiedEvent()
    {
        _state.Died += OnDied;
    }

    private void UnbindDiedEvent()
    {
        _state.Died -= OnDied;
    }
}

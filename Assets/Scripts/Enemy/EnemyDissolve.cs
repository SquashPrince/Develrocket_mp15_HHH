using UnityEngine;

// 처치된 적의 시체를 녹여 없애고, 녹기 시작할 때 처치 이펙트를 띄운다.
// 시체 시간이 끝나는 순간 다 녹도록 그보다 디졸브 시간만큼 먼저 시작하고, 시체 시간과 같은 적 전용 시간으로 흐른다.
// 같은 Material 을 쓰는 다른 적에 영향이 없게 녹는 정도는 MaterialPropertyBlock 으로 넣는다.
// 처치된 적은 비활성화만 하고 다시 쓰지 않으므로, 바꾼 Material 은 되돌리지 않는다.
[RequireComponent(typeof(EnemyController))]
public class EnemyDissolve : MonoBehaviour
{
    private static readonly int DISSOLVE_AMOUNT_ID = Shader.PropertyToID("_DissolveAmount");

    [Tooltip("녹일 Renderer (몸 · 무기). 예고 표시처럼 몸이 아닌 Renderer 는 넣지 않는다")]
    [SerializeField] private Renderer[] _renderers;

    private MaterialPropertyBlock _propertyBlock;
    private EnemyState _state;
    private EnemyData _data;
    // 모두 적 전용 시간
    private float _timer;
    private float _delay;
    private float _duration;
    private bool _isDying;
    private bool _isDissolving;

    private void Awake() => CacheComponent();
    private void Start() => Init();
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

    private void UpdateDissolve()
    {
        if(!_isDying) return;

        _timer += _state.Clock.DeltaTime;

        if(_timer < _delay) return;

        if(!_isDissolving) BeginDissolve();

        if(_data.DissolveMaterial != null) SetDissolveAmount(Mathf.Clamp01((_timer - _delay) / _duration));
    }

    private void BeginDissolve()
    {
        _isDissolving = true;
        BattleEffect.Play(_data.DeathEffectPrefab, transform.position, transform.rotation);

        if(_data.DissolveMaterial == null) return;

        foreach(Renderer target in _renderers)
        {
            Material[] materials = new Material[target.sharedMaterials.Length];

            for(int i = 0; i < materials.Length; i++)
            {
                materials[i] = _data.DissolveMaterial;
            }

            target.sharedMaterials = materials;
        }
    }

    private void SetDissolveAmount(float amount)
    {
        foreach(Renderer target in _renderers)
        {
            target.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(DISSOLVE_AMOUNT_ID, amount);
            target.SetPropertyBlock(_propertyBlock);
        }
    }

    // 처치 알림은 EnemyState 가 가진다. EnemyController 의 Awake 보다 먼저 켜질 수 있어, 컨트롤러를 거치지 않고 직접 찾는다.
    private void CacheComponent()
    {
        _state = GetComponent<EnemyState>();
        _data = GetComponent<EnemyController>().Data;
    }

    private void Init()
    {
        _propertyBlock = new MaterialPropertyBlock();
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

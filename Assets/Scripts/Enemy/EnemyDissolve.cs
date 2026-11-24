using UnityEngine;

// 처치된 적의 시체를 녹여 없애고, 녹기 시작할 때 처치 이펙트를 띄운다. 적 데이터의 디졸브 Material · 디졸브 시간 · 처치 이펙트를 쓴다.
// 시체 시간이 끝나기 디졸브 시간만큼 전에, 연결한 Renderer 의 Material 을 모든 칸에서 디졸브 Material 로 바꾸고
// 녹는 정도(_DissolveAmount)를 0(멀쩡함) → 1(다 녹음)로 올린다. 다 녹는 순간 EnemyState 가 시체 시간이 끝나 적을 비활성화한다.
// 처치 이펙트는 적에 붙이지 않고 그 자리에 띄워, 적이 사라진 뒤에도 끝날 때까지 남는다.
// 디졸브 Material 이 없어도 처치 이펙트는 같은 때에 띄우고, 몸은 녹지 않고 그냥 사라진다.
// 시간 : 시체 시간과 같은 적 전용 시간으로 흘러, 슬로모션 · 필살기 연출 동안 함께 느려지거나 멈춘다.
// 녹는 정도는 MaterialPropertyBlock 으로 Renderer 마다 따로 넣는다. 같은 Material 을 쓰는 다른 적은 영향을 받지 않는다.
// 적은 웨이브마다 새로 만들고 처치되면 비활성화만 하므로, 바꾼 Material 은 되돌리지 않는다.
[RequireComponent(typeof(EnemyController))]
public class EnemyDissolve : MonoBehaviour
{
    private static readonly int DISSOLVE_AMOUNT_ID = Shader.PropertyToID("_DissolveAmount");

    [Tooltip("녹일 Renderer (몸 · 무기). 예고 표시처럼 몸이 아닌 Renderer 는 넣지 않는다")]
    [SerializeField] private Renderer[] _renderers;

    private MaterialPropertyBlock _propertyBlock;
    private EnemyState _state;
    private EnemyData _data;
    // 처치된 뒤 흐른 시간 · 녹기 시작할 때까지의 시간 · 녹는 데 걸리는 시간 (모두 적 전용 시간)
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

    // 디졸브 Material · 처치 이펙트가 모두 없으면 아무것도 하지 않는다. 디졸브 시간이 시체 시간보다 길면 처치되자마자 시작해 시체 시간 안에 끝낸다.
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

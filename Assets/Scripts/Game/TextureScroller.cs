using UnityEngine;

// 기본 텍스처(URP Lit 의 Base Map)를 시간에 따라 흐르게 한다. 전투 구역 경계 벽(innerWall)이 막혀 있다는 표시로 쓴다.
// 같은 Material 을 쓰는 다른 벽 · Material 에셋에 영향이 없도록 오프셋은 MaterialPropertyBlock 으로 넣는다.
// 일시정지 중에는 멈추도록 게임 시간으로 흐른다.
[RequireComponent(typeof(Renderer))]
public class TextureScroller : MonoBehaviour
{
    // URP Lit 기본 텍스처의 타일링(xy) · 오프셋(zw)
    private static readonly int BASE_MAP_ST_ID = Shader.PropertyToID("_BaseMap_ST");

    [Tooltip("초당 흐르는 양 (텍스처 한 장 = 1). x 는 가로, y 는 세로")]
    [SerializeField] private Vector2 _speed = new Vector2(0.1f, 0f);

    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;
    // Material 에 정해 둔 타일링 · 오프셋. 흐름은 여기에 더한다.
    private Vector4 _baseScaleOffset;
    private Vector2 _offset;

    private void Awake()
    {
        CacheComponent();
        Init();
    }

    private void Update() => Scroll();

    // 오프셋은 1 마다 같은 모양이라, 오래 켜 두어도 값이 커지지 않게 0 ~ 1 로 되돌린다.
    private void Scroll()
    {
        _offset.x = Mathf.Repeat(_offset.x + _speed.x * Time.deltaTime, 1f);
        _offset.y = Mathf.Repeat(_offset.y + _speed.y * Time.deltaTime, 1f);

        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetVector(BASE_MAP_ST_ID, new Vector4(_baseScaleOffset.x, _baseScaleOffset.y, _baseScaleOffset.z + _offset.x, _baseScaleOffset.w + _offset.y));
        _renderer.SetPropertyBlock(_propertyBlock);
    }

    private void CacheComponent()
    {
        _renderer = GetComponent<Renderer>();
    }

    private void Init()
    {
        _propertyBlock = new MaterialPropertyBlock();
        _baseScaleOffset = _renderer.sharedMaterial.GetVector(BASE_MAP_ST_ID);
    }
}

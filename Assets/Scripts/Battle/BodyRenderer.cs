using System.Collections.Generic;
using UnityEngine;

// 캐릭터 · 적이 카메라에 가까워지면 디더(픽셀을 바둑판처럼 빼고 그리기)로 사라지고, 멀어지면 다시 보이게 한다.
// 셰이더가 디더를 지원해야 한다. lilToon 은 Cutout + Use Dither 머티리얼의 색 알파를, 디더 Shader Graph 는 DitherFade 프로퍼티를 낮춘다.
// lilToon 외곽선은 메인 색이 아닌 외곽선 색의 알파를 쓰므로 함께 낮춘다.
// 머티리얼 에셋 · 같은 머티리얼을 쓰는 다른 대상에 영향이 없도록 Renderer 마다 MaterialPropertyBlock 으로 넣는다.
// 다 보일 때는 값을 지워 평소 렌더링 비용이 늘지 않게 한다. 카메라 이동이 끝난 뒤 맞추도록 LateUpdate 에서 돈다.
public class BodyRenderer : MonoBehaviour
{
    private static readonly int COLOR_ID = Shader.PropertyToID("_Color");
    private static readonly int OUTLINE_COLOR_ID = Shader.PropertyToID("_OutlineColor");
    private static readonly int DITHER_FADE_ID = Shader.PropertyToID("_DitherFade");
    private static readonly int DISSOLVE_AMOUNT_ID = Shader.PropertyToID("_DissolveAmount");
    // 이보다 덜 바뀌면 다시 넣지 않는다.
    private const float VISIBILITY_EPSILON = 0.001f;

    // 머티리얼 하나에 넣을 값. 셰이더마다 넣는 프로퍼티가 달라 미리 정해 둔다.
    private struct FadeSlot
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public bool UsesDitherFade;
        public bool HasOutline;
        public Color BaseColor;
        public Color BaseOutlineColor;
    }

    [Tooltip("몸과 무기의 Renderer. HUD와 공격 예고는 제외한다")]
    [SerializeField] private Renderer[] _renderers;
    [Tooltip("카메라가 이 거리(m)보다 가까워지면 흐려지기 시작한다")]
    [SerializeField, Min(0f)] private float _fadeStartDistance = 2f;
    [Tooltip("카메라가 이 거리(m) 안이면 완전히 사라진다. 시작 거리보다 작게 둔다")]
    [SerializeField, Min(0f)] private float _fadeEndDistance = 0.8f;
    [Tooltip("거리를 잴 몸 중심의 높이(m, 발밑 기준)")]
    [SerializeField, Min(0f)] private float _centerHeight = 1f;

    private readonly List<FadeSlot> _slots = new List<FadeSlot>();
    private MaterialPropertyBlock _propertyBlock;
    private Material[][] _originalMaterials;
    private Camera _camera;
    // 1 이면 다 보임, 0 이면 다 사라짐
    private float _appliedVisibility = 1f;
    private float _swapOutVisibility = 1f;
    private bool _isDissolving;

    public IReadOnlyList<Renderer> Renderers => _renderers;

    private void Awake()
    {
        CacheComponent();
        Init();
    }

    private void OnDisable() => ResetVisibility();
    private void LateUpdate() => UpdateVisibility();

    public void SetSwapOutVisibility(float visibility) => _swapOutVisibility = Mathf.Clamp01(visibility);

    public void BeginDissolve(Material material)
    {
        ResetVisibility();
        _isDissolving = true;

        for(int i = 0; i < _renderers.Length; i++)
        {
            Material[] materials = new Material[_originalMaterials[i].Length];

            for(int slot = 0; slot < materials.Length; slot++)
            {
                materials[slot] = material;
            }

            _renderers[i].sharedMaterials = materials;
        }
    }

    public void SetDissolveAmount(float amount)
    {
        foreach(Renderer target in _renderers)
        {
            target.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(DISSOLVE_AMOUNT_ID, amount);
            target.SetPropertyBlock(_propertyBlock);
        }
    }

    public void RestoreMaterials()
    {
        if(!_isDissolving) return;

        for(int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].sharedMaterials = _originalMaterials[i];
            _renderers[i].SetPropertyBlock(null);
        }

        _isDissolving = false;
    }

    private void UpdateVisibility()
    {
        if(_isDissolving) return;

        ApplyVisibility(Mathf.Min(GetCameraVisibility(), _swapOutVisibility));
    }

    private void ResetVisibility()
    {
        _swapOutVisibility = 1f;
        ApplyVisibility(1f);
    }

    // 시작 거리에서 1, 끝 거리에서 0 이 되게 선형으로 줄인다.
    private float GetCameraVisibility()
    {
        if(_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
        if(_camera == null) return 1f;

        Vector3 center = transform.position + Vector3.up * _centerHeight;
        float distance = Vector3.Distance(_camera.transform.position, center);

        if(_fadeStartDistance <= _fadeEndDistance) return distance <= _fadeEndDistance ? 0f : 1f;

        return Mathf.InverseLerp(_fadeEndDistance, _fadeStartDistance, distance);
    }

    private void ApplyVisibility(float visibility)
    {
        if(visibility == _appliedVisibility) return;
        if(visibility > 0f && visibility < 1f && Mathf.Abs(visibility - _appliedVisibility) < VISIBILITY_EPSILON) return;

        _appliedVisibility = visibility;

        foreach(FadeSlot slot in _slots)
        {
            if(visibility >= 1f)
            {
                slot.Renderer.SetPropertyBlock(null, slot.MaterialIndex);
                continue;
            }

            slot.Renderer.GetPropertyBlock(_propertyBlock, slot.MaterialIndex);

            if(slot.UsesDitherFade) _propertyBlock.SetFloat(DITHER_FADE_ID, visibility);
            else
            {
                _propertyBlock.SetColor(COLOR_ID, FadeAlpha(slot.BaseColor, visibility));

                if(slot.HasOutline) _propertyBlock.SetColor(OUTLINE_COLOR_ID, FadeAlpha(slot.BaseOutlineColor, visibility));
            }

            slot.Renderer.SetPropertyBlock(_propertyBlock, slot.MaterialIndex);
        }
    }

    private static Color FadeAlpha(Color color, float visibility) => new Color(color.r, color.g, color.b, color.a * visibility);

    // 디더를 지원하지 않는 머티리얼(프로퍼티가 없음)은 건너뛴다.
    private void AddSlots(Renderer target, Material[] materials)
    {
        for(int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];

            if(material == null) continue;

            bool usesDitherFade = material.HasProperty(DITHER_FADE_ID);
            bool hasOutline = !usesDitherFade && material.HasProperty(OUTLINE_COLOR_ID);

            if(!usesDitherFade && !material.HasProperty(COLOR_ID)) continue;

            _slots.Add(new FadeSlot
            {
                Renderer = target,
                MaterialIndex = i,
                UsesDitherFade = usesDitherFade,
                HasOutline = hasOutline,
                BaseColor = usesDitherFade ? Color.white : material.GetColor(COLOR_ID),
                BaseOutlineColor = hasOutline ? material.GetColor(OUTLINE_COLOR_ID) : Color.white
            });
        }
    }

    private void CacheComponent()
    {
        _camera = Camera.main;
    }

    private void Init()
    {
        _propertyBlock = new MaterialPropertyBlock();
        _originalMaterials = new Material[_renderers.Length][];

        for(int i = 0; i < _renderers.Length; i++)
        {
            Renderer target = _renderers[i];
            _originalMaterials[i] = target.sharedMaterials;
            AddSlots(target, _originalMaterials[i]);
        }
    }
}

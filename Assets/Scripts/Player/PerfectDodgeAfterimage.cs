using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 완벽 회피 연속 잔상. 조작 중인 캐릭터가 완벽 회피하면 일정 시간 동안 간격마다 그 순간의 모습을 남기고 흐려지게 한다.
// 잔상마다 GameObject 를 만들지 않고 구운 Mesh 를 매 프레임 직접 그리며(Graphics.RenderMesh), 다 사라진 잔상은 재사용한다.
// 슬로모션 · 히트스톱과 상관없이 실제 시간으로 흐른다.
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(BodyRenderer))]
public class PerfectDodgeAfterimage : MonoBehaviour
{
    // Renderer 목록과 같은 순서의 칸. 스킨 메시 칸은 이 잔상이 구운 Mesh, 나머지 칸은 원본 Mesh 를 가리킨다.
    private class Afterimage
    {
        public Mesh[] Meshes;
        public Matrix4x4[] Matrices;
        public bool[] IsVisible;
        public MaterialPropertyBlock PropertyBlock;
        public float Elapsed;
    }

    private readonly List<Afterimage> _active = new List<Afterimage>();
    private readonly List<Afterimage> _free = new List<Afterimage>();
    private IReadOnlyList<Renderer> _renderers;
    private MeshFilter[] _meshFilters;
    private PlayerController _controller;
    private float _emitTimer;
    private float _intervalTimer;

    private CommonCharacterSettings Common => _controller.Common;

    private void Awake() => CacheComponent();
    private void OnEnable() => BindFeedbackEvent();
    private void OnDisable()
    {
        UnbindFeedbackEvent();
        ClearAfterimages();
    }
    private void OnDestroy() => DestroyBakedMeshes();
    // 애니메이션이 이번 프레임 자세를 정한 뒤에 굽도록 LateUpdate 에서 처리한다.
    private void LateUpdate()
    {
        UpdateEmit();
        UpdateAfterimages();
    }

    private void OnMoment(BattleFeedback.Moment moment, Vector3 direction)
    {
        if(moment != BattleFeedback.Moment.PerfectDodge) return;
        if(_controller.Party.Controlled != _controller || Common.AfterimageMaterial == null) return;

        _emitTimer = Common.AfterimageEmitTime;
        _intervalTimer = 0f;
    }

    // 완벽 회피 순간에 첫 잔상을 바로 남긴다.
    private void UpdateEmit()
    {
        if(_emitTimer <= 0f) return;

        _intervalTimer -= Time.unscaledDeltaTime;

        if(_intervalTimer <= 0f)
        {
            Capture();
            _intervalTimer += Common.AfterimageInterval;
        }

        _emitTimer -= Time.unscaledDeltaTime;
    }

    private void UpdateAfterimages()
    {
        if(_active.Count == 0) return;

        Material material = Common.AfterimageMaterial;
        int propertyId = Shader.PropertyToID(Common.AfterimageFadeProperty);

        for(int i = _active.Count - 1; i >= 0; i--)
        {
            Afterimage afterimage = _active[i];

            afterimage.Elapsed += Time.unscaledDeltaTime;

            if(afterimage.Elapsed >= Common.AfterimageFadeTime || material == null)
            {
                _active.RemoveAt(i);
                _free.Add(afterimage);
                continue;
            }

            SetFadeValue(afterimage, material, propertyId);
            Draw(afterimage, material);
        }
    }

    // Material 의 색은 그대로 두고 알파만 잔상마다 바꾼다.
    private void SetFadeValue(Afterimage afterimage, Material material, int propertyId)
    {
        Color color = material.GetColor(propertyId);

        color.a = Mathf.Lerp(Common.AfterimageStartValue, Common.AfterimageEndValue, afterimage.Elapsed / Common.AfterimageFadeTime);
        afterimage.PropertyBlock.SetColor(propertyId, color);
    }

    private void Draw(Afterimage afterimage, Material material)
    {
        RenderParams renderParams = new RenderParams(material)
        {
            layer = gameObject.layer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            matProps = afterimage.PropertyBlock
        };

        for(int i = 0; i < afterimage.Meshes.Length; i++)
        {
            if(!afterimage.IsVisible[i]) continue;

            Mesh mesh = afterimage.Meshes[i];

            for(int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                Graphics.RenderMesh(renderParams, mesh, subMesh, afterimage.Matrices[i]);
            }
        }
    }

    // 스킨 메시는 크기를 넣어 굽기 때문에, 그릴 때는 위치 · 회전만 쓴다.
    private void Capture()
    {
        Afterimage afterimage = GetAfterimage();

        for(int i = 0; i < _renderers.Count; i++)
        {
            Renderer source = _renderers[i];

            afterimage.IsVisible[i] = source.enabled && source.gameObject.activeInHierarchy;

            if(!afterimage.IsVisible[i]) continue;

            if(source is SkinnedMeshRenderer skinned)
            {
                if(afterimage.Meshes[i] == null) afterimage.Meshes[i] = new Mesh();

                skinned.BakeMesh(afterimage.Meshes[i], true);
                afterimage.Matrices[i] = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
            }
            else
            {
                afterimage.Meshes[i] = _meshFilters[i].sharedMesh;
                afterimage.Matrices[i] = source.transform.localToWorldMatrix;
            }
        }

        afterimage.Elapsed = 0f;
        _active.Add(afterimage);
    }

    private Afterimage GetAfterimage()
    {
        if(_free.Count > 0)
        {
            Afterimage reused = _free[_free.Count - 1];

            _free.RemoveAt(_free.Count - 1);
            return reused;
        }

        return new Afterimage
        {
            Meshes = new Mesh[_renderers.Count],
            Matrices = new Matrix4x4[_renderers.Count],
            IsVisible = new bool[_renderers.Count],
            PropertyBlock = new MaterialPropertyBlock()
        };
    }

    private void ClearAfterimages()
    {
        _emitTimer = 0f;
        _free.AddRange(_active);
        _active.Clear();
    }

    // 스킨 메시 칸의 Mesh 만 이 컴포넌트가 만든 것이라 지운다. 나머지 칸은 원본 Mesh 다.
    private void DestroyBakedMeshes()
    {
        _free.AddRange(_active);
        _active.Clear();

        foreach(Afterimage afterimage in _free)
        {
            for(int i = 0; i < _renderers.Count; i++)
            {
                if(_renderers[i] is SkinnedMeshRenderer && afterimage.Meshes[i] != null) Destroy(afterimage.Meshes[i]);
            }
        }

        _free.Clear();
    }

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _renderers = GetComponent<BodyRenderer>().Renderers;
        _meshFilters = new MeshFilter[_renderers.Count];

        for(int i = 0; i < _renderers.Count; i++)
        {
            if(!(_renderers[i] is SkinnedMeshRenderer)) _meshFilters[i] = _renderers[i].GetComponent<MeshFilter>();
        }
    }

    private void BindFeedbackEvent()
    {
        BattleFeedback.Occurred += OnMoment;
    }

    private void UnbindFeedbackEvent()
    {
        BattleFeedback.Occurred -= OnMoment;
    }
}

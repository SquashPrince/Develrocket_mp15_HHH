using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 완벽 회피 연속 잔상. 캐릭터 루트(PlayerController 와 같은 오브젝트)에 붙이고, 잔상으로 남길 Renderer(몸 · 무기)를 연결한다.
// 완벽 회피 알림을 받으면 조작 중인 캐릭터만, 공통 설정의 남기는 시간 동안 간격마다 그 순간의 모습을 잔상으로 남긴다.
// 잔상 하나 : 스킨 메시는 그 순간의 자세를 Mesh 로 구워(Bake) 두고, 무기처럼 스킨이 아닌 Renderer 는 Mesh 와 그 순간의 위치만 기억한다.
// 그리기 : 잔상마다 GameObject 를 만들지 않고 매 프레임 직접 그린다(Graphics.RenderMesh). 그림자는 그리지 않는다.
//   잔상마다 흐려짐 값을 따로 넣어야 해서 MaterialPropertyBlock 을 잔상마다 하나씩 둔다.
// 흐려짐 : 공통 설정의 색 프로퍼티 알파를 시작 알파에서 끝 알파로, 사라지는 시간에 걸쳐 바꾼다. Material 의 색은 그대로 두고 알파만 바꾼다.
// 재사용 : 다 사라진 잔상(구운 Mesh 포함)은 다음 잔상에 다시 쓰고, 이 컴포넌트가 파괴될 때 구운 Mesh 를 지운다.
// 시간은 모두 실제 시간이라 슬로모션 · 히트스톱과 상관없이 일정하게 흐른다.
// 캐릭터가 꺼지면(교체 퇴장 등) 남은 잔상을 바로 지운다.
[RequireComponent(typeof(PlayerController))]
public class PerfectDodgeAfterimage : MonoBehaviour
{
    // 잔상 하나. Renderer 목록과 같은 순서로 그릴 Mesh 와 위치, 그 칸을 그릴지를 가진다.
    // 스킨 메시 칸의 Mesh 는 이 잔상이 만든 것이라 다시 구워 쓰고, 나머지 칸은 원본 Mesh 를 그대로 가리킨다.
    private class Afterimage
    {
        public Mesh[] Meshes;
        public Matrix4x4[] Matrices;
        public bool[] IsVisible;
        public MaterialPropertyBlock PropertyBlock;
        public float Elapsed;
    }

    [Tooltip("잔상으로 남길 Renderer (몸 스킨 메시 · 무기)")]
    [SerializeField] private Renderer[] _renderers;

    private readonly List<Afterimage> _active = new List<Afterimage>();
    private readonly List<Afterimage> _free = new List<Afterimage>();
    private MeshFilter[] _meshFilters;
    private PlayerController _controller;
    // 잔상을 더 남길 남은 시간 · 다음 잔상까지 남은 시간
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

    // 남기는 시간 동안 간격마다 잔상을 하나씩 남긴다. 완벽 회피 순간에 첫 잔상을 바로 남긴다.
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

    // 잔상마다 흐려짐 값을 넣어 그리고, 다 사라진 잔상은 재사용 목록으로 돌린다.
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

    // 지금 모습을 잔상 하나로 남긴다. 꺼져 있는 Renderer(숨긴 무기 등)는 남기지 않는다.
    // 스킨 메시는 크기를 넣어 굽기 때문에, 그릴 때는 위치 · 회전만 쓴다.
    private void Capture()
    {
        Afterimage afterimage = GetAfterimage();

        for(int i = 0; i < _renderers.Length; i++)
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

    // 다 사라진 잔상을 꺼내고, 없으면 하나 더 만든다.
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
            Meshes = new Mesh[_renderers.Length],
            Matrices = new Matrix4x4[_renderers.Length],
            IsVisible = new bool[_renderers.Length],
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
            for(int i = 0; i < _renderers.Length; i++)
            {
                if(_renderers[i] is SkinnedMeshRenderer && afterimage.Meshes[i] != null) Destroy(afterimage.Meshes[i]);
            }
        }

        _free.Clear();
    }

    // 스킨이 아닌 Renderer 는 그릴 Mesh 를 MeshFilter 에서 읽는다. 스킨 메시 칸은 비워 둔다.
    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _meshFilters = new MeshFilter[_renderers.Length];

        for(int i = 0; i < _renderers.Length; i++)
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

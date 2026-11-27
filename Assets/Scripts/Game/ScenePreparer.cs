using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// 게임 씬의 로딩 준비. 로딩 화면이 덮고 있는 동안 전투 중 처음 나올 때 끊기는 것들(셰이더 · 대미지 숫자 · 이펙트 · 웨이브 적)을 미리 준비한다.
// 자세한 흐름 : Docs/Technical/Game/SceneLoading.md
public class ScenePreparer : MonoBehaviour
{
    [Tooltip("셰이더 예열 기록 파일. 비워 두면 예열하지 않는다")]
    [SerializeField] private GraphicsStateCollection _graphicsStates;
    [SerializeField] private BattleEffectPool _effectPool;
    [SerializeField] private DamageTextSpawner _damageTextSpawner;
    [SerializeField] private WaveSpawner _waveSpawner;
    [Tooltip("미리 그릴 때 이펙트 · 적을 놓는 카메라 앞 거리(m)")]
    [SerializeField, Min(0.5f)] private float _warmDistance = 5f;
    [Tooltip("이펙트 · 적을 켜 둔 채 그리는 프레임 수")]
    [SerializeField, Min(1)] private int _warmFrames = 3;

    private void Awake() => Init();

    private async UniTask PrepareAsync(CancellationToken token)
    {
        Transform view = Camera.main.transform;
        Vector3 warmPosition = view.position + view.forward * _warmDistance;
        Vector3 toCamera = HorizontalVector.Between(warmPosition, view.position);
        Quaternion faceCamera = toCamera == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(toCamera);

        await WarmUpShadersAsync(token);
        _damageTextSpawner.Prewarm();
        _effectPool.Prewarm(warmPosition);
        _waveSpawner.Prewarm(warmPosition, faceCamera);
        await UniTask.DelayFrame(_warmFrames, cancellationToken: token);
        await WaitEditorShaderCompileAsync(token);
        _effectPool.RecallAll();
        _waveSpawner.RecallPrewarmed();
    }

    // 에디터는 셰이더를 비동기로 컴파일하며, 끝나기 전까지 하늘색 임시 셰이더로 그린다. 미리 그린 것들의 컴파일이 끝날 때까지 로딩 화면을 유지한다.
    // 빌드는 그리는 순간 바로 컴파일하므로 기다리지 않는다.
    private static async UniTask WaitEditorShaderCompileAsync(CancellationToken token)
    {
#if UNITY_EDITOR
        await UniTask.WaitUntil(() => !UnityEditor.ShaderUtil.anythingCompiling, cancellationToken: token);
#else
        await UniTask.CompletedTask;
#endif
    }

    // 기록 파일의 셰이더 · 그리기 상태를 이 PC 에서 미리 컴파일한다. 다른 그래픽 API 로 기록한 파일은 쓸 수 없어 건너뛴다.
    // 이미 드라이버 캐시에 있는 것은 금방 끝난다.
    private async UniTask WarmUpShadersAsync(CancellationToken token)
    {
        if(_graphicsStates == null) return;

        if(_graphicsStates.graphicsDeviceType != SystemInfo.graphicsDeviceType)
        {
            Debug.LogWarning($"[셰이더 예열] 기록 파일의 그래픽 API({_graphicsStates.graphicsDeviceType})가 지금({SystemInfo.graphicsDeviceType})과 달라 건너뜁니다.", this);
            return;
        }

        JobHandle warmUp = _graphicsStates.WarmUp(default);

        await UniTask.WaitUntil(() => warmUp.IsCompleted, cancellationToken: token);
        warmUp.Complete();
    }

    // 공용 UI 는 먼저 실행되므로 Awake 에서 씬 이동에 준비를 맡길 수 있다.
    private void Init()
    {
        CommonUI.Instance.SceneLoader.AddPreparation(PrepareAsync);
    }
}

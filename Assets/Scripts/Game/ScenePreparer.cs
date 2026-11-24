using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// 게임 씬의 로딩 준비. 로딩 화면이 덮고 있는 동안 전투 중 처음 나올 때 끊기는 것들(셰이더 · 대미지 숫자 · 이펙트)을 미리 준비한다.
// 자세한 흐름 : Docs/Technical/Game/SceneLoading.md
public class ScenePreparer : MonoBehaviour
{
    [Tooltip("셰이더 예열 기록 파일. 비워 두면 예열하지 않는다")]
    [SerializeField] private GraphicsStateCollection _graphicsStates;
    [SerializeField] private BattleEffectPool _effectPool;
    [SerializeField] private DamageTextSpawner _damageTextSpawner;
    [Tooltip("미리 그릴 때 이펙트를 놓는 카메라 앞 거리(m)")]
    [SerializeField, Min(0.5f)] private float _warmDistance = 5f;
    [Tooltip("이펙트를 켜 둔 채 그리는 프레임 수")]
    [SerializeField, Min(1)] private int _warmFrames = 3;

    private void Awake() => Init();

    private async UniTask PrepareAsync(CancellationToken token)
    {
        Transform view = Camera.main.transform;

        await WarmUpShadersAsync(token);
        _damageTextSpawner.Prewarm();
        _effectPool.Prewarm(view.position + view.forward * _warmDistance);
        await UniTask.DelayFrame(_warmFrames, cancellationToken: token);
        _effectPool.RecallAll();
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

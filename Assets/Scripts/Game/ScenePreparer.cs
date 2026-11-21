using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// 게임 씬의 로딩 준비. 로딩 화면이 덮고 있는 동안, 전투 중 처음 나올 때 끊기는 것들을 미리 만들고 한 번 그려 둔다.
// 셰이더 : 기록 파일(GraphicsStateTracer 로 기록)의 셰이더 · 그리기 상태를 미리 컴파일한다. 적 · 캐릭터처럼 미리 만들기 어려운 것도 여기서 준비된다.
// 대미지 숫자 : 정한 수만큼 미리 만든다.
// 이펙트 : 미리 만들 목록의 이펙트를 카메라 앞에 모두 켜서 몇 프레임 그린 뒤 거둔다. 처음 그릴 때의 셰이더 · 파티클 준비가 여기서 끝난다.
// 씬 이동이 이 준비를 기다린 뒤 로딩 화면을 닫는다. 에디터에서 게임 씬을 바로 플레이하면 덮는 화면 없이 바로 준비한다.
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

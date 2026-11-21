using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 씬 이동. 공용 UI 에 하나만 두고, 씬을 바꾸는 곳(타이틀 시작 · 재시작 · 타이틀로)은 모두 이것을 통한다.
// 흐름 : 화면을 검게 덮음 → [로딩 화면 표시] → 새 씬을 비동기로 불러옴 → 새 씬이 맡긴 준비를 기다림 → 메모리 정리 → [최소 표시 시간 대기 · 로딩 화면 닫음] → 화면을 걷음 → 씬 표시 알림
//   로딩 화면을 열고 닫을 때도 검은 화면을 한 번 거쳐, 화면이 갑자기 바뀌지 않게 한다.
// 배경음 : 다른 씬으로 갈 때는 덮는 동안 배경음을 끄고, 로딩 화면이 열리면 로딩 곡을 튼다. 새 씬의 곡은 화면이 다 보인 뒤에 나오게 한다.
//   같은 씬을 다시 불러올 때(재시작 · 재도전)는 곡을 끊지 않는다. 새 씬도 같은 곡을 요청하므로 이어서 나온다.
// 씬 표시 알림 : 배경음처럼 "화면이 다 보인 뒤" 할 일은 WhenShown 으로 맡긴다. 이동 중이 아니면(처음 연 씬 등) 바로 실행한다.
// 이동 중 새 요청은 무시한다. 버튼을 여러 번 눌러도 한 번만 이동하게 하기 위해서다.
// 실제 시간으로 진행한다. 히트스톱 · 일시정지로 게임 시간이 멈춰 있어도 이동은 진행된다.
public class SceneLoader : MonoBehaviour
{
    // 진행 막대에서 씬 불러오기가 차지하는 비율. 나머지는 새 씬의 준비가 채운다.
    private const float LOAD_PROGRESS_RATE = 0.8f;

    [Header("페이드")]
    [Tooltip("화면 전체를 덮는 검은 Image 의 CanvasGroup. 로딩 화면보다 앞(아래쪽 형제)에 둔다. 평소에는 꺼 두고 이동하는 동안만 켠다. 켜져 있는 동안 뒤의 버튼이 눌리지 않게 Image 의 Raycast Target 을 켠다")]
    [SerializeField] private CanvasGroup _fade;
    [Tooltip("검게 덮거나 걷는 데 걸리는 시간(초, 실제 시간)")]
    [SerializeField, Min(0f)] private float _fadeTime = 0.4f;

    [Header("로딩 화면")]
    [Tooltip("로딩 화면 표시 묶음. 평소에는 꺼 둔다")]
    [SerializeField] private GameObject _loadingRoot;
    [Tooltip("로딩 화면에서 달리는 마스코트들. 열 때마다 하나만 무작위로 켠다")]
    [SerializeField] private GameObject[] _mascots;
    [Tooltip("진행 막대. Image Type 을 Filled 로 둔다")]
    [SerializeField] private Image _progressFill;
    [Tooltip("로딩 화면을 최소한 보여 줄 시간(초, 실제 시간). 너무 빨리 닫혀 깜빡이는 것처럼 보이지 않게 한다")]
    [SerializeField, Min(0f)] private float _minLoadingTime = 1f;

    // 화면이 다 보인 뒤 실행할 일. 실행하면 비운다.
    private readonly List<Action> _shownActions = new List<Action>();
    // 새 씬이 맡긴 준비(이펙트 미리 만들기 등). 로딩 화면을 닫기 전에 모두 기다리고 비운다.
    private readonly List<Func<CancellationToken, UniTask>> _preparations = new List<Func<CancellationToken, UniTask>>();

    public bool IsLoading { get; private set; }

    private void Awake() => Init();

    // 씬을 불러온다. showLoading 이 꺼져 있으면 로딩 화면 없이 검은 화면만 거친다.
    public void Load(string sceneName, bool showLoading)
    {
        if(IsLoading) return;

        LoadAsync(sceneName, showLoading, destroyCancellationToken).Forget();
    }

    // 화면이 다 보인 뒤 한 번 실행한다. 이동 중이 아니면 바로 실행한다.
    public void WhenShown(Action action)
    {
        if(IsLoading) _shownActions.Add(action);
        else action();
    }

    // 새 씬이 로딩 화면 뒤에서 할 준비를 맡긴다. 이동 중이 아니면(처음 연 씬 등) 바로 시작한다.
    public void AddPreparation(Func<CancellationToken, UniTask> preparation)
    {
        if(IsLoading) _preparations.Add(preparation);
        else preparation(destroyCancellationToken).Forget();
    }

    private async UniTaskVoid LoadAsync(string sceneName, bool showLoading, CancellationToken token)
    {
        bool isSceneChange = sceneName != SceneManager.GetActiveScene().name;

        IsLoading = true;
        _fade.alpha = 0f;
        _fade.gameObject.SetActive(true);

        if(isSceneChange) BgmRequest.Play(BgmTrack.None);

        await FadeAsync(1f, token);

        float shownTime = Time.unscaledTime;

        if(showLoading)
        {
            if(isSceneChange) BgmRequest.Play(BgmTrack.Loading);

            ShowLoading();
            await FadeAsync(0f, token);
        }

        await LoadSceneAsync(sceneName, token);
        await RunPreparationsAsync(token);
        await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: token);
        GC.Collect();

        if(showLoading)
        {
            float remain = _minLoadingTime - (Time.unscaledTime - shownTime);

            if(remain > 0f) await UniTask.Delay(TimeSpan.FromSeconds(remain), DelayType.UnscaledDeltaTime, cancellationToken: token);

            await FadeAsync(1f, token);
            _loadingRoot.SetActive(false);
        }

        await FadeAsync(0f, token);

        _fade.gameObject.SetActive(false);
        IsLoading = false;
        RunShownActions();
    }

    private async UniTask LoadSceneAsync(string sceneName, CancellationToken token)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        while(!operation.isDone)
        {
            // 불러오기는 0.9 까지 오른다. 진행 막대는 불러오기로 80% 까지 채우고, 나머지는 준비가 끝나면 채운다.
            SetProgress(operation.progress / 0.9f * LOAD_PROGRESS_RATE);
            await UniTask.Yield(token);
        }

        SetProgress(LOAD_PROGRESS_RATE);
    }

    private async UniTask RunPreparationsAsync(CancellationToken token)
    {
        Func<CancellationToken, UniTask>[] preparations = _preparations.ToArray();

        _preparations.Clear();

        foreach(Func<CancellationToken, UniTask> preparation in preparations)
        {
            await preparation(token);
        }

        SetProgress(1f);
    }

    private async UniTask FadeAsync(float target, CancellationToken token)
    {
        float start = _fade.alpha;
        float timer = 0f;

        while(timer < _fadeTime)
        {
            timer += Time.unscaledDeltaTime;
            _fade.alpha = Mathf.Lerp(start, target, timer / _fadeTime);
            await UniTask.Yield(token);
        }

        _fade.alpha = target;
    }

    private void ShowLoading()
    {
        int pick = UnityEngine.Random.Range(0, _mascots.Length);

        for(int i = 0; i < _mascots.Length; i++)
        {
            _mascots[i].SetActive(i == pick);
        }

        SetProgress(0f);
        _loadingRoot.SetActive(true);
    }

    private void SetProgress(float rate)
    {
        _progressFill.fillAmount = Mathf.Clamp01(rate);
    }

    // 실행 중 새로 맡겨지는 일이 있어도 목록이 꼬이지 않게, 복사본을 돌린다.
    private void RunShownActions()
    {
        Action[] actions = _shownActions.ToArray();

        _shownActions.Clear();

        foreach(Action action in actions)
        {
            action();
        }
    }

    private void Init()
    {
        _fade.gameObject.SetActive(false);
        _loadingRoot.SetActive(false);
    }
}

using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// 셰이더 예열용 기록. 플레이하는 동안 실제로 그린 셰이더 · 그리기 상태를 모아 파일로 남긴다. 개발 중 기록할 때만 켜는 도구다.
// 공용 UI 에 붙여 두고, 기록할 때만 컴포넌트를 켠다. 씬이 바뀌어도 이어서 기록하고, 게임을 끌 때 저장한다.
// 기록 방법 : 모든 캐릭터 · 스킬 · 필살기 · 적 · 보스가 한 번씩 화면에 나오도록 한 판을 플레이한다. 빠진 것은 예열되지 않는다.
// 저장 : 에디터에서는 저장 경로에 바로 쓴다. 빌드(개발 빌드)에서는 에디터로 보내, 에디터가 같은 이름으로 저장한다.
// 다 기록했으면 반드시 컴포넌트를 끈다. 켠 채 빌드하면 플레이어 PC 에서도 기록이 돈다.
public class GraphicsStateTracer : MonoBehaviour
{
    [Tooltip("저장할 파일 경로(프로젝트 기준). 확장자는 .graphicsstate")]
    [SerializeField] private string _savePath = "Assets/Settings/GraphicsStates/GameGraphicsStates.graphicsstate";

    private GraphicsStateCollection _collection;

    private void Start() => BeginTrace();
    private void OnApplicationQuit() => EndTrace();

    private void BeginTrace()
    {
        _collection = new GraphicsStateCollection();

        if(!_collection.BeginTrace()) Debug.LogWarning("[셰이더 기록] 기록을 시작하지 못했습니다.", this);
    }

    private void EndTrace()
    {
        if(_collection == null || !_collection.isTracing) return;

        _collection.EndTrace();
        Save();
    }

    private void Save()
    {
#if UNITY_EDITOR
        Directory.CreateDirectory(Path.GetDirectoryName(_savePath));
        bool isSaved = _collection.SaveToFile(_savePath);
#else
        bool isSaved = _collection.SendToEditor(Path.GetFileName(_savePath));
#endif

        if(isSaved) Debug.Log($"[셰이더 기록] 셰이더 {_collection.variantCount}개 · 그리기 상태 {_collection.totalGraphicsStateCount}개를 저장했습니다 : {_savePath}");
        else Debug.LogWarning("[셰이더 기록] 저장하지 못했습니다.", this);
    }
}

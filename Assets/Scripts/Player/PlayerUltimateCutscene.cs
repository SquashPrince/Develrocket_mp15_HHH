using UnityEngine;
using UnityEngine.Playables;

// 연출 무대의 재생과 화면 전환을 맡는다.
public class PlayerUltimateCutscene : MonoBehaviour
{
    [SerializeField] private PlayableDirector _director;
    [SerializeField] private Camera _stageCamera;

    private Camera _battleCamera;
    private Canvas _battleHud;
    private int _battleMask;
    private bool _wasHudEnabled;

    public void Begin(Camera battleCamera)
    {
        HideBattleHud();
        _battleCamera = battleCamera;
        _battleMask = battleCamera.cullingMask;
        battleCamera.cullingMask &= ~_stageCamera.cullingMask;
        _stageCamera.depth = battleCamera.depth + 1f;
        _stageCamera.enabled = true;

        _director.timeUpdateMode = DirectorUpdateMode.Manual;
        _director.extrapolationMode = DirectorWrapMode.Hold;
        _director.time = 0d;
        _director.Play();
        _director.Evaluate();
    }

    public bool Advance(float deltaTime)
    {
        double duration = _director.duration;
        _director.time = System.Math.Min(_director.time + deltaTime, duration);
        _director.Evaluate();
        return _director.time >= duration;
    }

    public void End()
    {
        _stageCamera.enabled = false;
        _director.Stop();
        if(_battleCamera != null) _battleCamera.cullingMask = _battleMask;
        _battleCamera = null;
        if(_battleHud != null) _battleHud.enabled = _wasHudEnabled;
        _battleHud = null;
        BattleHudVisibility.SetCutsceneActive(false);
    }

    private void HideBattleHud()
    {
        // 콤보 선택 화면 컴포넌트는 전투 HUD Canvas 루트에 있다.
        ComboSelectionView view = FindAnyObjectByType<ComboSelectionView>();
        if(view != null && view.TryGetComponent(out _battleHud))
        {
            _wasHudEnabled = _battleHud.enabled;
            _battleHud.enabled = false;
        }

        BattleHudVisibility.SetCutsceneActive(true);
    }
}

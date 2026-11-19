using System;
using Unity.Cinemachine;
using UnityEngine;

// 전투 구도 카메라를 켜고 끈다. 조작 중인 캐릭터가 지금 쓰는 액션의 구도를 매 프레임 읽어, 그 구도의 카메라만 켠다.
// 패링은 패링 액션(준비 · 반응)과 추격 액션에 같은 구도를 고르면 패링 시작부터 추격이 끝날 때까지 이어진다.
//   반응에서 추격으로 넘어가는 것은 같은 프레임 안이라 그 사이에 구도가 끊기지 않는다.
// 구도가 없는 액션이나 액션을 쓰지 않는 상태(이동 · 피격 등)가 되면 구도 카메라를 끄고 조작 카메라(일반 · 록온)로 돌아간다.
// 구도 카메라는 조작 카메라보다 Priority 를 높게 두어, 켜져 있는 동안 화면을 맡게 한다. 카메라 사이 전환은 Cinemachine Brain 이 섞어 준다.
// 구도 카메라가 따라갈 대상은 CameraTargetSwitcher 의 카메라 목록에 넣어 조작 캐릭터로 바꾼다.
public class CameraShotDirector : MonoBehaviour
{
    // 구도 하나와 그 구도를 맡을 Cinemachine 카메라
    [Serializable]
    private class ShotCamera
    {
        [SerializeField] private ActionData.CameraShot _shot;
        [SerializeField] private CinemachineCamera _camera;

        public ActionData.CameraShot Shot => _shot;
        public CinemachineCamera Camera => _camera;
    }

    [Tooltip("조작 중인 캐릭터를 읽을 파티")]
    [SerializeField] private PlayerParty _party;
    [Tooltip("구도마다 쓸 Cinemachine 카메라. 시작할 때 모두 끈다")]
    [SerializeField] private ShotCamera[] _shotCameras;

    private ActionData.CameraShot _currentShot;

    private void Start() => Init();
    private void Update() => UpdateShot();

    private void UpdateShot()
    {
        ActionData action = _party.Controlled.CurrentAction;
        ActionData.CameraShot shot = action != null ? action.Shot : ActionData.CameraShot.None;

        if(shot == _currentShot) return;

        _currentShot = shot;
        ApplyShot();
    }

    // 지금 구도의 카메라만 켜고 나머지는 끈다. 구도가 없으면 모두 꺼져 조작 카메라가 화면을 맡는다.
    private void ApplyShot()
    {
        for(int i = 0; i < _shotCameras.Length; i++)
        {
            _shotCameras[i].Camera.enabled = _shotCameras[i].Shot == _currentShot;
        }
    }

    private void Init()
    {
        _currentShot = ActionData.CameraShot.None;
        ApplyShot();
    }
}

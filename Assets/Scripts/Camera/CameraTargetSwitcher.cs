using Unity.Cinemachine;
using UnityEngine;

// 조작 중인 캐릭터가 바뀌면 카메라가 따라갈 대상을 새 캐릭터로 바꾼다.
// 파티는 "조작 캐릭터가 바뀌었다" 는 것만 알리고, 어떤 카메라를 어떻게 바꿀지는 이 기능이 맡는다.
// 일반 · 록온 Cinemachine 카메라의 Tracking Target 과 록온 카메라가 보는 Target Group 의 플레이어를 바꾼다.
// 퇴장하는 이전 캐릭터는 바로 대상에서 빠지므로 카메라가 담지 않는다.
public class CameraTargetSwitcher : MonoBehaviour
{
    [SerializeField] private PlayerParty _party;
    [Tooltip("조작 캐릭터를 따라가는 Cinemachine 카메라들 (일반 · 록온)")]
    [SerializeField] private CinemachineCamera[] _cameras;
    [Tooltip("록온 카메라가 보는 Target Group. 플레이어 멤버를 새 캐릭터로 바꾼다")]
    [SerializeField] private CinemachineTargetGroup _targetGroup;

    // Target Group 에 들어 있는 플레이어 캐릭터. 처음 알림을 받기 전에는 씬에 미리 넣어 둔 캐릭터를 찾지 못하므로 null 이다.
    private Transform _groupedPlayer;

    private void OnEnable() => BindPartyEvent();
    private void OnDisable() => UnbindPartyEvent();

    private void OnControlledChanged(PlayerController controlled)
    {
        Transform player = controlled.transform;

        for(int i = 0; i < _cameras.Length; i++)
        {
            _cameras[i].Target.TrackingTarget = player;
        }

        ChangeGroupedPlayer(player);
    }

    // Target Group 의 플레이어 멤버를 바꾼다. 비중 · 반경은 이전 멤버 값을 그대로 쓴다.
    // 처음 알림 때는 씬에 미리 넣어 둔 첫 캐릭터가 이미 멤버이므로 기억만 한다.
    private void ChangeGroupedPlayer(Transform player)
    {
        if(player == _groupedPlayer) return;

        int index = _groupedPlayer != null ? _targetGroup.FindMember(_groupedPlayer) : -1;

        if(index < 0)
        {
            if(_targetGroup.FindMember(player) < 0) _targetGroup.AddMember(player, 1f, 0f);

            _groupedPlayer = player;
            return;
        }

        CinemachineTargetGroup.Target member = _targetGroup.Targets[index];

        _targetGroup.RemoveMember(_groupedPlayer);
        _targetGroup.AddMember(player, member.Weight, member.Radius);
        _groupedPlayer = player;
    }

    private void BindPartyEvent()
    {
        _party.ControlledChanged += OnControlledChanged;
    }

    private void UnbindPartyEvent()
    {
        _party.ControlledChanged -= OnControlledChanged;
    }
}

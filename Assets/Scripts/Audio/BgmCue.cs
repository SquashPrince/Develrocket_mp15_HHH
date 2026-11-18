using UnityEngine;

// 씬이 시작될 때 정한 배경음으로 바꾸라고 요청한다. 타이틀처럼 씬 하나에 곡 하나인 곳에 둔다.
// 게임 씬은 진행 단계마다 곡이 바뀌므로 게임 배경음 기능을 쓴다.
public class BgmCue : MonoBehaviour
{
    [SerializeField] private BgmTrack _track = BgmTrack.Title;

    private void Start() => Init();

    private void Init()
    {
        BgmRequest.Play(_track);
    }
}

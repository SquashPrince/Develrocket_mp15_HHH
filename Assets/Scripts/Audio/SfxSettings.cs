using System;
using UnityEngine;

// 캐릭터 · 액션과 상관없는 공통 효과음을 전투 순간마다 둔다(버튼 클릭음 포함). 비어 있는 순간은 소리를 내지 않는다.
// 패링처럼 묵직한 소리를 만들도록 순간마다 추가 겹을 더해 동시에 낼 수 있다.
// 휘두름 · 적중 소리는 액션마다 달라 액션 데이터에 두므로, 일반 · 강타 적중 순간은 보통 비워 둔다.
[CreateAssetMenu(fileName = "NewSfxSettings", menuName = "HHH/Sfx Settings")]
public class SfxSettings : ScriptableObject
{
    // 전투 순간 하나에 낼 소리
    [Serializable]
    public class MomentSound
    {
        [SerializeField] private BattleFeedback.Moment _moment;
        [SerializeField] private SoundGroup _sound;
        [Tooltip("기본 소리와 동시에 낼 추가 겹. 비워 두면 기본 소리만 낸다")]
        [SerializeField] private SoundGroup[] _extraLayers;

        public BattleFeedback.Moment Moment => _moment;
        public SoundGroup Sound => _sound;
        public SoundGroup[] ExtraLayers => _extraLayers;
    }

    [Tooltip("전투 순간마다 낼 소리. 같은 순간을 두 번 넣으면 앞의 항목만 쓴다")]
    [SerializeField] private MomentSound[] _momentSounds;

    [Header("UI")]
    [SerializeField] private SoundGroup _buttonClick;

    public SoundGroup ButtonClick => _buttonClick;

    // moment 에 정한 소리 항목이 있으면 돌려준다. 기본 소리 · 추가 겹이 비어 있는지는 재생할 때 본다.
    public bool TryGetSound(BattleFeedback.Moment moment, out MomentSound sound)
    {
        foreach(MomentSound momentSound in _momentSounds)
        {
            if(momentSound.Moment != moment) continue;

            sound = momentSound;
            return true;
        }

        sound = null;
        return false;
    }
}

using System;
using UnityEngine;

// 공통 효과음 설정. 캐릭터 · 액션과 상관없는 소리(패링 · 완벽 회피 · 피격 · 적 처치 · 대시 · 회피 · 발자국 등)를 전투 순간마다 둔다.
// 목록에 없거나 소리가 비어 있는 순간은 소리를 내지 않는다. 순간을 늘리면 항목만 더한다.
// 공격의 휘두름 · 적중 소리는 캐릭터 · 액션마다 다르므로 액션 데이터에 둔다. 그래서 일반 · 강타 적중 순간은 보통 비워 둔다.
// 버튼 클릭음도 여기서 정한다.
[CreateAssetMenu(fileName = "NewSfxSettings", menuName = "HHH/Sfx Settings")]
public class SfxSettings : ScriptableObject
{
    // 전투 순간 하나에 낼 소리
    [Serializable]
    public class MomentSound
    {
        [SerializeField] private BattleFeedback.Moment _moment;
        [SerializeField] private SoundGroup _sound;

        public BattleFeedback.Moment Moment => _moment;
        public SoundGroup Sound => _sound;
    }

    [Tooltip("전투 순간마다 낼 소리. 같은 순간을 두 번 넣으면 앞의 항목만 쓴다")]
    [SerializeField] private MomentSound[] _momentSounds;

    [Header("UI")]
    [SerializeField] private SoundGroup _buttonClick;

    public SoundGroup ButtonClick => _buttonClick;

    // moment 에 정한 소리가 있으면 돌려준다.
    public bool TryGetSound(BattleFeedback.Moment moment, out SoundGroup sound)
    {
        foreach(MomentSound momentSound in _momentSounds)
        {
            if(momentSound.Moment != moment) continue;

            sound = momentSound.Sound;
            return sound.HasClip;
        }

        sound = null;
        return false;
    }
}

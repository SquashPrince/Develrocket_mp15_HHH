using System;
using UnityEngine;

// 효과음 설정. 전투 순간마다 소리 목록과 볼륨을 두고, 재생할 때 목록에서 무작위로 하나 고른다.
// 같은 순간이 반복될 때 소리가 섞여 나오게 해 반복감을 줄이기 위해서다.
// 목록에 없는 순간은 소리를 내지 않는다. 순간을 늘리면 항목만 더한다.
// 버튼 클릭음도 여기서 정한다.
[CreateAssetMenu(fileName = "NewSfxSettings", menuName = "HHH/Sfx Settings")]
public class SfxSettings : ScriptableObject
{
    // 소리 묶음 하나. 목록에서 무작위로 골라 정한 볼륨으로 재생한다.
    [Serializable]
    public class SoundGroup
    {
        [SerializeField] private AudioClip[] _clips;
        [Tooltip("이 묶음의 볼륨(0 ~ 1). 효과음 · 전체 볼륨 설정과 곱해진다")]
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        public float Volume => _volume;
        public bool HasClip => _clips != null && _clips.Length > 0;

        public AudioClip PickClip() => _clips[UnityEngine.Random.Range(0, _clips.Length)];
    }

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

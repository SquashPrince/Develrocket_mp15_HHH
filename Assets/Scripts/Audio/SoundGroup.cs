using System;
using UnityEngine;

// 소리 묶음 하나. 재생할 때 목록에서 무작위로 하나 골라 정한 볼륨으로 낸다.
// 같은 소리가 반복될 때 섞여 나오게 해 반복감을 줄이기 위해서다. 목록이 비어 있으면 소리를 내지 않는다.
// 효과음 설정(공통 소리)과 액션 데이터(캐릭터 · 액션마다 다른 소리)가 함께 쓴다.
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

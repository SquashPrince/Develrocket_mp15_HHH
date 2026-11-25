using System;
using UnityEngine;
using UnityEngine.InputSystem;

// [확인용] 여러 겹의 소리를 볼륨 · 시작 지연을 맞춰 한 번에 겹쳐 들어 본다. 키(기본 P)나 컴포넌트 메뉴로 낸다.
// 소리를 고르는 동안만 씬에 두고, 고른 뒤에는 지운다. 게임 기능에는 쓰지 않는다.
[RequireComponent(typeof(AudioSource))]
public class SoundLayerPreview : MonoBehaviour
{
    // 함께 낼 소리 한 겹
    [Serializable]
    private class Layer
    {
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [Tooltip("재생 키를 누른 뒤 이 겹을 내기까지 기다리는 시간(초)")]
        [SerializeField, Min(0f)] private float _delay;

        public AudioClip Clip => _clip;
        public float Volume => _volume;
        public float Delay => _delay;
    }

    [SerializeField] private Layer[] _layers;
    [SerializeField] private Key _playKey = Key.P;

    private AudioSource _source;
    // 겹마다 남은 지연 시간. 0 보다 작으면 이미 냈거나 기다리지 않는 겹이다.
    private float[] _timers;

    private void Awake() => CacheComponent();
    private void Update()
    {
        ReadPlayKey();
        UpdateLayers();
    }

    [ContextMenu("겹쳐 재생")]
    private void PlayAll()
    {
        if(!Application.isPlaying) return;

        _timers = new float[_layers.Length];

        for(int i = 0; i < _layers.Length; i++)
        {
            _timers[i] = _layers[i].Delay;
        }
    }

    private void ReadPlayKey()
    {
        if(Keyboard.current != null && Keyboard.current[_playKey].wasPressedThisFrame) PlayAll();
    }

    // 지연이 끝난 겹을 낸다.
    private void UpdateLayers()
    {
        if(_timers == null) return;

        for(int i = 0; i < _timers.Length; i++)
        {
            if(_timers[i] < 0f) continue;

            _timers[i] -= Time.unscaledDeltaTime;

            if(_timers[i] > 0f) continue;

            _timers[i] = -1f;

            if(_layers[i].Clip != null) _source.PlayOneShot(_layers[i].Clip, _layers[i].Volume);
        }
    }

    private void CacheComponent()
    {
        _source = GetComponent<AudioSource>();
    }
}

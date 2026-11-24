using System;
using UnityEngine;

// 동작 이펙트 한 칸의 값. 동작(ActionStep)의 이펙트 목록에 들어가며, 클립의 동작 이펙트(MotionEffect) 이벤트가 목록 번호로 고른다.
// 이펙트는 캐릭터의 자식으로 붙어 캐릭터를 따라가므로, 위치 · 회전 · 크기는 모두 캐릭터 기준 값이다.
[Serializable]
public class MotionEffectSetting
{
    [Tooltip("띄울 이펙트 Prefab. 비워 두면 이 번호의 이벤트는 아무것도 띄우지 않는다")]
    [SerializeField] private GameObject _prefab;
    [Tooltip("이펙트 위치 (캐릭터 기준)")]
    [SerializeField] private Vector3 _position = new Vector3(0f, 1f, 0f);
    [Tooltip("이펙트 회전 각도 (캐릭터 기준)")]
    [SerializeField] private Vector3 _rotation;
    [Tooltip("이펙트 크기 배수 (Prefab 원래 크기에 곱한다)")]
    [SerializeField] private Vector3 _scale = Vector3.one;

    public GameObject Prefab => _prefab;
    public Vector3 Position => _position;
    public Quaternion Rotation => Quaternion.Euler(_rotation);
    public Vector3 Scale => _scale;
}

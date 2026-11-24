using System;
using UnityEngine;

// 적중 이펙트 한 칸의 값. 한 클립 안에서 휘두르는 방향이 달라도 휘두르기마다 다른 이펙트 · 기울기를 쓰도록,
// 판정 시작 · 강타 끝 이벤트의 Int 값이 목록 번호로 고른다. 기울기는 공격 방향을 축으로 돌리는 각도다.
[Serializable]
public class HitEffectSetting
{
    [Tooltip("맞은 적마다 띄울 이펙트 Prefab. 비워 두면 이 번호의 휘두르기는 적중 이펙트를 띄우지 않는다")]
    [SerializeField] private GameObject _prefab;
    [Tooltip("공격 방향을 축으로 이펙트를 돌리는 각도. 가로 베기 이펙트라면 0 · 180 은 가로(반대 방향), 90 은 세로(내려찍기)")]
    [SerializeField] private float _rollAngle;
    [Tooltip("맞을 때마다 기울기에 더하는 무작위 각도의 최대값(±). 펀치 러시처럼 한 구간에서 여러 번 맞을 때 매번 조금씩 다르게 나오게 한다")]
    [SerializeField, Range(0f, 180f)] private float _rollRandom;

    public GameObject Prefab => _prefab;

    // 기울기에 무작위 각도를 더한 이번 타격의 기울기
    public float PickRollAngle() => _rollAngle + UnityEngine.Random.Range(-_rollRandom, _rollRandom);
}

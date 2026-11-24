using System;
using UnityEngine;

// 보스전 점수 계산 값. 전체 시간 · 재시도 · 보스(깎은 체력 비율) 세 항목을 더하고, 합계로 등급을 정한다.
// 등급 목록은 낮은 등급부터 두고, 첫 등급의 최소 점수는 0 으로 둔다.
[CreateAssetMenu(fileName = "NewScoreSettings", menuName = "HHH/Score Settings")]
public class ScoreSettings : ScriptableObject
{
    [Serializable]
    public class GradeTier
    {
        [SerializeField] private string _name;
        [SerializeField, Min(0f)] private float _minScore;

        public string Name => _name;
        public float MinScore => _minScore;
    }

    [Header("전체 시간")]
    [SerializeField, Min(0f)] private float _totalTimeMaxScore = 1000f;
    [Tooltip("전체 시간이 이 시간(초) 이하면 만점")]
    [SerializeField, Min(0f)] private float _fullScoreTime = 300f;
    [Tooltip("전체 시간이 이 시간(초) 이상이면 0점. 만점 기준 시간보다 길어야 한다")]
    [SerializeField, Min(0f)] private float _zeroScoreTime = 900f;

    [Header("재시도")]
    [SerializeField, Min(0f)] private float _retryMaxScore = 1000f;
    [Tooltip("감점 없이 허용하는 재도전 횟수. 이 횟수를 넘은 재도전부터 감점한다")]
    [SerializeField, Min(0)] private int _freeRetryCount;
    [Tooltip("허용 횟수를 넘은 재도전 1 회마다 깎는 점수")]
    [SerializeField, Min(0f)] private float _retryPenalty = 200f;

    [Header("보스")]
    [SerializeField, Min(0f)] private float _bossMaxScore = 1000f;
    [Tooltip("전멸로 실패했을 때 보스 점수에 곱하는 배율")]
    [SerializeField, Range(0f, 1f)] private float _wipedBossScoreRate = 0.5f;

    [Header("등급")]
    [Tooltip("낮은 등급부터 둔다 (F ~ S). 첫 등급의 최소 점수는 0. SS · SSS 는 넣지 않는다 (추가 조건으로 붙는다)")]
    [SerializeField] private GradeTier[] _grades;

    public float TotalTimeMaxScore => _totalTimeMaxScore;
    public float FullScoreTime => _fullScoreTime;
    public float ZeroScoreTime => _zeroScoreTime;
    public float RetryMaxScore => _retryMaxScore;
    public int FreeRetryCount => _freeRetryCount;
    public float RetryPenalty => _retryPenalty;
    public float BossMaxScore => _bossMaxScore;
    public float WipedBossScoreRate => _wipedBossScoreRate;

    // 합계 점수가 최소 점수 이상인 가장 높은 등급의 이름
    public string GetGrade(float score)
    {
        string grade = _grades[0].Name;

        foreach(GradeTier tier in _grades)
        {
            if(score >= tier.MinScore) grade = tier.Name;
        }

        return grade;
    }

    // 합계 점수로 가장 높은 등급(목록의 마지막)을 받았는지. 추가 조건 등급을 붙일지 정하는 데 쓴다.
    public bool IsTopGrade(float score) => score >= _grades[_grades.Length - 1].MinScore;
}

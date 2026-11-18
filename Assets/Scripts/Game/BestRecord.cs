using System;
using UnityEngine;

// 최고 기록 하나. JSON 파일로 저장하고 불러온다.
// 보스 실패 여부를 함께 둔다. 전멸로 실패한 기록은 보스 시간을 시간 대신 실패로 보여줘야 하기 때문이다.
[Serializable]
public class BestRecord
{
    [SerializeField] private float _score;
    [SerializeField] private string _grade;
    [SerializeField] private float _totalTime;
    [SerializeField] private float _bossTime;
    [SerializeField] private int _retryCount;
    [SerializeField] private bool _isBossFailed;

    public float Score => _score;
    public string Grade => _grade;
    public float TotalTime => _totalTime;
    public float BossTime => _bossTime;
    public int RetryCount => _retryCount;
    public bool IsBossFailed => _isBossFailed;

    // 생성자를 따로 두지 않는다. JSON 에서 불러올 때 기본 생성자를 쓰기 때문이다.
    public static BestRecord Create(float score, string grade, float totalTime, float bossTime, int retryCount, bool isBossFailed)
    {
        return new BestRecord
        {
            _score = score,
            _grade = grade,
            _totalTime = totalTime,
            _bossTime = bossTime,
            _retryCount = retryCount,
            _isBossFailed = isBossFailed
        };
    }
}

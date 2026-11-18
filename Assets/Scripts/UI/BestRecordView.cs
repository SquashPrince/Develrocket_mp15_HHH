using TMPro;
using UnityEngine;

// 타이틀의 최고 기록 표시. 점수 · 등급 · 전체 시간 · 보스 시간 · 재시도 횟수를 보여준다.
// 기록이 없으면 이 표시 자체(붙은 오브젝트)를 끈다.
// 보스 시간은 전멸로 실패한 기록이면 [실패]다. 시간 형식은 결과 화면과 같다.
// 위치 · 꾸미기는 UI 작업 때 정한다.
public class BestRecordView : MonoBehaviour
{
    private const string FAILED_TEXT = "[실패]";

    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _gradeText;
    [SerializeField] private TMP_Text _totalTimeText;
    [SerializeField] private TMP_Text _bossTimeText;
    [SerializeField] private TMP_Text _retryText;

    private void Start() => Init();

    private void Init()
    {
        BestRecord record = BestRecordStore.Current;

        if(record == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _scoreText.text = Mathf.FloorToInt(record.Score).ToString();
        _gradeText.text = record.Grade;
        _totalTimeText.text = RecordTimeText.Format(record.TotalTime);
        _bossTimeText.text = record.IsBossFailed ? FAILED_TEXT : RecordTimeText.Format(record.BossTime);
        _retryText.text = record.RetryCount.ToString();
    }
}

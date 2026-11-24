using TMPro;
using UnityEngine;

// 타이틀의 최고 기록 표시. 기록이 없으면 붙은 오브젝트를 끈다.
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

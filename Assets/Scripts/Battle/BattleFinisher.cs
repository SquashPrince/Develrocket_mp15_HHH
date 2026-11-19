using System;
using Unity.Cinemachine;
using UnityEngine;

// 마지막 일격 연출. 웨이브의 마지막 적을 처치하면 게임 전체를 잠깐 느리게, 보스전의 보스를 처치하면 게임 전체를 멈춘다.
// 언제 할지는 게임 흐름이 정해 부르고, 이 기능은 시간 배율 · 길이와 보스 마무리 카메라를 맡는다. 줌은 순간 알림을 받은 카메라 연출 기능이 낸다.
// 웨이브 : 정한 시간 동안 느려졌다가 원래 배율로 돌아온다.
// 보스 : 게임 전체를 멈추고 마무리 카메라가 처치된 보스를 바라보며 천천히 돈다. 정한 시간이 지나면 끝났다고 알려 게임 흐름이 결과 화면을 띄운다.
//   결과 화면이 떠도 게임은 멈춘 채로 두고 카메라도 계속 돈다. 결과 화면의 버튼은 씬을 바꾸기 전에 게임 시간을 되돌리므로 여기서 되돌리지 않는다.
//   보스 마무리 카메라는 Timeline 연출 작업 때 컷으로 바꿀 자리다.
// 길이와 카메라 회전은 실제 시간으로 잰다. 시간 배율을 바꾼 채로 게임 시간으로 재면 느린 만큼 길어지고, 멈추면 끝나지 않기 때문이다.
// 웨이브 슬로우 중 다른 기능(콤보 선택 등)이 시간 배율을 바꿨다면 그 값을 따르고 되돌리지 않는다. (히트스톱과 같은 규칙)
public class BattleFinisher : MonoBehaviour
{
    [Header("웨이브 마지막 적")]
    [Tooltip("느려질 때의 게임 시간 배율")]
    [SerializeField, Range(0.01f, 1f)] private float _waveSlowScale = 0.3f;
    [Tooltip("느려지는 시간(실제 시간, 초)")]
    [SerializeField, Min(0f)] private float _waveSlowDuration = 1f;

    [Header("보스")]
    [Tooltip("멈춘 뒤 결과 화면이 뜰 때까지의 시간(실제 시간, 초). 결과 화면이 떠도 멈춘 채로 있는다")]
    [SerializeField, Min(0f)] private float _bossStopDuration = 5f;
    [Tooltip("보스 마무리 카메라. 다른 카메라보다 Priority 를 높게 둔다. 시작할 때 끈다")]
    [SerializeField] private CinemachineCamera _bossFinishCamera;
    [Tooltip("보스 마무리 카메라의 Orbital Follow. Binding Mode 는 World Space 여야 가로 각도가 월드 방향과 일치한다")]
    [SerializeField] private CinemachineOrbitalFollow _bossFinishOrbit;
    [Tooltip("보스 마무리 카메라가 보스 주위를 도는 속도(도 / 실제 시간 초). 음수면 반대로 돈다")]
    [SerializeField] private float _bossOrbitSpeed = 15f;
    [Tooltip("화면을 비추는 메인 카메라. 마무리 카메라가 지금 화면 쪽에서 돌기 시작하게 한다")]
    [SerializeField] private Camera _mainCamera;

    // 웨이브 슬로우 전의 시간 배율과 슬로우가 건 배율. 끝날 때 배율이 슬로우가 건 값 그대로일 때만 되돌린다.
    private float _previousTimeScale = 1f;
    private float _appliedTimeScale = 1f;
    private float _waveTimer;
    private float _bossTimer;
    private bool _isBossCameraActive;

    private bool IsWaveSlowing => _waveTimer > 0f;

    // 보스를 처치하고 정한 시간이 지나면 알린다. 게임 흐름이 결과 화면을 띄운다.
    public event Action BossFinishEnded;

    private void Start() => Init();

    private void Update()
    {
        UpdateWaveSlow();
        UpdateBossFinish();
    }

    private void OnDisable()
    {
        if(IsWaveSlowing) RestoreTimeScale();

        _waveTimer = 0f;
    }

    // 웨이브 마지막 적 처치. 진행 중인 슬로우가 있으면 처음부터 다시 센다.
    public void PlayWaveFinish()
    {
        BattleFeedback.Raise(BattleFeedback.Moment.WaveFinish);

        if(_waveSlowDuration <= 0f) return;

        if(!IsWaveSlowing) _previousTimeScale = Time.timeScale;

        _appliedTimeScale = _waveSlowScale;
        _waveTimer = _waveSlowDuration;
        Time.timeScale = _waveSlowScale;
    }

    // 보스 처치. 게임 전체를 멈추고, 보스가 있으면 마무리 카메라로 보스를 비춘다. 이 뒤로 게임 시간은 되돌리지 않는다.
    public void PlayBossFinish(Transform boss)
    {
        BattleFeedback.Raise(BattleFeedback.Moment.BossFinish);

        _waveTimer = 0f;
        Time.timeScale = 0f;

        if(boss != null) StartBossCamera(boss);

        _bossTimer = _bossStopDuration;

        if(_bossTimer <= 0f) BossFinishEnded?.Invoke();
    }

    private void UpdateWaveSlow()
    {
        if(!IsWaveSlowing) return;

        _waveTimer -= Time.unscaledDeltaTime;

        if(_waveTimer > 0f) return;

        _waveTimer = 0f;
        RestoreTimeScale();
    }

    private void UpdateBossFinish()
    {
        if(_isBossCameraActive) _bossFinishOrbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, _bossFinishOrbit.HorizontalAxis.Value + _bossOrbitSpeed * Time.unscaledDeltaTime);

        if(_bossTimer <= 0f) return;

        _bossTimer -= Time.unscaledDeltaTime;

        if(_bossTimer <= 0f) BossFinishEnded?.Invoke();
    }

    // 마무리 카메라가 보스를 따라가며 바라보게 하고, 지금 화면이 보고 있는 쪽에서 돌기 시작하게 한다.
    // Orbital Follow 는 가로 각도 방향의 반대편에 서므로, 보스에서 메인 카메라로 향하는 방향의 반대 각도를 준다.
    private void StartBossCamera(Transform boss)
    {
        Vector3 fromBoss = _mainCamera.transform.position - boss.position;

        _bossFinishCamera.Target.TrackingTarget = boss;
        _bossFinishOrbit.HorizontalAxis.Value = Mathf.Atan2(-fromBoss.x, -fromBoss.z) * Mathf.Rad2Deg;
        _bossFinishCamera.enabled = true;
        _isBossCameraActive = true;
    }

    // 슬로우 중 다른 기능이 시간 배율을 바꿨으면 그쪽 값을 그대로 둔다.
    private void RestoreTimeScale()
    {
        if(Time.timeScale == _appliedTimeScale) Time.timeScale = _previousTimeScale;
    }

    private void Init()
    {
        _bossFinishCamera.enabled = false;
    }
}

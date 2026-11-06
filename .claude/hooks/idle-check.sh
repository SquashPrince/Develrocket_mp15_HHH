#!/usr/bin/env bash
# UserPromptSubmit 훅 : 마지막 입력 시각과 현재 시각을 비교한다.
# 30분 이상 지났을 때만 한 줄을 출력하고, 그 외에는 아무것도 출력하지 않는다. (토큰 절약)

state_dir="${CLAUDE_PROJECT_DIR:-.}/.claude/state"
state_file="$state_dir/last-prompt-time"
limit_seconds=1800

now=$(date +%s)
last=""
if [ -f "$state_file" ]; then
  last=$(cat "$state_file")
fi

# 이번 입력 시각을 다음 비교 기준으로 저장한다.
mkdir -p "$state_dir"
echo "$now" > "$state_file"

# 첫 실행이거나 기록이 숫자가 아니면 비교하지 않는다.
case "$last" in
  ''|*[!0-9]*) exit 0 ;;
esac

gap=$((now - last))
if [ "$gap" -lt "$limit_seconds" ]; then
  exit 0
fi

echo "[작업 시간] 마지막 입력 $(date -d "@$last" +%H:%M) → 현재 $(date -d "@$now" +%H:%M) ($((gap / 60))분 경과). 그동안 작업을 중단했었는지 사용자에게 확인하고, agentlog 시간 기록에 반영하세요."
exit 0

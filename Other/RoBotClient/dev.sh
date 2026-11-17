#!/usr/bin/env bash
# Dashboard dev loop. Hot reload applies most edits (Razor markup, method bodies) in about a second while the
# bots keep playing. Edits it can't apply (new fields, signatures, records) trigger an automatic restart;
# the running bots are logged out cleanly and respawned by the dashboard itself (running-bots.json).
cd "$(dirname "$0")"
export DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1
PORT=5299               # the "http" launch profile's port
DOWN_LIMIT=90           # seconds unreachable before the watchdog rebuilds (a normal rebuild + boot takes ~20-40s)
LOG=dashboard-watch.log # timestamped watch lines: which file each restart came from, build errors, boots

# Echo everything to the terminal; keep watch's own lines (and errors/boots) in $LOG with a timestamp.
stamp() {
    while IFS= read -r line; do
        printf '%s\n' "$line"
        case "$line" in
            *"dotnet watch"*|*"dev.sh:"*|*"error"*|*"Application started"*|*"Respawned"*)
                printf '%(%F %T)T %s\n' -1 "$line" >> "$LOG" ;;
        esac
    done
}

watch_pid=
trap '[ -n "$watch_pid" ] && kill -- -"$watch_pid" 2>/dev/null; exit 0' INT TERM
set -m # job control: each background job gets its own process group, so one kill stops watch, the app and stamp
while :; do
    dotnet watch --project RoBotClient.Web --non-interactive run --launch-profile http "$@" 2>&1 | stamp &
    watch_pid=$(jobs -p %%) # the job's group leader (dotnet watch); $! would be stamp
    # Watchdog: after a failed build watch waits for the next file edit, leaving the dashboard (and the bots) down
    # until someone touches a file (65 min once). Builds fail transiently too (another build holding obj/), so
    # rebuild when the app has been unreachable for DOWN_LIMIT.
    # ponytail: a real compile error rebuilds every DOWN_LIMIT until fixed; harmless, just noisy.
    down=0
    while kill -0 "$watch_pid" 2>/dev/null; do
        sleep 10
        if curl -s -o /dev/null --max-time 5 "http://localhost:$PORT/"; then down=0; else down=$((down + 10)); fi
        if [ "$down" -ge "$DOWN_LIMIT" ]; then
            echo "dev.sh: dashboard unreachable for ${DOWN_LIMIT}s, restarting dotnet watch" | stamp
            # TERM lets a still-running app save running-bots.json; watch itself ignores TERM and INT while it
            # waits for a file change, so KILL follows.
            kill -- -"$watch_pid" 2>/dev/null
            sleep 15
            kill -KILL -- -"$watch_pid" 2>/dev/null
            down=0
        fi
    done
    echo "dev.sh: dotnet watch exited, restarting in 2s (Ctrl+C to stop)" | stamp
    sleep 2
done

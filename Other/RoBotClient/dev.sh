#!/usr/bin/env bash
# Dashboard dev loop. Hot reload applies most edits (Razor markup, method bodies) in about a second while the
# bots keep playing. Edits it can't apply (new fields, signatures, records) trigger an automatic restart;
# the running bots are logged out cleanly and respawned by the dashboard itself (running-bots.json).
cd "$(dirname "$0")"
export DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1
exec dotnet watch --project RoBotClient.Web --non-interactive run --launch-profile http "$@"

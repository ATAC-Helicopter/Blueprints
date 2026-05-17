#!/usr/bin/env sh
set -eu

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"

if [ "$(uname -s)" = "Linux" ]; then
  if [ -n "${WAYLAND_DISPLAY:-}" ] && [ -z "${DISPLAY:-}" ]; then
    cat >&2 <<'MSG'
Blueprints is running in a Wayland session without DISPLAY set.
Avalonia's Linux desktop backend for this app currently requires X11/XWayland.

Install/enable XWayland and launch from an environment that exports DISPLAY.
On many desktops this happens automatically; in this shell it is currently missing.
MSG
  elif [ -n "${DISPLAY:-}" ] && [ -z "${XAUTHORITY:-}" ]; then
    cat >&2 <<'MSG'
Blueprints sees DISPLAY, but XAUTHORITY is not set.
If XOpenDisplay still fails, this shell likely lacks permission to connect to XWayland.
MSG
  fi
fi

cd "${repo_root}"
dotnet run --project Blueprints.App/Blueprints.App.csproj "$@"

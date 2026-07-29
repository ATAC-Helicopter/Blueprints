#!/usr/bin/env sh
set -eu

printf 'Session\n'
printf '  XDG_SESSION_TYPE=%s\n' "${XDG_SESSION_TYPE:-}"
printf '  WAYLAND_DISPLAY=%s\n' "${WAYLAND_DISPLAY:-}"
printf '  DISPLAY=%s\n' "${DISPLAY:-}"
printf '  XAUTHORITY=%s\n' "${XAUTHORITY:-}"
printf '\n'

printf 'Sockets\n'
if [ -d /tmp/.X11-unix ]; then
  printf '  /tmp/.X11-unix:\n'
  ls -la /tmp/.X11-unix | sed 's/^/    /'
else
  printf '  /tmp/.X11-unix: missing\n'
fi

if [ -n "${XDG_RUNTIME_DIR:-}" ]; then
  printf '  XDG_RUNTIME_DIR=%s\n' "${XDG_RUNTIME_DIR}"
  find "${XDG_RUNTIME_DIR}" -maxdepth 1 \( -name 'wayland-*' -o -name '.mutter-Xwaylandauth.*' \) -print 2>/dev/null | sed 's/^/    /' || true
fi

printf '\n'
printf 'Tools\n'
for tool in Xwayland xauth xdpyinfo xeyes xclock; do
  if command -v "$tool" >/dev/null 2>&1; then
    printf '  %-8s %s\n' "$tool" "$(command -v "$tool")"
  else
    printf '  %-8s missing\n' "$tool"
  fi
done

printf '\n'
printf 'Display probes\n'
for display in "${DISPLAY:-}" "${GNOME_SETUP_DISPLAY:-}" :0 :1 :2; do
  [ -n "$display" ] || continue
  printf '  DISPLAY=%s: ' "$display"
  if command -v xdpyinfo >/dev/null 2>&1; then
    if DISPLAY="$display" xdpyinfo >/dev/null 2>&1; then
      printf 'ok\n'
    else
      printf 'failed\n'
    fi
  else
    printf 'xdpyinfo missing\n'
  fi
done

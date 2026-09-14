#!/usr/bin/env bash
# Idempotent dev environment control. Safe to run after a Mac restart once OrbStack is open.
#   ./dev.sh up      build image if missing, apply manifests, wait for the pod
#   ./dev.sh shell   open a shell in the dev pod
#   ./dev.sh exec …  run a command in the dev pod
#   ./dev.sh start   start Aspire (Postgres + API + Angular) in the pod
#   ./dev.sh stop    stop Aspire and its managed resources
#   ./dev.sh logs    follow the Aspire host log
#   ./dev.sh ports   expose dashboard :15888 and web :4201 to macOS
#   ./dev.sh rebuild force-rebuild the image and restart the pod
#   ./dev.sh down    delete the deployment (PVC caches are kept)
set -euo pipefail

export PATH="$HOME/.orbstack/bin:$PATH"
cd "$(dirname "$0")"

NS=hiring-dev
IMAGE=hiring-devbox:local

wait_for_orbstack() {
  orb start >/dev/null 2>&1 || true
  for _ in $(seq 60); do
    if docker info >/dev/null 2>&1 && kubectl --context orbstack get nodes >/dev/null 2>&1; then return; fi
    sleep 2
  done
  echo "OrbStack docker/kubernetes not reachable" >&2
  exit 1
}

build() { docker build -t "$IMAGE" .devcontainer; }

up() {
  wait_for_orbstack
  docker image inspect "$IMAGE" >/dev/null 2>&1 || build
  # migrate from the old bare Pod
  kubectl --context orbstack -n "$NS" delete pod devbox --ignore-not-found >/dev/null 2>&1 || true
  kubectl --context orbstack apply -f .devcontainer/k8s-devpod.yaml
  kubectl --context orbstack -n "$NS" rollout status deploy/postgres --timeout=180s
  kubectl --context orbstack -n "$NS" rollout status deploy/devbox --timeout=180s
}

start_stack() {
  up
  kubectl --context orbstack -n "$NS" exec deploy/devbox -- bash -lc '
    set -e
    if test -f /tmp/aspire.pid && kill -0 $(cat /tmp/aspire.pid) 2>/dev/null; then echo "Aspire is already running"; exit; fi
    rm -f /tmp/aspire.pid /tmp/aspire.log
    pkill -x dcp 2>/dev/null || true
    cd /workspace
    dotnet build HiringPlatform.slnx >/tmp/hiring-build.log 2>&1
    nohup dotnet run --project src/HiringPlatform.AppHost --no-build --launch-profile http >/tmp/aspire.log 2>&1 </dev/null &
    echo $! >/tmp/aspire.pid
  '
  echo "Aspire is starting. Run: ./dev.sh ports"
}

stop_stack() {
  kubectl --context orbstack -n "$NS" exec deploy/devbox -- bash -lc '
    test -f /tmp/aspire.pid && kill $(cat /tmp/aspire.pid) 2>/dev/null || true
    rm -f /tmp/aspire.pid
    pkill -x dcp 2>/dev/null || true
  ' || true
}

case "${1:-up}" in
  up) up ;;
  start) start_stack ;;
  stop) stop_stack ;;
  logs) kubectl --context orbstack -n "$NS" exec -it deploy/devbox -- tail -f /tmp/aspire.log ;;
  ports) echo "Dashboard: http://localhost:15888  Web: http://localhost:4201"; kubectl --context orbstack -n "$NS" port-forward deploy/devbox 15888:15888 4201:4201 ;;
  shell) kubectl --context orbstack -n "$NS" exec -it deploy/devbox -- bash ;;
  exec) shift; kubectl --context orbstack -n "$NS" exec deploy/devbox -- bash -lc "$*" ;;
  rebuild) wait_for_orbstack; build; up; kubectl --context orbstack -n "$NS" rollout restart deploy/devbox; kubectl --context orbstack -n "$NS" rollout status deploy/devbox ;;
  down) stop_stack; kubectl --context orbstack -n "$NS" delete deploy/devbox deploy/postgres --ignore-not-found ;;
  *) echo "usage: $0 {up|start|stop|logs|ports|shell|exec <cmd>|rebuild|down}" >&2; exit 2 ;;
esac

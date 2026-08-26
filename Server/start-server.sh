#!/bin/sh
# Possession Server one-click launcher (Linux / macOS): auto-build and run.
# Usage: chmod +x start-server.sh (first time), then  ./start-server.sh [-addr :9000 ...]
# Any extra arguments are passed through to the server.
cd "$(dirname "$0")"

if ! command -v go >/dev/null 2>&1; then
    echo "[ERROR] Go not found. Please install Go 1.26.5+ first: https://go.dev/dl/"
    exit 1
fi

echo "[1/2] Building server..."
mkdir -p bin
if ! go build -o bin/server ./cmd/server; then
    echo "[ERROR] Build failed. Send the messages above to the dev team."
    exit 1
fi

echo "[2/2] Starting server (default :8080; db data/, files repo/, daily logs log/)"
echo "Press Ctrl+C to stop."
echo ""
./bin/server "$@"

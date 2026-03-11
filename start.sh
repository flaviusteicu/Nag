#!/bin/bash
# Nag Launcher - double-click this or run ./start.sh
DIR="$(cd "$(dirname "$0")" && pwd)"
chmod +x "$DIR/Nag"
"$DIR/Nag" "$@"

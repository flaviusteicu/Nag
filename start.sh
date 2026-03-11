#!/bin/bash
# Nag Launcher - double-click this or run ./start.sh
DIR="$(cd "$(dirname "$0")" && pwd)"

# macOS: strip Gatekeeper quarantine from downloaded files
if [ "$(uname)" = "Darwin" ]; then
    xattr -dr com.apple.quarantine "$DIR"
fi

chmod +x "$DIR/Nag"
"$DIR/Nag" "$@"

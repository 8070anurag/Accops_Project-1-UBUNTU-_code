#!/bin/bash
# ============================================
#  App.Net - Build and Run Script
# ============================================

set -e

echo "=== Building App.Net ==="
dotnet build --configuration Release

echo ""
echo "=== Starting App.Net Process Manager ==="
echo "(Running with sudo for process management privileges)"
echo ""

sudo dotnet run --configuration Release

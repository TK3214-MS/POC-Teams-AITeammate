#!/usr/bin/env bash
set -euo pipefail

echo "========================================="
echo " AI Teammate - Development Environment Setup"
echo "========================================="

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

check_command() {
    if command -v "$1" &>/dev/null; then
        echo -e "${GREEN}✓${NC} $1 found: $($1 --version 2>/dev/null | head -1)"
        return 0
    else
        echo -e "${RED}✗${NC} $1 not found"
        return 1
    fi
}

echo ""
echo "1. Checking prerequisites..."
echo "-------------------------------------------"

# .NET SDK
if check_command dotnet; then
    DOTNET_VERSION=$(dotnet --version)
    MAJOR=$(echo "$DOTNET_VERSION" | cut -d. -f1)
    if [ "$MAJOR" -lt 10 ]; then
        echo -e "${YELLOW}⚠ .NET SDK $DOTNET_VERSION found, but 10.0+ is required${NC}"
        echo "  Install from: https://dot.net/download"
    fi
else
    echo "  Install from: https://dot.net/download"
fi

# Node.js
if check_command node; then
    NODE_VERSION=$(node --version | sed 's/v//')
    MAJOR=$(echo "$NODE_VERSION" | cut -d. -f1)
    if [ "$MAJOR" -lt 22 ]; then
        echo -e "${YELLOW}⚠ Node.js $NODE_VERSION found, but 22+ is recommended${NC}"
    fi
else
    echo "  Install from: https://nodejs.org/"
fi

# Azure CLI
if ! check_command az; then
    echo "  Install from: https://aka.ms/install-azure-cli"
fi

# Azure Developer CLI
if ! check_command azd; then
    echo "  Install: curl -fsSL https://aka.ms/install-azd.sh | bash"
fi

# Docker
check_command docker || echo "  Install from: https://docs.docker.com/get-docker/"

echo ""
echo "2. Restoring .NET dependencies..."
echo "-------------------------------------------"
cd "$(dirname "$0")/.."
dotnet restore TeamsAITeammate.slnx

echo ""
echo "3. Building solution..."
echo "-------------------------------------------"
dotnet build TeamsAITeammate.slnx -c Debug

echo ""
echo "4. Creating appsettings.Development.json template..."
echo "-------------------------------------------"
DEV_SETTINGS="src/TeamsAITeammate.Agent/appsettings.Development.json"
if [ -f "$DEV_SETTINGS" ]; then
    echo -e "${YELLOW}⚠ $DEV_SETTINGS already exists, skipping${NC}"
else
    echo "  Created $DEV_SETTINGS — fill in your values"
fi

echo ""
echo "5. Dev Tunnel setup..."
echo "-------------------------------------------"
if command -v devtunnel &>/dev/null; then
    echo -e "${GREEN}✓${NC} devtunnel CLI found"
    echo "  To create a tunnel: devtunnel create --allow-anonymous"
    echo "  To start: devtunnel host --port 5000"
else
    echo -e "${YELLOW}⚠ devtunnel CLI not found${NC}"
    echo "  Install: https://aka.ms/devtunnels/download"
fi

echo ""
echo "========================================="
echo -e "${GREEN} Setup complete!${NC}"
echo ""
echo " Next steps:"
echo "  1. Fill in appsettings.Development.json with your Bot ID and secrets"
echo "  2. Run: devtunnel host --port 5000"
echo "  3. Run: dotnet run --project src/TeamsAITeammate.Agent"
echo "  4. Sideload the Teams app from appPackage/"
echo "========================================="

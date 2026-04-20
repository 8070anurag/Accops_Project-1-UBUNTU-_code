#!/bin/bash
# ============================================
#  App.Net Linux Process Manager - VM Setup
#  Run this script on your Linux VM as root
# ============================================

set -e

echo "=========================================="
echo "  App.Net - Full VM Setup Script"
echo "=========================================="

# --- 1. Update system ---
echo ""
echo "[1/7] Updating system packages..."
apt-get update -y && apt-get upgrade -y

# --- 2. Install .NET 8 SDK ---
echo ""
echo "[2/7] Installing .NET 8 SDK..."

# Install prerequisites
apt-get install -y wget apt-transport-https software-properties-common

# Add Microsoft package repository (Ubuntu/Debian)
if [ -f /etc/os-release ]; then
    . /etc/os-release
    DISTRO=$ID
    VERSION=$VERSION_ID
else
    echo "Cannot detect OS. Assuming Ubuntu 22.04."
    DISTRO="ubuntu"
    VERSION="22.04"
fi

wget "https://packages.microsoft.com/config/${DISTRO}/${VERSION}/packages-microsoft-prod.deb" -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

apt-get update -y
apt-get install -y dotnet-sdk-8.0

echo "Installed .NET version:"
dotnet --version

# --- 3. Install useful tools ---
echo ""
echo "[3/7] Installing additional tools..."
apt-get install -y procps coreutils util-linux python3 bpftrace xclip

# --- 3b. Install clipnotify (real-time clipboard event notification) ---
# clipnotify uses X11 XFixes extension (same approach as xrdp clipboard.c)
# It blocks on XNextEvent — fires only when clipboard changes (event-driven, not a loop)
# Reference: https://github.com/cdown/clipnotify
echo ""
echo "[3b/7] Installing clipnotify (X11 XFixes clipboard monitor)..."
apt-get install -y libxfixes-dev libx11-dev gcc make git

if command -v clipnotify &> /dev/null; then
    echo "clipnotify is already installed."
else
    echo "Building clipnotify from source..."
    rm -rf /tmp/clipnotify
    git clone https://github.com/cdown/clipnotify.git /tmp/clipnotify
    cd /tmp/clipnotify && make && cp clipnotify /usr/local/bin/
    rm -rf /tmp/clipnotify
    echo "clipnotify installed to /usr/local/bin/clipnotify"
fi

# --- 4. Enable cgroups v2 ---
echo ""
echo "[4/7] Configuring cgroups v2..."

# Check if cgroups v2 is already mounted
if mount | grep -q "cgroup2"; then
    echo "cgroups v2 is already enabled."
else
    echo "Enabling cgroups v2 (may require reboot)..."
    # Add cgroup v2 kernel parameter for next boot
    if grep -q "systemd.unified_cgroup_hierarchy" /etc/default/grub; then
        echo "GRUB already configured for cgroup v2."
    else
        sed -i 's/GRUB_CMDLINE_LINUX_DEFAULT="/GRUB_CMDLINE_LINUX_DEFAULT="systemd.unified_cgroup_hierarchy=1 /' /etc/default/grub
        update-grub
        echo "GRUB updated. A reboot will be needed after setup completes."
    fi
fi

# Enable cpu and memory controllers at root level
echo "+cpu +memory" > /sys/fs/cgroup/cgroup.subtree_control 2>/dev/null || true

# --- 5. Create a dedicated user for testing (optional) ---
echo ""
echo "[5/7] Creating test user 'dummyuser' (UID 1001)..."
if id "dummyuser" &>/dev/null; then
    echo "User 'dummyuser' already exists."
else
    useradd -m -u 1001 -s /bin/bash dummyuser
    echo "dummyuser:password123" | chpasswd
    echo "Created user 'dummyuser' with password 'password123'."
fi

# --- 6. Configure passwordless sudo for the app user ---
echo ""
echo "[6/7] Configuring passwordless sudo..."

CURRENT_USER=${SUDO_USER:-$USER}

# Create sudoers entry for passwordless operation of required commands
SUDOERS_FILE="/etc/sudoers.d/appnet"
cat > "$SUDOERS_FILE" << 'EOF'
# App.Net Process Manager - Passwordless sudo for required commands
# Allow the app to manage processes, cgroups, and signals without password

%sudo ALL=(ALL) NOPASSWD: /usr/bin/kill
%sudo ALL=(ALL) NOPASSWD: /usr/bin/pkill
%sudo ALL=(ALL) NOPASSWD: /usr/bin/pgrep
%sudo ALL=(ALL) NOPASSWD: /bin/ps
%sudo ALL=(ALL) NOPASSWD: /usr/bin/ps
%sudo ALL=(ALL) NOPASSWD: /bin/mkdir
%sudo ALL=(ALL) NOPASSWD: /usr/bin/mkdir
%sudo ALL=(ALL) NOPASSWD: /bin/bash
%sudo ALL=(ALL) NOPASSWD: /bin/rm
%sudo ALL=(ALL) NOPASSWD: /usr/bin/rm
%sudo ALL=(ALL) NOPASSWD: /usr/bin/tee
EOF

chmod 0440 "$SUDOERS_FILE"
echo "Passwordless sudo configured in $SUDOERS_FILE"

# Make sure the current user is in sudo group
usermod -aG sudo "$CURRENT_USER" 2>/dev/null || true

echo ""
echo "=========================================="
echo "  Setup Complete!"
echo "=========================================="
echo ""
echo "Next steps:"
echo "  1. Copy your project files to the VM"
echo "  2. cd /path/to/your/project"
echo "  3. dotnet build"
echo "  4. sudo dotnet run"
echo ""
echo "To test with dummy load:"
echo "  python3 dummy_load.py &"
echo ""
if ! mount | grep -q "cgroup2"; then
    echo "⚠  IMPORTANT: Reboot required to enable cgroups v2!"
    echo "  Run: sudo reboot"
fi

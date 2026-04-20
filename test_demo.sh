#!/bin/bash
# ============================================
#  App.Net - Test Demo Script
#  Run this in a SEPARATE terminal to create
#  test processes, then manage them from the app
# ============================================

echo "=== App.Net Complete Feature Demo ==="
echo ""

# Start dummy load processes
echo "[*] Starting 3 dummy Python processes..."
python3 dummy_load.py &
PID1=$!
python3 dummy_load.py &
PID2=$!
python3 dummy_load.py &
PID3=$!

echo ""
echo "Started python3 processes with PIDs: $PID1, $PID2, $PID3"
echo "--------------------------------------------------------"
echo "NOW GO TO THE C# APP -> CHOOSE OPTION 2 (MONITORING MODE)"
echo "--------------------------------------------------------"
echo "Run these commands to demo each API feature:"
echo ""

echo "1. PAUSE AND RESUME (ISuspendResumeService)"
echo "   suspend $PID1                 # Pauses the process"
echo "   resume $PID1                  # Resumes it"
echo ""

echo "2. EXCLUDE LIST PROTECTION (IExcludeListService)"
echo "   suspend 1                     # Tries to suspend systemd -> BLOCKED"
echo "   exclude-add python3           # Adds python3 to protected list"
echo "   suspend $PID2                 # Fails because it's now protected"
echo "   exclude-remove python3        # Removes protection"
echo ""

echo "3. BLOCK / UNBLOCK ALL (IBlockService)"
echo "   block-process python3         # Freezes ALL python3 processes"
echo "   unblock-process python3       # Resumes ALL python3 processes"
echo ""

echo "4. RESTRICT CPU & MEMORY (IResourceRestrictionService) - Dynamic!"
echo "   restrict-cpu $PID3 python3    # dynamically sets CPU limit"
echo "   restrict-mem $PID3 python3    # dynamically sets memory limit"
echo "   restrict-user python3 $(whoami) # Restricts only YOUR processes"
echo ""

echo "5. REMOVE RESTRICTIONS (IResourceRestrictionService)"
echo "   remove-restrict $PID3         # Removes limits and closes cgroups"
echo ""

echo "6. REDUCE & RESTORE PRIORITY (IPriorityService)"
echo "   reduce-priority $PID1         # Lowers priority (sets nice to 10)"
echo "   restore-priority $PID1        # Restores normal priority (nice to 0)"
echo ""

echo "7. RELEASE MEMORY SAFELY (IMemoryReleaseService)"
echo "   release-memory $PID2          # Runs malloc_trim(0) to free heap memory"
echo ""
echo "--------------------------------------------------------"
echo "Press Ctrl+C in this terminal to kill all test processes."
echo "--------------------------------------------------------"

# Wait for user to stop
wait

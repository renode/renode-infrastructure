# Renode Semihosting Project - Session Memory

## What We Accomplished

### ✅ Successfully Implemented ARM Semihosting SYS_WRITE (0x05)
- **Location**: `/home/jrmiller/projects/renode-mainline/src/Infrastructure/src/Emulator/Cores/Arm/Arm.cs` (lines 174-192)
- **Status**: WORKING - Verified with test program
- **What it does**: Enables `printf()` output from ARM firmware using semihosting

### ✅ Built Custom Renode
- **Location**: `/home/jrmiller/projects/renode-mainline/`
- **Version**: v1.16.0.19023 (custom build 2026-02-13)
- **Changes**: Mainline Renode + SYS_WRITE implementation

### ✅ Testing Infrastructure
- **Test program**: `/home/jrmiller/projects/renode-mainline/semihosting-test/`
- **Platform description**: `stm32f4-with-semihosting.repl`
- **Robot Framework**: Installed in `.venv` directory

### ✅ Configuration Files
- **Renode config**: `~/.config/renode/config` (16pt font, INI format with `[termsharp]`)
- **Font issue**: Resolved - use `font-size=16` not `terminal-font-size:`

## Key Technical Discoveries

### Semihosting in Renode
- Mainline Renode HAS partial semihosting (SYS_READC, SYS_WRITEC, SYS_WRITE0)
- SYS_WRITE (0x05) was MISSING - needed for printf()
- Registration: Create `UART.SemihostingUart @ cpu` in .repl file
- Command: `showAnalyzer sysbus.cpu.semihostingUart` (NO custom window title)

### Build System
- Renode uses dotnet 10.0, builds in ~2 minutes
- Infrastructure is separate submodule
- ARM toolchain: `~/.platformio/packages/toolchain-gccarmnoneeabi/`

### DWT Peripheral (Session 2)
- **Purpose**: Data Watchpoint and Trace - provides cycle counting for firmware timing
- **Address**: 0xE0001000 (CoreDebug peripheral)
- **Required parameter**: `frequency: 168000000` (CPU frequency)
- **Why needed**: Firmware uses DWT_CYCCNT for delay_us() timing
- **Without DWT**: Firmware gets stuck polling non-existent peripheral

### File Types in Renode (Session 2)
- **.repl files**: Platform descriptions - define hardware peripherals and addresses
  - Example: `dwt: Miscellaneous.DWT @ sysbus 0xE0001000`
  - Use for: Adding peripherals, configuring hardware
- **.resc files**: Renode scripts - execute monitor commands
  - Example: `machine LoadPlatformDescription @file.repl`
  - Use for: Loading firmware, starting emulation, configuration

### Robot Framework Tests (Session 2)
- Test location: `tests/platforms/*.robot`
- Structure: Variables, Keywords, Test Cases
- Key commands:
  - `Execute Command` - Run Renode monitor commands
  - `Create Terminal Tester` - Set up UART/semihosting monitoring
  - `Wait For Line On Uart` - Check for expected output
  - `Start Emulation` - Begin test execution

### Debugging Lessons (See DEBUG_PROTOCOL.md)
- User observations > terminal output
- GUI apps may not show output in terminal
- Verify state before troubleshooting
- **Token efficiency**: Use targeted greps, not full log reads (Session 2)
- **Ask user first**: User observation is faster and free (Session 2)

## Project File Locations

```
/home/jrmiller/projects/
├── hope-renode-infrastructure/          # HOPE fork (reference only)
│   └── src/Emulator/Cores/Arm/Arm.cs   # Original partial semihosting
├── renode-mainline/                     # OUR WORKING BUILD
│   ├── src/Infrastructure/src/Emulator/Cores/Arm/Arm.cs  # ← SYS_WRITE HERE
│   ├── .venv/                           # Robot Framework
│   ├── stm32f4-with-semihosting.repl   # Platform with semihosting
│   ├── semihosting-test/                # Working test program
│   │   ├── simple-main.c               # Direct SYS_WRITE test
│   │   ├── test.resc                   # Test script
│   │   └── Makefile                    # Build system
│   └── output/bin/Release/              # Built Renode binaries

/home/jrmiller/reefpilot/firmware/
└── .pio/build/renode_test/firmware.elf  # ReefPilot firmware (logging disabled)
```

## Session 2 Accomplishments (2026-02-13)

### ✅ Completed Tasks

1. **ReefPilot Firmware Logging** - Added `DEBUG_LOGGING_ENABLED=1`, rebuilt firmware
   - Status: Firmware runs but no printf output (firmware config issue, not Renode)
   - Deferred: Not blocking mainline PR

2. **Robot Framework Tests** - COMPLETE ✅
   - File: `tests/platforms/ARM-Cortex-M4-semihosting.robot`
   - Tests: `Should Output Via SYS_WRITE` (1.79s) and `Should Handle Multiple Writes` (0.51s)
   - Status: Both passing, 2.65s total

3. **Mainline Contribution Prep** - READY ✅
   - Commit: bf26d899 with clean, factual message
   - Project structure: ReefPilot tests moved to ReefPilot repo
   - DWT peripheral added to platform (fixes CoreDebug polling)
   - Next: Create PR to renode/renode-infrastructure

### 📋 Ready for Next Session

1. **Create GitHub PR**
   - Push to feature branch: `feature/arm-semihosting-sys-write`
   - Create PR to renode/renode-infrastructure
   - Include test results in description

2. **Address Review Feedback**
   - Respond to maintainer comments
   - Make requested changes if needed

### 4. Additional Semihosting Operations (Optional)
Consider implementing:
- SYS_OPEN (0x01)
- SYS_CLOSE (0x02)
- SYS_READ (0x06)
- SYS_ISTTY (0x09)
- SYS_EXIT (0x18)

## Important Commands

```bash
# Build Renode
cd /home/jrmiller/projects/renode-mainline && ./build.sh --net

# Test semihosting
cd /home/jrmiller/projects/renode-mainline
./renode semihosting-test/test.resc

# Activate Robot Framework venv
cd /home/jrmiller/projects/renode-mainline
source .venv/bin/activate

# Run Robot tests
./renode-test-with-venv tests/platforms/some-test.robot
```

## User Preferences

- **Font**: Prefers JetBrains Mono / Deja Vu Sans Mono (16pt+)
- **System**: Arch Linux, Wayland, Gnome
- **Style**: Diagnose root cause, avoid workarounds
- **Communication**: Direct, technical, meta-aware

## Related Documentation

- Context document: `.claude/SEMIHOSTING_CONTEXT.md`
- Debug protocol: `.claude/DEBUG_PROTOCOL.md`

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

### Debugging Lessons (See DEBUG_PROTOCOL.md)
- User observations > terminal output
- GUI apps may not show output in terminal
- Verify state before troubleshooting

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

## Next Session Tasks

### 1. Fix ReefPilot Firmware Logging
**Problem**: `DEBUG_LOGGING_ENABLED` is not defined in renode_test build
**Solution**: Add to `platformio.ini`:
```ini
[env:renode_test]
build_flags =
    ...existing flags...
    -DDEBUG_LOGGING_ENABLED=1
```

### 2. Write Robot Framework Tests
**Purpose**: Automated testing for mainline PR
**Location**: Create generic ARM semihosting tests (NOT ReefPilot-specific)
**File**: `tests/platforms/ARM-semihosting.robot`

### 3. Prepare Mainline Contribution
- Clean commit with good message
- Add documentation
- Create PR to renode/renode-infrastructure
- Reference: [Renode Contributing Guide](https://github.com/renode/renode/blob/master/CONTRIBUTING.md)

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

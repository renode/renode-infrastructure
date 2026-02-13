# ARM Semihosting Implementation for Renode

**Project:** ReefPilot Firmware Testing Infrastructure
**Date:** 2026-02-13
**Goal:** Add/improve ARM semihosting support to enable printf() output in Renode

---

## Objective

Enable ReefPilot firmware to run in Renode with FreeRTOS and semihosting-based printf() output for automated testing.

---

## Background

### Parent Project
- **Location:** `~/reefpilot/firmware/`
- **Requirements:** `~/reefpilot/firmware/docs/emulation_requirements.md`
- **Test firmware:** `~/reefpilot/firmware/.pio/build/renode_test/firmware.elf`

### The Problem
- ReefPilot firmware uses `--specs=rdimon.specs` (ARM semihosting)
- Firmware calls `printf()` → newlib's `_write()` → **SYS_WRITE (0x05)** semihosting operation
- Mainline Renode does NOT support ARM semihosting ([Issue #681](https://github.com/renode/renode/issues/681))
- QEMU supports semihosting but has incomplete STM32 peripherals

### Why HOPE Fork?
- HOPE (Draper Labs) fork has **partial** semihosting implementation
- Found in `src/Emulator/Cores/Arm/Arm.cs:164-197`
- Implements SYS_READC (0x07), SYS_WRITEC (0x03), SYS_WRITE0 (0x04)
- **Missing:** SYS_WRITE (0x05) - the operation printf() actually uses!

---

## Current Analysis

### What's Implemented (HOPE Fork)

**File:** `src/Emulator/Cores/Arm/Arm.cs`

```csharp
[Export]
private uint DoSemihosting()
{
    uint operation = R[0];  // ARM register r0 = operation code
    uint r1 = R[1];        // ARM register r1 = parameter pointer

    switch(operation)
    {
    case 7: // SYS_READC - Read single byte
        result = uart.SemihostingGetByte();
        break;
    case 3: // SYS_WRITEC - Write single character
    case 4: // SYS_WRITE0 - Write null-terminated string
        // Reads from memory, writes via uart.SemihostingWriteString(s)
        break;
    default:
        this.Log(LogLevel.Debug, "Unknown semihosting operation: 0x{0:X}", operation);
        break;
    }
}
```

**File:** `src/Emulator/Peripherals/Peripherals/UART/SemihostingUart.cs`
- Simple UART peripheral (90 lines)
- `SemihostingWriteString()` - triggers `CharReceived` event
- `SemihostingGetByte()` - reads from FIFO
- Renode displays output via LoggingUartAnalyzer

### What's Missing

**SYS_WRITE (0x05)** - Write N bytes to file descriptor

According to ARM semihosting spec:
- **r0 = 0x05** (SYS_WRITE operation)
- **r1 = pointer** to 3-word block:
  - word[0] = file handle (1 = stdout, 2 = stderr)
  - word[1] = pointer to data buffer
  - word[2] = number of bytes to write
- **Returns:** Number of bytes NOT written (0 = success)

---

## Implementation Plan

### Phase 1: Add SYS_WRITE to HOPE Fork (2-4 hours)

1. **Modify** `src/Emulator/Cores/Arm/Arm.cs:DoSemihosting()`

   Add case for operation 0x05:
   ```csharp
   case 5: // SYS_WRITE
       if(uart == null) break;
       var paramBlock = this.TranslateAddress(r1);
       var handle = this.Bus.ReadDoubleWord(paramBlock);       // word[0]
       var dataPtr = this.Bus.ReadDoubleWord(paramBlock + 4);  // word[1]
       var length = this.Bus.ReadDoubleWord(paramBlock + 8);   // word[2]

       // Read data from memory
       string s = "";
       var addr = this.TranslateAddress(dataPtr);
       for(uint i = 0; i < length; i++)
       {
           var c = this.Bus.ReadByte(addr++);
           s = s + Convert.ToChar(c);
       }
       uart.SemihostingWriteString(s);

       result = 0; // Success - all bytes written
       break;
   ```

2. **Test** with ReefPilot firmware
   - Run firmware.elf in HOPE Renode build
   - Verify printf() output appears

3. **Document** findings

### Phase 2: Build HOPE Fork (Status: TODO)

**Current blocker:** Need to figure out how to build Renode with modified infrastructure.

**Options:**
- A) Find HOPE's full Renode repo (if it exists)
- B) Build mainline Renode with HOPE infrastructure as submodule
- C) Manually integrate changes into mainline Renode

**Next:** Research Renode build process

### Phase 3: Contribute to Mainline Renode (8-16 hours)

1. **Port** implementation to mainline `renode/renode-infrastructure`
2. **Implement** full semihosting spec (all operations, not just SYS_WRITE)
3. **Add tests** for semihosting operations
4. **Submit PR** to Renode project
5. **Iterate** based on maintainer feedback

---

## ARM Semihosting Operations Reference

**Operations we need for printf():**
- 0x05: SYS_WRITE - Write to file/stdout (**MISSING - must add!**)
- 0x03: SYS_WRITEC - Write single character (**DONE** in HOPE)
- 0x04: SYS_WRITE0 - Write null-terminated string (**DONE** in HOPE)

**Nice to have (full spec):**
- 0x01: SYS_OPEN - Open file
- 0x02: SYS_CLOSE - Close file
- 0x06: SYS_READ - Read from file
- 0x07: SYS_READC - Read single character (**DONE** in HOPE)
- 0x09: SYS_ISTTY - Check if file is a terminal
- 0x0C: SYS_SEEK - Seek in file
- 0x10: SYS_FLEN - Get file length
- 0x18: SYS_EXIT - Exit (terminate emulation)

**Spec:** [ARM Semihosting Documentation](https://developer.arm.com/documentation/dui0471/m/semihosting/semihosting-operations)

---

## Related Issues

- [Renode #681](https://github.com/renode/renode/issues/681): Semihosting support request (open, no response)
- [Renode #699](https://github.com/renode/renode/issues/699): Rust semihosting UART access (open, no response)
- [Renode #516](https://github.com/renode/renode/issues/516): Related semihosting discussion

---

## Decision Log

**2026-02-13:** Chose HOPE fork over custom implementation
- Rationale: Code already exists, just missing one operation
- Alternative considered: Implement from scratch (too much work)
- Risk: HOPE fork may be outdated, build process unclear

---

## Current Status

- [x] Clone HOPE fork
- [x] Analyze semihosting implementation
- [x] Identify gap (SYS_WRITE 0x05 missing)
- [ ] Figure out build process
- [ ] Add SYS_WRITE implementation
- [ ] Test with ReefPilot firmware
- [ ] Contribute to mainline Renode

---

## Quick Reference

**Files to modify:**
- `src/Emulator/Cores/Arm/Arm.cs` - Add SYS_WRITE case
- `src/Emulator/Peripherals/Peripherals/UART/SemihostingUart.cs` - May need modifications

**Test firmware:**
- `~/reefpilot/firmware/.pio/build/renode_test/firmware.elf`

**Expected behavior:**
- Firmware calls printf() → SYS_WRITE 0x05 → SemihostingUart → Console output

---

**Last Updated:** 2026-02-13
**Next Action:** Research Renode build process

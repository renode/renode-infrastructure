# KVM-based emulation

This code uses [KVM](https://linux-kvm.org) to emulate the CPU.
It uses KVM API, documentation for it may be found on [here](https://docs.kernel.org/virt/kvm/api.html).

*DISCLAIMER:* Currently we don't support executing exact number of instructions.
Instead, we are trying to execute for time proportional to the number of instruction that have been issued.
Effectively, number of executed instructions will be changing from run to run.

## How it works

### `kvm_init`

The KVM-based emulation should be initialized with `cpu_init`.
It initializes KVM and CPU state.

### `kvm_map_range`

To map memory, use `kvm_map_range()`.
It will map memory specified by `pointer` of `size` on `address`.
`slot` variable also should be passed, it indicates which slot should be used.
To map new memory segment, use new slot number.
Reusing slot number allows for modifying existing mapping.

### Execution

Execution is started with calling one of two functions:

#### `kvm_execute`

KVM emulation is done by calling `ioctl(..., KVM_RUN, ...)` which won't return until additional action will be necessary (e.g. handling IO instruction).
The idea here is to emulate CPU and do necessary action in loop.
To limit emulation for specified time, we use interval timer (see `getitimer(2)`).
The timer will send `SIGALRM` signal after specified time.
Sending signal interrupts `ioctl`, thus allows for returning after some time limit.

#### `kvm_execute_single_step()`

It will run single instruction, by setting debug mode in KVM.

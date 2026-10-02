# Changelog

Notable changes to the Adamantium Engine packages, in the form of [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
All packages share one version.

## Unreleased

### Fixed

- `ErrorOutOfDeviceMemory` when several applications, or an application and its designer previews, ran at once on a GPU
  without Resizable BAR. The device-local host-visible window (about 214 MB on such cards) is shared by every process;
  when it is full, buffers that want it now take host-visible system memory instead of failing.

### Changed

- Memory blocks in a small heap are a 64th of it, at least 4 MB, instead of an 8th: a process takes about half as much of
  the shared window as before (a designer preview 40 MB instead of 80).
- Running out of memory in every type a buffer allows reports the size, the memory type and the heap.
- Every new memory block is logged at the Debug level with its type, heap and the heap's total in blocks.

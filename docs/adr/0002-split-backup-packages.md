# Split oversized backup packages into manifest-backed ZIP volumes

**Status:** accepted  
**Date:** 2026-11-04

Backups may exceed a cloud provider's comfortable single-object size, and a failed large upload is expensive to repeat. We will first create the complete ZIP package, split its bytes into valid ZIP volumes whose sidecar and embedded `manifest.json` files define order, sizes, and hashes, and delete the complete ZIP only after every volume is written and verified. The product does not download from cloud storage; users place all manually downloaded volumes in one directory, where the restore tool validates the unit and assembles the original ZIP before extracting it into a new timestamped directory.

## Considered Options

- Standard split ZIP (`.z01`, `.z02`, `.zip`): portable with compatible tools, but SharpZipLib 1.4.2 has no supported writer entry point and Windows Explorer does not reliably assemble these files.
- Raw byte chunks without ZIP wrappers: simplest to split, but a chunk cannot be inspected with normal ZIP tools.
- Independent file-boundary ZIPs: each volume can contain complete source files, but one source file larger than the limit can still create an oversized volume.

## Consequences

- The complete ZIP and all volumes coexist temporarily, so free local space must cover both.
- Every package must carry a stable backup ID and a manifest; generated volumes are managed as one backup unit for upload, restore, retention, and immediate deletion.
- Upload completion means all enabled data destinations have all volumes and the manifest, with remote size/checksum verification where the provider exposes it.


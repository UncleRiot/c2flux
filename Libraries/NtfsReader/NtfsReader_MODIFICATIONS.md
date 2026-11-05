# NtfsReader — Third-Party Copyright and Modification Notice

**Included in:** c² flux 1.4.3  
**Notice prepared:** 2026-11-04  
**Last updated:** 2026-11-05  
**Library license:** GNU Lesser General Public License, version 2.1 or later (LGPL-2.1-or-later)

## Original project and attribution

The `Libraries/NtfsReader/` directory contains source code derived from:

- **Modernized upstream:** `itsWindows11/NtfsReader` — https://github.com/itsWindows11/NtfsReader
- **Original NtfsReader .NET Library:** Danny Couture, **Copyright (C) 2008 Danny Couture** — https://sourceforge.net/projects/ntfsreader/
- The original library acknowledges that it is based on work by **Jeroen Kessels**, author of JkDefrag, and thanks him for that work. This attribution does not imply that he authored every part of NtfsReader.

The original copyright holders retain their respective rights. The `itsWindows11` project maintains a later, modernized fork. The copy in c² flux contains additional changes listed below.

**License text:** `Libraries/NtfsReader/LICENSE` (also supplied under `Licenses/itsWindows11.NtfsReader-lgpl2.1.txt` and `Ressources/Licenses/itsWindows11.NtfsReader-lgpl2.1.txt` in the reviewed source archive).

## Comparison reference

- **Historical comparison baseline:** `c2flux2_1.4.2_Final.zip` (the earlier c² flux 1.4.2 source archive).
- **Current documented application version:** c² flux 1.4.3. The later NTFS safety change is documented separately below; no new full upstream comparison for 1.4.3 is claimed.
- **Reference:** publicly available `itsWindows11/NtfsReader` source on branch `main`, inspected on 2026-11-04 (its project file identifies version `0.1.0`).
- **Scope:** substantive differences confirmed by comparing the included source with publicly accessible upstream files. This is **not** a byte-for-byte diff against a pinned Git commit and does not establish which person made each change or the date of each historical edit.
- **Date distinction:** 2026-11-04 is the **date of the upstream comparison and original notice**, **not** an assertion that the historical code modifications were made on that date. Individual historical modification dates were not provided in the archive. The subsequently applied security change is documented as of 2026-11-05.

## Confirmed changes relative to that upstream reference

### `Libraries/NtfsReader/RetrieveMode.cs` — modified

- Introduced the `LastWriteTimes = 8` retrieval flag to obtain modification timestamps without requesting the full set of `StandardInformations` metadata.

### `Libraries/NtfsReader/NtfsReader.cs` — modified

- Added `_lastWriteTimes` storage for compact per-node modification timestamps.
- Added retrieval of the timestamp from `AttributeStandardInformation.FileChangeTime` when `RetrieveMode.LastWriteTimes` is selected.
- Added allocation/preservation of the timestamp array while processing the MFT, including the first MFT record.
- Added `GetLastWriteTimeUtc(uint nodeIndex)`; a stored value of zero returns `DateTime.MinValue`.
- Added `_mftReadBufferSizeBytes` and support for a configurable sequential MFT read buffer; the synchronous path ensures the buffer is at least one MFT record and aligns its length to the MFT record size.

### `Libraries/NtfsReader/NtfsReader.Async.cs` — modified

- Extended `InitializeAsync` to accept a custom MFT buffer size and an optional `IProgress<double>` reporter.
- Added progress reporting during initialisation, MFT bitmap reading and MFT record scanning.
- Extended `ProcessBitmapDataAsync` and `ProcessMftAsync` to support progress reporting.
- Added handling of `RetrieveMode.LastWriteTimes` in the asynchronous MFT path.
- Added use of the configurable MFT read buffer in the asynchronous scanning path.
- Removed an explicit `GC.Collect()` call present in the inspected upstream asynchronous initialization path.

### `Libraries/NtfsReader/NtfsReader.Public.cs` — modified

- Added a three-argument synchronous constructor accepting `uint mftReadBufferSizeBytes`; the original two-argument constructor now delegates to it.
- Extended `CreateAsync` with optional `uint mftReadBufferSizeBytes` and `IProgress<double> progress` parameters.
- Added `GetNodesUnfiltered()` to return named entries from the loaded MFT without calculating full paths for prefix filtering.
- Removed an explicit `GC.Collect()` call present in the inspected upstream synchronous constructor.

### `Libraries/NtfsReader/Structures/NtfsNodeData.cs` — new in the reviewed copy

- Added `NtfsNodeData`, a public read-only value type carrying node index, parent index, attributes, name, size and `LastChangeTimeFileTimeUtc`.
- No corresponding source file was found at the matching path in the inspected upstream `main` branch.

### `Libraries/NtfsReader/NtfsReader.csproj` — modified

- Changed the target framework declaration from upstream's `netstandard2.0;net8.0;net9.0;net10.0` to `net10.0` only.
- The upstream package identifier and package version `0.1.0` are retained in the reviewed file. The application-local modifications should not be mistaken for an unmodified official `0.1.0` package release.

## Subsequent security change documented for c² flux 1.4.3

### `Libraries/NtfsReader/NtfsReader.cs` — modified on 2026-11-05

- Hardened MFT record buffer bounds checks in `ProcessMft()` before obtaining a record pointer.
- Used a bounds-checked `Span<byte>` over the rented array and a pinned record pointer for both `FixupRawMftdata()` and `ProcessMftRecord()`.
- Addressed two CodeQL findings (`cs/unvalidated-local-pointer-arithmetic`) previously reported at lines 1039 and 1048 of the earlier source.
- Following the fix and subsequent GitHub scan, both findings appeared as closed, with zero open code-scanning alerts for the `master` branch at the time of review.

This addition documents the specific subsequent change and the reported scan result; it does not assert a complete security audit or runtime regression test.

## Other source files

In the historical 1.4.2 comparison, the remaining files under `Libraries/NtfsReader/` retained the examined upstream types and APIs. Formatting, blank-line, or file-encoding differences have **not** been exhaustively classified. This notice intentionally does **not** claim that every other file is byte-for-byte identical to upstream.

## Licensing and distribution notes

1. NtfsReader remains subject to **LGPL-2.1-or-later**; the application's own GPL-3.0 licensing does not erase the library's original terms or copyright notices.
2. Keep this notice, the original upstream attribution and the complete LGPL license text with any published modified source distribution.
3. **Important:** LGPL 2.1 section 2(b) calls for prominent notices in **the changed source files themselves**, stating that the files were changed and the date of changes. This standalone notice is an additional record and **does not by itself replace file-level modification notices**. Actual historical modification dates and responsible authors must be established before adding definitive file-level statements; do not substitute the review date for an unknown change date. The additional 2026-11-05 security change has a known modification date for the purposes of this notice.
4. The distribution must also satisfy the LGPL conditions applicable to the way the library is combined with and distributed alongside the application; the presence of a license file alone does not establish full compliance.

## References

- Modernized upstream: https://github.com/itsWindows11/NtfsReader
- Original source and attribution: https://sourceforge.net/projects/ntfsreader/
- Original copyright header preserved in an archival mirror: https://github.com/wizzard0/ntfsreader-sf/blob/master/NtfsReader/System/IO/Filesystem/Ntfs/NtfsReader.cs
- LGPL 2.1: https://www.gnu.org/licenses/old-licenses/lgpl-2.1.en.html

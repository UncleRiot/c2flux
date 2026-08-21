## Changelog v1.3.5-beta.2

## Changelog v1.3.5-beta.2  * (Fix) Pinned SQLitePCLRaw to 2.1.13 to resolve the affected native SQLite dependency * (Change) Added and improved comments across multiple source files   **Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.3.5-beta.1...v1.3.5-beta.2

## Changelog v1.3.4

Again, a bigger fix-release...

## Changelog v1.3.4

Detailed Scan History
- (Fix) Detailed scan history now forces "Show files in tree"
- (Fix) "Show files in tree" is locked while detailed history is active
- (Fix) Added a hint for the required setting

C² Flux Scan
- (Fix) C² Flux scan now records file modification times
- (Fix) Date-based search filters now work correctly with C² Flux results
- (Fix) Modification time handling keeps existing scan performance

Directory Scan
- (Fix) Directory scans now detect FindNextFile errors
- (Fix) Normal scan completion is no longer treated as an error
- (Fix) Failed directories are reported through skipped-directory handling

Deep Directory Handling
- (Fix) Deep directory scans now use iterative traversal
- (Fix) Prevent stack overflows on deeply nested paths
- (Fix) C² Flux and MFT scan performance remains unchanged

Scan History Diagnostics
- (Fix) Removed misleading zero-value performance metrics
- (Change) Improved scan history diagnostics by removing unused AllFiles counters

Scan Path Filtering
- (Change) Disabled the unused ExcludedPaths setting
- (Change) Disabled inactive path exclusion logic in all scanners
- (Change) Disabled the unused ScanPathFilter implementation
- (Change) Disabled the unused NtQueryDirectoryScanner path filter
- (Change) Existing scan behavior and performance remain unchanged
- (Change) C² Flux progress path handling remains independent

Search Performance
- (Change) Improved search performance by avoiding duplicate file traversal
- (Change) Directories are traversed through Children
- (Change) Files are processed once through rootEntry.AllFiles
- (Change) Children traversal remains as fallback when AllFiles is empty
- (Change) Reduced stack operations, path allocations and duplicate HashSet lookups
- (Change) Existing search criteria and result behavior remain unchanged

Saving/Loading Scans
- (Fix) Shared file references are restored correctly when loading
- (Change) Improved .wtfscan serialization by preserving shared file references
- (Change) Avoided serializing the same file twice through AllFiles and Children
- (Change) Reduced .wtfscan file size with "Show files in tree" enabled
- (Change) Reduced save and load overhead for .wtfscan files
- (Change) Existing .wtfscan files remain loadable

**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.3.3...v1.3.4

## Changelog v1.3.3

## Changelog v1.3.3
- Bump Version 1.3.1 -> 1.3.3
- (Fix) Prevented recursive scan loops through reparse points when reparse point scanning is enabled
- (Fix) Corrected scan progress calculation for folder scans
- (Fix) Made scan cache replacement atomic to avoid cache loss during updates
- (Change) Added configurable NtQuery directory buffer sizes with 64 KiB as the default
- (Change) Reduced NtQuery scanner buffer memory usage while keeping scan performance high
- (Change) Removed the forced full garbage collection after MFT processing
- (Change) Added detailed verbose diagnostics for MFT support and scanner selection
- (Fix) Changed selection-only dropdowns in Settings, Search and Storage History to proper list mode


## Changelog v1.3.1
- (Fix) Scan History range changes now refresh the chart immediately


## Changelog v1.3.0

## ✨ What's new?

- Extensive overhaul and rearrangement of the settings dialogue
- Redesign of the behavior of the Scan History - former Storage history. 
<br>

File -> Settings -> History
- Activate "Save detailed scan history" to save scans in a SQLite DB
- The Database and help files resides here:
<br>
ScanHistory
<br><br>
  <img width="620" height="163" alt="grafik" src="https://github.com/user-attachments/assets/2732eb0a-a4e1-4f14-aaba-20841bbeb6a1" />

<br><br>

   The DB is highly compacted, but may get large over time. 
   Activate at least: "Auto-compact DB" so it get's shrinked, when necessary.
   Auto-purge let's you decide, how long you like to keep data!
<br>

  <img width="519" height="358" alt="grafik" src="https://github.com/user-attachments/assets/c6de461b-d752-449b-840e-8ec92c614cc6" />


## ⚠️ Breaking Changes v1.3.0

* Renamed Storage History UI terminology to Scan History
* Renamed storage_history.json to scan_history.json
* Renamed storage_history_details.db to scan_history_details.db
* Removed the deprecated “Save scan history” setting from the visible Settings UI
* Removed the legacy Compare scans entry from the toolbar and toolbar context menu
* Renamed Statistics settings tab to History
* Moved “Show partition panel” from General to UI settings


## Changes

- (Fix) Disabled the Drive dropdown while a scan is running
- (Change) Enabled “Show files in tree” by default
- (Change) Moved Language to the first position in General settings
- (Change) Moved “Show partition panel” to UI settings and aligned UI spacing
- (Change) Reordered Settings tabs to General, UI, History, Export, Logging
- (Change) Renamed Statistics to History
- (Change) Renamed Storage History to Scan History across the main UI and related labels
- (Change) Renamed Storage History details to “Save detailed scan history”
- (Change) Renamed Scan History storage files to scan_history.json and scan_history_details.db
- (Fix) Removed the deprecated “Save scan history” option from the Settings UI
- (Fix) Removed the legacy Compare scans button from the toolbar and context menu while keeping the legacy code path
- (Fix) Corrected active toolbar state when starting a scan from Scan History
- (Feature) Added Scan History database size and reusable database space indicators
- (Feature) Added configurable Auto-purge with 90-day and 20-snapshots-per-drive defaults
- (Feature) Added Auto-compact DB with a 30% reusable-space threshold
- (Fix) Disabled Scan History database controls when “Save detailed scan history” is disabled
- (Fix) Corrected History settings spacing and input alignment
- (Fix) Prevented stale 100% History-save progress callbacks from leaving the window title and progress state stuck
- (Change) Completed and synchronized all language files with the latest History and database settings keys
- (Feature) Added detailed Storage History persistence using SQLite.
- (Change) Replaced the previous storage_history_details.json / storage_history_details.json.br persistence with storage_history_details.db.
- (Change) Kept Storage History file snapshots as compact Brotli-compressed BLOBs inside SQLite to minimize database size.
- (Change) Added automatic migration of existing JSON and Brotli Storage History details into SQLite.
- (Change) Removed legacy Storage History detail files automatically after successful migration.
- (Change) Added compact snapshot serialization using sorted paths, common-prefix encoding, VarInt values, and Brotli compression.
- (Change) Added the C2S2 snapshot format while retaining backward compatibility with previously stored snapshot data.
- (Performance) Improved Storage History snapshot serialization by using buffered Brotli compression and block-based UTF-8 path encoding.
- (Performance) Changed Brotli compression from maximum-size optimization to CompressionLevel.Optimal for substantially faster Storage History writes.
- (Performance) Optimized Storage History change detection by replacing large dictionary-based comparisons with a sorted linear merge.
- (Performance) Reduced allocations and duplicate sorting while creating and serializing Storage History snapshots.
- (Performance) Added SQLite write settings optimized for the current compressed-BLOB storage model using WAL journaling and synchronous=NORMAL.
- (Change) Changed SQLite auto-vacuum handling from FULL to INCREMENTAL to avoid unnecessary work during commits.
- (Feature) Added real Storage History post-processing progress instead of simulated repeating progress.
- (Change) Added Saving History details to SQLite-DB xx% progress information to the main window title.
- (Change) Added Storage History post-processing progress to the main status progress bar.
- (Performance) Throttled Storage History UI progress updates to actual displayed percentage changes.
- (Fix) Restored the normal completed scan progress state after Storage History background processing finishes.
- (Fix) Improved Storage History intensity slider responsiveness after switching drives.
- (Fix) Prevented Storage History chart hover index mismatches after changing drives.
- (Change) Reset Storage History drive selector focus after rebinding records to prevent slider input from remaining captured by the selector.
- (Fix) Added protection against stale Storage History chart point arrays while records are being rebound.
- (Change) Added Storage History timing diagnostics used to identify snapshot creation, comparison, compression, SQLite write, and commit costs.
- (Performance) Confirmed through profiling that SQLite transaction and commit overhead is minimal compared with snapshot preparation and compression.
- (Change) Retained the compressed snapshot-BLOB architecture after testing and rejecting a relational per-file SQLite model because it significantly increased runtime and database size.

**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.2.51...v1.3.0

**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.2.51...v1.3.1

**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.3.0...v1.3.2

**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.3.2...v1.3.3

## Changelog v1.3.1

## Changelog v1.3.1
- (Fix) Scan History range changes now refresh the chart immediately


## Changelog v1.3.0

## ✨ What's new?

- Extensive overhaul and rearrangement of the settings dialogue
- Redesign of the behavior of the Scan History - former Storage history. 
<br>

File -> Settings -> History
- Activate "Save detailed scan history" to save scans in a SQLite DB
- The Database and help files resides here:
<br>
ScanHistory
<br><br>
  <img width="620" height="163" alt="grafik" src="https://github.com/user-attachments/assets/2732eb0a-a4e1-4f14-aaba-20841bbeb6a1" />

<br><br>

   The DB is highly compacted, but may get large over time. 
   Activate at least: "Auto-compact DB" so it get's shrinked, when necessary.
   Auto-purge let's you decide, how long you like to keep data!
<br>

  <img width="519" height="358" alt="grafik" src="https://github.com/user-attachments/assets/c6de461b-d752-449b-840e-8ec92c614cc6" />


## ⚠️ Breaking Changes v1.3.0

* Renamed Storage History UI terminology to Scan History
* Renamed storage_history.json to scan_history.json
* Renamed storage_history_details.db to scan_history_details.db
* Removed the deprecated “Save scan history” setting from the visible Settings UI
* Removed the legacy Compare scans entry from the toolbar and toolbar context menu
* Renamed Statistics settings tab to History
* Moved “Show partition panel” from General to UI settings


## Changes

- (Fix) Disabled the Drive dropdown while a scan is running
- (Change) Enabled “Show files in tree” by default
- (Change) Moved Language to the first position in General settings
- (Change) Moved “Show partition panel” to UI settings and aligned UI spacing
- (Change) Reordered Settings tabs to General, UI, History, Export, Logging
- (Change) Renamed Statistics to History
- (Change) Renamed Storage History to Scan History across the main UI and related labels
- (Change) Renamed Storage History details to “Save detailed scan history”
- (Change) Renamed Scan History storage files to scan_history.json and scan_history_details.db
- (Fix) Removed the deprecated “Save scan history” option from the Settings UI
- (Fix) Removed the legacy Compare scans button from the toolbar and context menu while keeping the legacy code path
- (Fix) Corrected active toolbar state when starting a scan from Scan History
- (Feature) Added Scan History database size and reusable database space indicators
- (Feature) Added configurable Auto-purge with 90-day and 20-snapshots-per-drive defaults
- (Feature) Added Auto-compact DB with a 30% reusable-space threshold
- (Fix) Disabled Scan History database controls when “Save detailed scan history” is disabled
- (Fix) Corrected History settings spacing and input alignment
- (Fix) Prevented stale 100% History-save progress callbacks from leaving the window title and progress state stuck
- (Change) Completed and synchronized all language files with the latest History and database settings keys
- (Feature) Added detailed Storage History persistence using SQLite.
- (Change) Replaced the previous storage_history_details.json / storage_history_details.json.br persistence with storage_history_details.db.
- (Change) Kept Storage History file snapshots as compact Brotli-compressed BLOBs inside SQLite to minimize database size.
- (Change) Added automatic migration of existing JSON and Brotli Storage History details into SQLite.
- (Change) Removed legacy Storage History detail files automatically after successful migration.
- (Change) Added compact snapshot serialization using sorted paths, common-prefix encoding, VarInt values, and Brotli compression.
- (Change) Added the C2S2 snapshot format while retaining backward compatibility with previously stored snapshot data.
- (Performance) Improved Storage History snapshot serialization by using buffered Brotli compression and block-based UTF-8 path encoding.
- (Performance) Changed Brotli compression from maximum-size optimization to CompressionLevel.Optimal for substantially faster Storage History writes.
- (Performance) Optimized Storage History change detection by replacing large dictionary-based comparisons with a sorted linear merge.
- (Performance) Reduced allocations and duplicate sorting while creating and serializing Storage History snapshots.
- (Performance) Added SQLite write settings optimized for the current compressed-BLOB storage model using WAL journaling and synchronous=NORMAL.
- (Change) Changed SQLite auto-vacuum handling from FULL to INCREMENTAL to avoid unnecessary work during commits.
- (Feature) Added real Storage History post-processing progress instead of simulated repeating progress.
- (Change) Added Saving History details to SQLite-DB xx% progress information to the main window title.
- (Change) Added Storage History post-processing progress to the main status progress bar.
- (Performance) Throttled Storage History UI progress updates to actual displayed percentage changes.
- (Fix) Restored the normal completed scan progress state after Storage History background processing finishes.
- (Fix) Improved Storage History intensity slider responsiveness after switching drives.
- (Fix) Prevented Storage History chart hover index mismatches after changing drives.
- (Change) Reset Storage History drive selector focus after rebinding records to prevent slider input from remaining captured by the selector.
- (Fix) Added protection against stale Storage History chart point arrays while records are being rebound.
- (Change) Added Storage History timing diagnostics used to identify snapshot creation, comparison, compression, SQLite write, and commit costs.
- (Performance) Confirmed through profiling that SQLite transaction and commit overhead is minimal compared with snapshot preparation and compression.
- (Change) Retained the compressed snapshot-BLOB architecture after testing and rejecting a relational per-file SQLite model because it significantly increased runtime and database size.

**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.2.51...v1.3.0

**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.2.51...v1.3.1

## Changelog v1.2.82-beta.5

## Changelog v1.2.82-beta.5
- (Fix) Storage History intensity slider responsiveness after switching drives
- (Fix) Storage History chart hover index mismatch after changing drives
- (Change) Reset Storage History drive selector focus after rebinding records



## Changelog v1.2.81-beta.4
- (Fix) Improved custom file dialog table rendering with vertical column separators only
- (Fix) Reduced file dialog list flicker with double-buffered rendering
- (Fix) Removed unnecessary horizontal row separators
- (Fix) Corrected list column sizing to avoid unnecessary horizontal scrolling
- (Fix) Filled the native ListView header remainder area to remove the gray block beside the last column



## Changelog v1.2.81-beta.3

* (Feature) Added AntdUI-themed Open, Save, Export, and folder selection dialogs
* (Change) Centralized file dialog styling, sizing, layout, and button visuals in `AntdThemeService`
* (Change) Added Windows shell icons for drives, folders, and files
* (Fix) Corrected file dialog parenting, navigation pane, footer layout, and resize behavior
* (Fix) Resolved shell icon lifetime crash during TreeView handle creation
* (Fix) Removed unnecessary file name field from folder selection dialogs
* (Fix) Removed unwanted dark button background areas in file dialog footers
* (Change) Added localized file dialog labels and overwrite prompts across all language files


**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.2.80-beta.2...v1.2.81-beta.3

**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.2.80-beta.2...v1.2.81-beta.4

**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.2.80-beta.2...v1.2.82-beta.5

## Changelog v1.2.80-beta.2

## Changelog v1.2.80-beta.2

- (Fix) Single-click activation for inactive app windows, menus, and toolbars
- (Fix) Settings dialog OK action no longer causes toolbar layout flicker
- (Fix) About dialog OK button aligned with the central AntdUI button style
- (Fix) Storage History buttons aligned to the common app button height and vertical layout
- (Fix) Storage History table header styling aligned with the central table theme
- (Fix) Storage History chart intensity at 0% now matches the app background and fades colors in progressively
- (Feature) Storage History details help tooltip now includes a localized explanation and embedded preview image
- (Change) Storage History details help text updated across all language files
- (Fix) Sunburst color families, gradients, hierarchy mapping, and label rendering aligned with Treemap visuals
- (Change) Sunburst labels are now horizontal and displayed without background bars
- (Change) Bar and Pie charts now reuse the central chart color palette
- (Change) Bar and Pie charts now use the same centralized gradient factors as Treemap
- (Fix) Pie chart gradients are calculated per segment instead of across the full chart area
- (Change) Shared chart colors, shades, and gradient rules centralized in AntdThemeService


**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.2.51...v1.2.80-beta.2

## Changelog v1.2.79-beta.1

## Changelog

- (Feature) Added Storage History range presets, calendar selection, and persistent settings
- (Feature) Added right-click deletion for Storage History chart points and table rows
- (Feature) Added Storage History Details with top 10 added/removed file changes per history point
- (Feature) Added right-click "Details" access for Storage History table rows and chart points
- (Feature) Added optional Storage History Details snapshot capture with MFT-based detection and non-admin fallback scanning
- (Feature) Added visible Storage History post-processing phase with separate progress indication and total elapsed time
- (Fix) Fixed Storage History dropdown focus, initial loading, hover hints, automatic refresh, and record deletion refresh behavior
- (Fix) Improved real-time Storage History intensity updates
- (Fix) Storage History Details now compares against the latest available detail snapshot to avoid broken change chains
- (Fix) Storage History Details change signs now follow Free Space / Used Space display semantics
- (Fix) Suppressed misleading MFT snapshot errors when the fallback snapshot succeeds
- (Fix) Added sorting and consistent resizable columns to the Storage History Details table
- (Fix) Corrected early startup theme initialization so initial dialogs use the configured dark theme
- (Fix) Corrected active Export button text color after scans
- (Change) Improved Storage History layout, spacing, widths, and control order
- (Change) Moved "Storage History details" to the primary Statistics position and marked "Save scan history" as deprecated
- (Change) Updated Storage History settings help text and aligned help icons to the central UI spacing
- (Change) Standardized Storage History and related dialogs against the central AntdUI / AntdThemeService styling
- (Change) Switched publishing to framework-dependent single-file output without bundling the .NET runtime

**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.2.38...v1.2.79-beta.1

## Changelog v1.2.51

## Changelog
... comming soon

-  (Feature) Added Storage History range presets, calendar selection, and persistent settings
- (Feature) Added right-click deletion for chart points and table rows
- (Fix) Fixed Storage History dropdown focus, initial loading, hover hints, and delete refresh
- (Change) Improved Storage History layout, spacing, widths, and control order
- (Fix) Improved real-time intensity updates
- (Change) Enabled self-contained single-file publishing



**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.2.38...v1.2.51

## Changelog v1.2.38

## Changelog v1.2.38

**Major speed improvements**:
- (Feature) Added c²flux Scan for faster NTFS scans with admin rights
- (Change) Reduced scan processing and tree build overhead
- (Change) Improved MFT node handling and parent mapping
- (Change) Added lazy file and directory path creation
- (Change) Moved partition refresh work out of the critical scan-completion path
- (Change) Reduced scan-result UI transition time significantly
- (Change) Added optimized local NtfsReader integration and larger MFT read buffering
- (Feature) Added c²flux Scan setting and localized help text for all supported languages
- (Fix) Kept the finished progress indicator visible after deferred UI refreshes
- (Fix) Removed unnecessary forced garbage collection after MFT reads

  <img width="1788" height="1120" alt="grafik" src="https://github.com/user-attachments/assets/eb2d5c65-8b18-4796-a05e-c53d9bf0a472" />


**Full Changelog**: https://github.com/UncleRiot/c2flux/compare/v1.1.9...v1.2.38

## Changelog v1.1.11-beta.1

## Changelog v1.1.11-beta.1

- (Fix) Native Storage History delete confirmation (light theme) -> AntdUI-themed dialog
- (Fix) Aligned Storage History, Search, and Scan History button hover behavior -> AntdUI-themed 
- (Change) Reused the central AntdThemeService button styling to avoid redundant visual configuration
- (Change) Aligned Scan History tabs with the central Analysis tab styling

## Changelog v1.1.10-beta.1

## Changelog v1.1.10-beta.1

* (Feature) Remove individual scan roots from the Tree Pane via right-click; removed roots stay hidden during scan updates
* (Change) Context menu now shows the currently selected scan root in the remove action
   <img width="353" height="159" alt="grafik" src="https://github.com/user-attachments/assets/f1858078-e032-4a25-81a5-8460357d3ce7" />
* (Change) Language files updated with missing translations plus general overhaul

## Changelog v1.1.9

## Changelog v1.1.9

- (Change) Integrated `Analysis_ResponsiveTableGrid` into `AdvancedFeaturesForm`.
- (Fix) Ensured truncated table text remains visible instead of disappearing.
- (Change) Centralized table text rendering behavior for consistent column display.
- (Change) Aligned the Table view column order with the File types view.
- (Feature) Added `Size (MB)` to the Table view.
- (Change) Standardized `Usage`, `Size (GB)` and `Size (MB)` formatting across table views.

## Changelog v1.1.8


- ✨(Feature) Treemap view

   <img width="1475" height="936" alt="grafik" src="https://github.com/user-attachments/assets/1eb4efad-8483-4036-86ca-20fd87e7018a" />


<br>


- ✨(Feature, v1.0.3) Toolbar button visibility customization via context menu

    <img width="1234" height="479" alt="grafik" src="https://github.com/user-attachments/assets/b3d72d02-2f33-4898-9325-baf5d4f3b735" />


- (Fix) Restored saved scan child entries when loading scan result files.
- (Change) Reworked Treemap rendering for a flatter WinDirStat-style block layout.
- (Change) Added clearer block separation with subtle gradients and borders.
- (Change) Improved Treemap label backgrounds, alignment, and readability.
- (Change) Prioritized size labels when space is limited.
- (Change) Suppressed Treemap labels below 100 MB.
- (Change) Improved large directory labeling while preventing text overlap.
- (Fix) Added parent-folder context menu handling for aggregated `Other` blocks.
- (Change) Lightened aggregated `Other` blocks for better visual consistency.

## Changelog v1.1.8

## Changelog v1.1.8


- ✨(Feature) Treemap view

   <img width="1475" height="936" alt="grafik" src="https://github.com/user-attachments/assets/02029d53-caa9-4442-a2b8-9f87e107daa3" />

<br>


- ✨(Feature, v1.0.3) Toolbar button visibility customization via context menu

    <img width="1234" height="479" alt="grafik" src="https://github.com/user-attachments/assets/b3d72d02-2f33-4898-9325-baf5d4f3b735" />


- (Fix) Restored saved scan child entries when loading scan result files.
- (Change) Reworked Treemap rendering for a flatter WinDirStat-style block layout.
- (Change) Added clearer block separation with subtle gradients and borders.
- (Change) Improved Treemap label backgrounds, alignment, and readability.
- (Change) Prioritized size labels when space is limited.
- (Change) Suppressed Treemap labels below 100 MB.
- (Change) Improved large directory labeling while preventing text overlap.
- (Fix) Added parent-folder context menu handling for aggregated `Other` blocks.
- (Change) Lightened aggregated `Other` blocks for better visual consistency.

## Changelog v1.1.7

## Changelog v1.1.7


- ✨(Feature) Treemap view

   <img width="1475" height="936" alt="grafik" src="https://github.com/user-attachments/assets/02029d53-caa9-4442-a2b8-9f87e107daa3" />

<br>


- ✨(Feature, v1.0.3) Toolbar button visibility customization via context menu

    <img width="1234" height="479" alt="grafik" src="https://github.com/user-attachments/assets/b3d72d02-2f33-4898-9325-baf5d4f3b735" />


- (Feature) Added a Treemap view with hierarchical squarified layout and color-separated main groups
- (Fix) Improved file and folder selection synchronization between the Treemap and TreePane
- (Fix) Optimized Treemap rendering during window resizing and reduced unnecessary recalculations
- (Fix) Improved Treemap render, layout, and bitmap caching to reduce CPU usage and resize stutter
- (Fix) Prevented race conditions in `NtQueryDirectoryScanner` by keeping directory information class and filename 
   offset in a consistent atomic state
- (Fix) Added controlled exception handling for scan failures to prevent unhandled UI-thread crashes
- (Fix) Added error handling for scan result file loading and saving, including invalid JSON, missing files, permission issues, and I/O failures
- (Fix) Reworked the GitHub update check to avoid unsafe `async void` behavior and handle unexpected UI-side exceptions safely
- (Fix) Added missing `Toolbar.Treemap` and `Toolbar.TreemapButton` translations to all external language files
- (Fix) Added diagnostic logging for previously swallowed exceptions in settings migration, scan cache handling, storage history handling, 
   and storage history settings
- (Fix) Added safe fallback logging for `AppAlertLog` failures without introducing recursive logging or changing existing fallback behavior

## Changelog v1.0.4

## Changelog

Still new (since v1.0.3)
- ✨(Feature) Toolbar button visibility customization via context menu

    <img width="1234" height="479" alt="grafik" src="https://github.com/user-attachments/assets/b3d72d02-2f33-4898-9325-baf5d4f3b735" />

- (Fix) Corrected button hover colors
- (Fix) Compare Scans button behaviour
- (Change) Refined status bar progress positioning and fine-tuned progress bar positioning

## Changelog v1.0.3

## Changelog

- ✨(Feature) Toolbar button visibility customization via context menu

    <img width="1234" height="479" alt="grafik" src="https://github.com/user-attachments/assets/b3d72d02-2f33-4898-9325-baf5d4f3b735" />

- (Fix) Improved drive pane status, sizing, alignment and layout persistence
- (Fix) Status-Bar now displays consistent information
- (Change) Updated language files with missing toolbar and settings keys

## Changelog v1.0.2

## Changelog

- (Fix) Improve 200% DPI scaling across dialogs and controls
- (Fix) Correct Settings window scaling at high DPI
- (Fix) Correct About dialog layout at high DPI
- (Fix) Correct elevation prompt layout at high DPI
- (Fix) Correct Search input/control scaling at high DPI
- (Fix) Correct Storage History layout and chart scaling at high DPI
- (Fix) Correct main drive selector height at high DPI
- (Fix) Correct tree and partition pane scaling at high DPI
- (Fix) Restore full status text including drive/folder name
- (Fix) Improve status progress display at high DPI
- (Feature) Open in explorer added to Bar View

## Changelog v1.0.0

## Changelog

- (Fix) Prevent startup blocking from unresponsive mapped/network drives
- (Fix) Add 3-second timeout for drive availability checks
- (Fix) Log unavailable or timed-out drives as warnings
- (Fix) Show truncated Name values in Largest files
- (Fix) Show truncated Path values in Largest files
- (Fix) Show truncated Modified values in Largest files
- (Fix) Fix Largest files context menu on first right-click
- (Fix) Use dark title bar color for active windows in Dark Mode
- (Change) Dark mode now is default (light mode still not optimized)
- (Change) Add Usage column to Largest files
- (Change) Compact Usage column in Largest files
- (Change) Compact Size (GB) column in Largest files
- (Change) Compact Size (MB) column in Largest files
- (Change) Compact Modified column in Largest files
- (Change) Format Largest files Size (MB) without decimals
- (Change) Add MB suffix to Largest files Size (MB)
- (Change) Compact Usage column in File types
- (Change) Compact Size (GB) column in File types
- (Change) Compact Size (MB) column in File types
- (Change) Add GB suffix to File types Size (GB)
- (Change) Format File types Size (MB) without decimals
- (Change) Add MB suffix to File types Size (MB)
- (Change) Compact Usage column in Table view
- (Change) Style active Analysis tab with blue background and white text
- (Change) Scale Pie Chart dynamically with available window size
- (Change) Improve Sunburst color differentiation
- (Change) Disable Compare scans when scan history is inactive
- (Change) Gray out Compare scans text and icon when unavailable
- (Feature) Add Compare scans activation tooltip
- (Feature) Add Explorer context menu to Largest files
- (Feature) Add Explorer context menu to Table view
- (Feature) Add Explorer context menu to Pie Chart
- (Feature) Add Explorer context menu to Sunburst
- (Feature) Localize new context-menu and Compare scans texts

## Changelog v0.9.90-beta.1

## v0.9.90-beta.1

### Added

- Added a new Sunburst view for visualizing storage usage across multiple directory levels.
- Added a dedicated Sunburst toolbar button and View menu entry.
- Added configurable Sunburst depth and maximum item count.
- Added persistent view selection so the last selected view is restored on the next application start.
- Added Semantic Versioning support for update checks, including:
  - `alpha`
  - `beta`
  - `rc`
  - stable releases


### Improved

- Improved view button icons and removed obsolete duplicate symbol artifacts.
- Improved Sunburst layout spacing above the status bar.
- Improved update version parsing for tags such as:
  - `0.9.91-alpha.1`
  - `0.9.91-beta.1`
  - `0.9.91-rc.1`
  - `0.9.91`
- Improved version comparison according to Semantic Versioning precedence.


### Fixed

- Fixed duplicate colon output in the free-space status text.
- Fixed the About dialog showing a generic error when GitHub release versions contained valid prerelease suffixes.
- Fixed incorrect initial rendering artifacts in the Table, Pie Chart, and Bar Chart buttons.

### Notes

- Prerelease versions are ordered as follows:

  `alpha < beta < rc < stable`

- Example:

  `0.9.91-beta.1` is newer than `0.9.90`, but older than `0.9.91`.

## Changelog v0.9.83_repack

## Repack

For the people who like it smaler:
c2flux-v0.9.83-win-x64.zip
c2flux-v0.9.83-win-x64.zip.-.SHA256SUMS.txt

For those who like maximum transparency:
c2flux-v0.9.83_singlefile_plus_lang-win-x64.zip
c2flux-v0.9.83_singlefile_plus_lang-win-x64.zip.-.SHA256SUMS.txt

## Changelog v0.9.83

## Changelog

- (Fix) Added missing `Status.Scanning` localization.
- (Fix) Prevented crashes from invalid translation placeholders.
- (Fix) Added collision-safe backups for invalid settings files.
- (Fix) Made `settings.json` saving more reliable using a temporary file.

## Changelog v0.9.80

## Important

- (Fix) Prevented settings loss after invalid or unreadable `settings.json`.
- (Fix) Added safe handling for read-only and locked settings files.
- (Fix) Prevented storage history loss after invalid or unreadable JSON.
- (Fix) Added logging and themed warnings for settings and history errors.
- (Fix) Added timeout and detailed error handling for GitHub update checks.
- (Fix) Corrected live language switching in the main toolbar and Storage History.
- (Fix) Centralized previously hard-coded UI texts.
- (Fix) Corrected missing German translations in Scan History and Analysis views.
- (Fix) Updated all external language files with the new localization keys.
- (Fix) Corrected Storage History layout, spacing and control alignment.
- (Fix) Removed the redundant “Files” toolbar checkbox.

## Less important

- (Fix) Sorted language names alphabetically.
- (Fix) Removed unnecessary ellipses from “Compare scans”.
- (Fix) Corrected German labels such as “Kuchendiagramm”, “Suche” and “Historie löschen”.
- (Fix) Improved localized dropdown widths for longer translations.
- (Fix) Corrected elevation dialog localization and text wrapping.
- (Fix) Improved vertical alignment of Storage History controls.
- (Change) Standardized English as the fallback language.
- (Change) Added timestamped backups for invalid configuration files.

## Changelog v0.9.55

## Changelog

### Fixed (bugs)

- Restored the normal Windows Explorer double-click behavior for drives
- Prevented c² flux context-menu verbs from becoming the default drive action
- Restored the original drive shell verb when available
- Added fallback to the standard `open` verb when an invalid custom default is detected

## Changelog v0.9.54

⚠️ Downloads unavailable

## Changes

- Small fixes
- Cleaning rubbish

## Changelog v0.9.53

⚠️ Downloads unavailable

## Changes

- Added automatic update checks with direct download link
- Added built-in language support with external `Languages` folder
- Improved scan history, search and comparison features
- Added Windows Explorer context menu integration
- Fixed status bar, layout and publish-related issues

## Changelog v0.9.4

⚠️ Downloads unavailable


# Changelog

> This project is a refactored and independently developed continuation of **WTF – Where's the Filespace**, based on the original application from a separate repository.
>
> The release includes a **complete UI overhaul** with a redesigned interface, updated controls, improved scaling, and a new visual identity.

## [0.9.43]

### Added

- Global search for files and folders.
- Search in current scans, saved SQLite scans, and selected drives.
- Filters for size, modification date, file type, path, and entry type.
- Sorting, cancellation, progress display, and Explorer actions for search results.
- Optional Windows Explorer context-menu entry for searches.
- Persistent search settings, window position, column widths, and sorting.
- Centralized application metadata and branding.

### Changed

- Refactored the original **WTF – Where's the Filespace** application into a separate project and repository.
- Renamed the application to **c² flux – Tree Scanner**.
- Completed a full UI overhaul across the entire application.
- Replaced LucidUI with AntdUI.
- Redesigned the main window, settings, scan history, storage history, analysis views, dialogs, tables, tabs, and controls.
- Improved light, dark, and Windows-default theme support.
- Updated Windows Explorer integration for scan and search commands.
- Updated application icons, executable name, namespaces, solution files, and project files.
- Updated the project version from `0.9.1` to `0.9.4`.
- Updated the target framework configuration for Windows 7 compatibility.
- Improved single-file publishing by including native libraries during extraction.
- Integrated German and English translations into the localization system.
- Expanded localized texts for search, settings, dialogs, menus, and history views.
- Simplified the repository README.

### Improved

- Improved high-DPI behavior and responsive window layouts.
- Improved scan history comparison.
- Improved storage history.
- Improved shell context-menu registration and cleanup of legacy entries.
- Improved startup argument handling for direct scans and searches.
- Improved consistency across dialogs, buttons, selectors, date controls, and data grids.
- Improved persistence of layout, appearance, shell integration, and search preferences.

### Removed

- Removed the previous WTF branding and associated icons.
- Removed LucidUI and the obsolete Lucid theme service.
- Removed the previous form styling implementation.
- Removed bundled promotional images and screenshots.
- Removed static language JSON files in favor of managed language files.

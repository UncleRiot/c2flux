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

- ## Changelog v1.1.7


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

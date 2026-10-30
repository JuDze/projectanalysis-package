# Project Analysis

Project Analysis is an Editor package for examining imported textures, models and audio. Its current catalogue contains 13 rules across three resource categories. Findings describe review conditions; they do not measure overall game performance.

## Setup

In Unity Package Manager, use **Install package from git URL** with `https://github.com/JuDze/projectanalysis-package.git`. For a local copy, use **Install package from disk** and select this package's `package.json`. Open **Tools > Project Analysis**. The package targets Unity 6000.3.10f1. Unity Test Framework is not required.

## Workflow

1. In **Analysis**, choose **All project assets** or a folder under `Assets` or `Packages`. Press **Add** to inspect the resolved resource list; Remove excludes individual resources from that scope.
2. Open **Rules**, expand Textures, Models or Audio, change settings and press **Save**. Cancel discards unsaved changes; Reset to defaults changes the draft until Save is pressed.
3. Press **Run Analysis**. Search and category/risk filters affect the displayed findings. Separate findings for the same resource remain separate rows.
4. Select a finding to review its explanation, measurements, recommendation, proposed changes and warnings. The row **Fix** button and **Fix selected** require confirmation before changing import settings.
5. Fixed rows remain visible with dimmed text. Use the row **Undo** action or **Revert all fixes** to restore changes from the current analysis session. A new Run Analysis starts a new session.
6. **Create report** captures the original analysis and opens **Reports**. Reports with active fixes have type **Analysis and fixes**; otherwise the type is **Analysis**. Save Report exports the selected snapshot as JSON. Delete Report removes the selected snapshot after confirmation.

## Current rules

| Rule | Condition and default thresholds | Risk and proposed change |
|---|---|---|
| TEX001 | Largest imported dimension; Medium 4096 px, High 8192 px | Report only above Medium; propose the next smaller supported Max Size based on the imported dimension |
| TEX002 | Read/Write enabled; CPU data Medium 8 MiB, High 32 MiB | Low/Medium/High when size is known; propose Off |
| TEX003 | Sprite (2D and UI) with mipmaps enabled | Low; propose disabling mipmaps after reviewing sprite/UI use |
| TEX004 | Existing mipmaps and missing texture/global streaming; mip data Medium 8 MiB, High 32 MiB | Low/Medium/High when size is known; the global finding uses the highest related risk. Propose the texture flag only when global streaming is enabled |
| MOD001 | Mesh vertex count; Medium 10,000, High 40,000 | Low/Medium/High; recommendation |
| MOD002 | LOD0 triangle count; Medium 25,000, High 100,000 | Report only above Medium; recommendation |
| MOD003 | Model Read/Write enabled | Medium; propose Off |
| MOD004 | LOD0 triangles exceed MOD002 Medium; then review existing Mesh LODs and known LODGroups | Uses both MOD002 thresholds even when MOD002 is disabled; a model uses the highest candidate mesh risk. Propose Generate Mesh LODs only for supported geometry with at least 256 triangles when generation limits allow a simplified level and no known conflict exists. Maximum LOD and Discard Odd Levels remain unchanged; blocked or unknown limits receive a recommendation only |
| AUD001 | Vorbis/ADPCM clip in the load mode opposite to the selected goal | RAM goal uses both AUD005 thresholds even when AUD005 is disabled, and proposes Compressed In Memory. Playback CPU goal: ADPCM Low, Vorbis Medium; proposes Decompress On Load |
| AUD002 | Effective compression format is PCM | Low; propose the user's ADPCM or Vorbis choice; Vorbis quality is 0–1, initially 0.75 |
| AUD003 | Imported frequency; Medium 48,000 Hz, High 96,000 Hz | Report only above Medium; offer Optimize Sample Rate for supported PCM/ADPCM cases, otherwise recommend review |
| AUD004 | Multichannel clip, Force To Mono off, Ambisonic off | Low; offer Force To Mono after the user reviews suitability |
| AUD005 | Non-streaming clip with estimated runtime size; Medium 5 MiB (5,242,880 bytes), High 20 MiB (20,971,520 bytes) | Report only above Medium; propose Streaming |

Each numerical configuration has two independently editable positive thresholds: High must exceed Medium. Comparisons are strict: a value equal to Medium stays Low (or has no finding for exceedance-only rules); a value equal to High stays Medium. Binary rules have no thresholds. Shared thresholds remain editable and validated while a dependent rule needs them.

Findings have only Low, Medium or High risk. When a required measurement is unavailable, the numerical rule records an internal diagnostic and produces no finding or fix proposal. Unknown measurement values may remain as context for findings whose risk is already determined independently. AUD001 (RAM goal) and AUD005 use the Project Auditor 1.0.2 estimate for one playback instance; it is not a measured build RAM value or a savings guarantee. Risk expresses review priority and does not determine whether a fix is safe or available.

## Platform and scope

The build target and current Editor Quality Level are read from the project. The tool does not switch platforms or quality levels. Current import-based analysis uses Standalone settings; numerical measurements must match the active imported target. Platform labels can show Unknown when no valid build target is available.

The scope contains texture, model, mesh, prefab and audio asset files. Scene objects are not included. Prefab/model hierarchies supply LODGroup context for MOD004; scenes and runtime-created uses are not inspected. Non-editable importer settings are excluded from collection.

## Fixes and reports

Analysis never modifies assets. Fix plans are checked again before application, then approved settings are changed, assets are reimported and findings are rechecked. Importer snapshots retain values needed for restoration, including settings that are not themselves analysed by a rule. Undo operates on the affected resource and can restore several rule changes to that resource.

Reports show findings and recorded changes, without general collection diagnostics. Opening and exporting a report excludes findings without one of the three supported risks; Summary and the saved JSON use the same filtered findings. Exporting does not modify the source snapshot or fix/Undo records. A recheck marked Results available may have partial results; unavailable comparison counts are left empty. Counts from different operations are not summed.

Rule settings are stored under the project's `UserSettings/ProjectAnalysis` directory. The current analysis's fix state is stored separately in `UserSettings/ProjectAnalysis/FixState/Session.json`: original and applied values, operation status, Undo links and recheck results. This state survives window and script reloads. A new Run Analysis replaces it with an empty session; earlier asset changes remain applied. Previously created reports retain their own snapshots and are not modified by later fixes, Undo or a new run. Invalid rule-settings files load defaults; the original file is preserved and backed up when the user saves a replacement. The current settings-file version is 4; report schema version is separately 1.

## Web Connection

The server URL defaults to `https://unityqawebsystem-production-5r5dcg.laravel.cloud` and remains editable. Enter the **Web Project UUID** generated in the web application. Save stores the address and UUID locally for this Unity project. A non-empty UUID must use the standard hyphenated format; this validates its format, not its existence on the server. Cancel restores the saved values. Reset to defaults restores the default URL and clears the UUID and token in the form until Save is pressed. Press Save to keep the token in SessionState for the current Unity session. It survives script reloads and reopening the window, and clears when Unity exits. A changed server address clears the token input. Tokens are excluded from settings files and report JSON.

**Test connection** uses the current Server URL, Web Project UUID and Access token without saving the form. It sends `GET /api/projects/{uuid}/connection` with a Bearer token. A successful check requires HTTP 200, a matching project UUID, `connected: true` and `permissions.submitReports: true`. The form displays **Connected** or a specific error, such as an invalid token, missing project access, password change requirement or rate limit.

While checking, the button becomes **Cancel test**. The request times out after 20 seconds, and closing or rebuilding the window cancels it. Editing connection values invalidates the previous result. Redirects are not followed, and server response text and tokens are not included in error messages. **Save** still controls local settings and SessionState storage. Checking a connection does not upload a report.

`Editor/WebConnection` contains a small client contract, request/response validation and the UnityWebRequest implementation. The view depends on the client interface; networking is separate from analysis and report serialization.

## Send to Web

Select a report in **Reports** and press **Send to Web**. The upload uses the current values in **Web Connection**, including unsaved form values, and the selected report's existing Web JSON snapshot. Save the connection settings first if they should survive reopening the window. No new analysis is run and the JSON schema is not changed.

The client sends `POST /api/projects/{uuid}/reports` with `Authorization: Bearer ...` and one multipart file field named `report` (`ProjectAnalysis.json`, `application/json`, UTF-8 without BOM). The file contains the same JSON as the report preview and **Save Report** export. The 10 MiB limit applies to the JSON file bytes. Multipart formatting is created by [UnityWebRequest.Post](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Networking.UnityWebRequest.Post.html).

While sending, the button becomes **Cancel upload** and Save/Delete are disabled. Cancellation stays available when selection or list filters change. Closing or rebuilding the window, script reload and Editor exit cancel the request. Uploads time out after 60 seconds; redirects are not followed and responses are limited to 64 KiB. There are no automatic retries.

HTTP 201 with a matching `unityReportId`, a positive server `id` and `duplicate: false` confirms a new upload. HTTP 200 with the matching ID and `duplicate: true` means Web kept the existing report. The footer identifies the submitted report and shows the outcome. After a timeout or cancellation Web may already have saved the report; resending the same report ID is safe because the Web endpoint does not create a second record for that project/report pair. Other responses show specific errors without exposing response bodies or tokens.

`IWebReportUploadClient` and its Unity implementation handle transport separately from `ReportUploadController`, which coordinates the Reports view. The backend is responsible for storing the complete file in its configured disk/bucket and recording report metadata, settings and Summary in the database. Individual findings remain inside the file.

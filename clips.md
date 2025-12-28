# Clips Documentation

This document describes the technical implementation of how video clips are combined, sorted, and filtered in the Clips Menu.

## 1. Combination (Server-Side)

The backend (`ClipsService.cs`) is responsible for scanning the file system and aggregating individual video files into logical `Clip` objects.

### Scanning & Parsing
1.  The service scans the configured root directory for all `.mp4` files.
2.  A regular expression is applied to each filename to extract:
    *   **Type:** `RecentClips`, `SavedClips`, or `SentryClips`.
    *   **Timestamp:** Date and time of the recording.
    *   **Camera:** The angle (front, back, left_repeater, etc.).
    *   **Event Folder:** (For Saved/Sentry) The specific event directory name.

### Grouping Strategy

#### Saved & Sentry Clips
These are grouped by their `EventFolderName`. All video files residing in the same event specific subdirectory (e.g., `2023-01-01_12-00-00`) are combined into a single `Clip`.

#### Recent Clips
Recent clips do not have event folders. They are grouped based on timestamps:
1.  Files are sorted by date.
2.  The system iterates through the files and groups them into segments.
3.  **Stitching:** If the gap between the end of one segment and the start of the next video is less than **5 seconds**, they are considered part of the same continuous `Clip`.
4.  If the gap exceeds 5 seconds, a new `Clip` is started.

### Segments
Each `Clip` consists of an array of `ClipVideoSegment` objects. A segment represents a specific timespan and contains references to the video files for all available camera angles during that timespan.

## 2. Sorting (Server-Side)

After combination, the list of clips is sorted before being returned to the client.
*   **Order:** Descending by `StartDate` (Newest first).
*   **Logic:** `clips.OrderByDescending(c => c.StartDate)`

## 3. Filtering (Client-Side)

Filtering is performed in the browser (`Index.razor.cs`) using the `EventFilterValues` logic.

The filter checks are inclusive. A clip is displayed if it matches **any** of the enabled criteria.

### Logic Breakdown

#### Recent
*   Enabled if `Values.Recent` is true and clip type is `ClipType.Recent`.

#### Dashcam (Saved)
A clip is shown if **any** of the following are true:
1.  **Honk:** `Values.DashcamHonk` is true AND reason is `user_interaction_honk`.
2.  **Saved:** `Values.DashcamSaved` is true AND reason is `user_interaction_dashcam_panel_save` OR `user_interaction_dashcam_icon_tapped`.
3.  **Other:** `Values.DashcamOther` is true AND clip type is `ClipType.Saved`.
    *   **Implementation Note:** Since "Honk" and "Saved" events are also of type `ClipType.Saved`, enabling **Dashcam Other** will display *all* Saved clips. This effectively overrides the "Honk" or "Saved" toggle states (showing them even if disabled) as long as "Other" is enabled.

#### Sentry
A clip is shown if **any** of the following are true:
1.  **Object Detection:** `Values.SentryObjectDetection` is true AND reason is `sentry_aware_object_detection`.
2.  **Acceleration:** `Values.SentryAccelerationDetection` is true AND reason starts with `sentry_aware_accel_`.
3.  **Other:** `Values.SentryOther` is true AND clip type is `ClipType.Sentry`.
    *   **Implementation Note:** Similar to Dashcam, enabling **Sentry Other** will display *all* Sentry clips. This effectively overrides the specific detection toggles (showing them even if disabled) as long as "Other" is enabled.

## 4. Naming & Display (Client-Side)

Clips do not have a specific human-readable name in the backend. In the user interface (Clips Menu), they are identified and displayed by their timestamp.

*   **Display Logic:** `(Clip.Event.Timestamp ?? Clip.StartDate).ToString("yyyy-MM-dd HH:mm:ss")`
*   **Explanation:**
    *   If the clip has associated Event data (e.g., Sentry or Saved clips with `event.json`), the **Event Timestamp** is used.
    *   Otherwise (e.g., Recent clips or missing event data), the **Clip Start Date** is used.
    *   The format is always `YYYY-MM-DD HH:mm:ss`.

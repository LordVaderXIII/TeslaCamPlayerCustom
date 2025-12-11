# Palette's Journal

## 2025-02-18 - Missing ARIA Labels on Icon Buttons
**Learning:** The application relies heavily on `MudIconButton` for critical controls (playback, navigation) without `AriaLabel` attributes. While some have tooltips, this leaves screen reader users guessing the function of these buttons.
**Action:** Systematically audit all `MudIconButton` usage and ensure `AriaLabel` is set, preferably matching the tooltip text if present, or describing the action clearly.

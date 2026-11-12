# Project UI language requirement

User requirement (2026-11-12): headset UI follows the participant's selected **source language**, not the translation target.

- Mandarin source uses Simplified Chinese UI; English source uses English UI.
- New app-owned labels, menus, prompts and errors must provide both languages through Assets/Script/FinalProject/Core/UiText.cs or the existing explicit bilingual status helpers.
- Refresh existing labels/notices when source language changes. Preserve drafts, completion states and popup expiry.
- Never translate or replace participant-written agenda points, conversation records, source transcripts or translated captions as part of UI localization.
- Keep button letters, participant identifiers, protocol values and keyboard character keys stable.
- Use a CJK-capable font for Chinese UI and run the UI language integration tests for changes to language behavior.
- System-owned dialogs/keyboard language is controlled by Quest OS settings.

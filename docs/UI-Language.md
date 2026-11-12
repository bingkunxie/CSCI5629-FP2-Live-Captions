# UI language follows the speaker's source language

The headset application uses the selected **source** language for its interface:

| Selected direction | Interface |
|---|---|
| Mandarin to English | Simplified Chinese |
| English to Mandarin | English |

Press **Y** outside the tools/notebook view to change the source language. Inside that view Y first closes it, as shown in its controls.

The change refreshes the caption header and placeholders, recording and gateway errors, review labels, voice preset names, tools, notebook controls, meeting agenda editor/keyboard labels, reaction shortcuts and meeting notices. An open agenda draft and visible reaction toast refresh without replacing participant text or restarting the popup timer. UI fonts use the bundled CJK font.

Actual transcripts, translated captions, participant-written agenda points and notebook contents retain their own language. The application does not rewrite meeting content merely because its interface changed. Controller letters A/B/X/Y, participant identifiers and keyboard character keys remain literal input symbols. Quest system permission dialogs and its system keyboard follow headset OS settings.

## Development rule

New application-owned labels and fixed messages must include both English and Chinese text. Use Core/UiText.cs and the current source-language flag; never use the translation target to select UI language. Pass only application-owned labels/statuses to the catalog, never arbitrary participant text. Keep internal protocol values, object identifiers and log keys stable.

Tests cover both source directions, menus/hints, error messages, gesture statuses, existing meeting notices, popup lifetime and preservation of multilingual drafts.

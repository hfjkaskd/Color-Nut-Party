# Local startup welcome

The production local gameplay prefab now opens the recovered `TXRecordGuidePanel` after loading and before teaching whenever `IsCompleteRecordGuide` is false. Its original hierarchy, artwork, localized labels, particles, delayed Start button and close animation are reused. The resource path and currency country are serialized on `LocalGameplay.prefab` (US matches the supplied reference).

The pending/open welcome participates in the existing modal gate. Teaching is deferred so its world-input override cannot bypass the welcome. Start uses the existing standard Button and code-bound listener, rejects early/repeated clicks, marks the existing completion flag, then allows the close animation to finish. On the next frame local teaching/core initialization resumes and the normal local save API checkpoints completion. Reloading a completed player skips the welcome without clearing progress.

This binds the welcome to the current local mode; it does not bind the full original server-dependent initialization dispatcher or add cash/withdrawal services. Existing local gameplay tests explicitly start after welcome; `LocalWelcomePlayValidation.Run` owns the fresh-player startup coverage.

Validation uses a separate project copy with a distinct product name, preserving the open development project's PlayerPrefs. The new Play validation exercises fresh entry, early/repeated clicks, an actual GraphicRaycaster/Button click, blocked world input, teaching continuation, persisted relaunch, and a returning level-two player. The render is captured at `Library/welcome-startup.png` in the validation project.

2026-09-09: Unity 2022.3.62f3 Play run passed with `NUT_LOCAL_WELCOME_PASS`. The captured 570×1014 render was visually inspected: original character/banner/progress layout, currency particles and Start button are present with no missing or magenta assets. A copy is available in the development project at `Library/ValidationCaptures/welcome-startup.png`. This verifies Editor playback, not an Android/iOS build.

Related Play regressions also passed: `NUT_LOCAL_TEACHING_VIEW_PASS` (all four tutorial seeds and later-level input reset) and `NUT_LOCAL_TOOLS_PLAY_PASS` (undo, exchange, add, replay, inventory persistence and modal guards).

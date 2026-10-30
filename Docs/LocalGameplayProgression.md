# Authorized local gameplay progression

The user approved local level advancement on 2026-09-09. OriginalStartupFlow loads the native LocalGameplay prefab through OriginalMainLevelSettings.LocalGameplayPath. The visible label identifies local mode. No server response, cash, ad completion, withdrawal, or reward-stage document is synthesized.

## Runtime behavior

- The scene raises BoardReady after original reconstruction and nut initialization. Local mode enables input and saves there. It also refreshes the source subround dot strip through the local prefab variant; reward milestones remain skipped (LocalSubroundView.md). Editor and device use the same runtime.
- Original selection, encrypted level resources, camera ray input, transfer/cap/type effects, completion counters/delays and the four first-level tutorial seeds retain their existing implementations.
- OriginalSuccessFlow owns tutorial advancement and victory timing. Its external request boundary advances Level, resets LevelSeed and increments TodayPassLevelCount locally. As requested on 2026-10-09, every completed board, including intermediate subrounds, prepares its own normal cash/diamond settlement using the existing configured reward rules. The Claim Button grants the saved pending reward once and closes the restored success panel before opening the next board, without a second level increment or a separate group-end award. Original source routing remains documented separately; the local production policy intentionally differs.
- Pending progress is flushed with empty LevelInfo before settlement or tutorial reconstruction. Quitting on settlement reconstructs the next board on entry. Normal board and move-history saves retain the original snapshot/envelope. A live board is flushed on pause; transition checkpoints are already durable.
- Original deadlock/failure delay now reaches the restored FailPanel through its SDK-skipped LocalFailure prefab variant (see LocalNativeFailure.md). Its Button uses RestartAfterFailure and the original seed increment. Existing replay uses the original prefab/controller. Captured-board guards prevent delayed local callbacks from acting on replacement boards.
- Locked rod clicks bind original AddScrew with existing inventory and a serialized 12-tile maximum. Missing inventory displays an SDK-skipped notice; no advertisement completion or inventory grant is fabricated.

## Prefab and fidelity boundary

The saved local composition contains the local label and a notice card with a standard Button; success and failure now load variants of the restored source prefabs (see LocalNativeSuccess.md and LocalNativeFailure.md). The builder is an authoring tool; runtime loads the prefab, binds code listeners, updates data and toggles visibility. The notice card remains a local alternative. Success and failure retain source presentation while explicitly omitting SDK reward/claim controls; they are not claimed complete reward-panel reproductions. Core world objects remain native meshes/sprites/prefabs/animations. There is no reflection or Editor fallback.

## Verification

LocalGameplayPlayValidation opens the real default scene with backed-up preferences. Its bounded offline solver chooses moves and sends actual camera-ray clicks. It never substitutes production completion callbacks, enables input manually or finishes animations early. It covers four tutorial seeds, real levels 2/3, the next Button and duplicate protection, settlement resume into level 4, partial board/history resume, an explicit persisted deadlock fixture with native failure delay/retry, and the actual replay popup.

Verified on Unity 2022.3.62f3:

- `Library/local-gameplay-play-final.log`: NUT_LOCAL_GAMEPLAY_PLAY_PASS, including actual failure/retry and replay Buttons.
- `Library/local-gameplay-notice-play.log`: NUT_LOCAL_NOTICE_PLAY_PASS, actual locked-rod click, no-grant notice, following-frame input release, owned-inventory fixture through original unlock/cost/save.
- `Library/local-gameplay-content-validation.log`: final NUT_CONTENT_VALIDATION_PASS; 164 PASS markers, including 1476 resources and 1474 board payloads. These markers are not a full-game completion claim.
- Current `local-gameplay-success-current.png`, `local-gameplay-resumed-current.png` and `local-gameplay-failure-current.png` in Library were visually inspected after rendered frames. They show the actual native boards and local panels; no old screenshots were used.
- Preference backup restored after validation. Existing pool-reparent diagnostics on scene teardown and existing edit-mode tool-flight diagnostics remain outside this change.

Remaining original scope includes the reward-dependent main-panel/guide/peripheral lifecycle, complete tool-acquisition UI, country/AB GM routing and source-device visual comparison. Early-level Play and content regression do not prove those complete. SDK services stay skipped.

# M1 Combo Practice Ground

Scene: Assets/Scenes/ComboPracticeGround.unity
Dummy prefab: Assets/CombatKit/Content/Prefabs/PracticeDummy.prefab

Controls: LMB ground 5-combo; C then LMB upper/air 6-combo; Q/E skills; Ctrl dodge; F5 reset dummy and counters.

The dummy keeps EnemyDamageReaction and EnemyKnockbackMotion. EnemyStateMachine only dispatches reactions and the no-op DummyIdle state when PracticeDummy is present. Other enemies are unchanged. Enemy weapon hitboxes are disabled. Damage restores dummy health so training can continue indefinitely.

Validation: ground input simulation reached stages 1-5 (4 hits from 1.5m initial spacing). Upper/air simulation reached air stages 1-6, with 7 hits including upper, and both actors landed. Reaction returns to DummyIdle. No console errors.

M1 scene implementation is ready; animation-by-animation hit-window visual tuning remains a manual gate, especially ground combo spacing/misses. Existing combat hit windows were preserved.

Original Main scene remains open with unsaved changes preserved. Editor play-start scene is set to this training scene; no source scene was saved.

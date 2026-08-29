AILURONE Ground Bot Rework — Phase 0
Date: 2026-07-31

Changes:
1. Fixed Unity warnings caused by setting linear/angular velocity on kinematic Rigidbody instances during EnemyTarget destruction.
2. Added explicit Ground Bot projectile damage/death attribution support.
3. Added a player-attributed environmental kill window API (default 3 seconds).
4. Preserved the original EnemyTarget.TakeDamage(...) signature for PlayerWeapon compatibility.

New public APIs for later phases:
- EnemyTarget.TakeDamageFromGroundBotProjectile(...)
- EnemyTarget.RegisterPlayerEnvironmentKillCredit(float duration = 3f)
- EnemyTarget.ClearPlayerEnvironmentKillCredit()

This phase does not yet change Ground Bot projectile gameplay. Those APIs will be connected in the projectile phase.

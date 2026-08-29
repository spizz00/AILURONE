AILURONE Ground Bot Rework — Phase 1
日期：2026-07-31

本阶段目标：
1. 为 Ground Bot、Spike、Ophanim 建立共用的外部硬直与受控击退底层。
2. 硬直时中断当前攻击，不恢复旧进度。
3. 硬直期间关闭接触伤害。
4. 使用 Spike 原有 Stunned 白色闪烁参数作为三类敌人的统一全身硬直视觉。
5. 受控击退检测 Environment，不能穿墙，但允许离开平台。
6. 受控击退可登记 3 秒玩家环境击杀归因。

本阶段尚未修改 Ground Bot 子弹。
因此正常开枪不会触发新硬直或击退；先通过 Play Mode 调试入口验证公共底层。

Play Mode 测试方法：
1. 进入 Play Mode。
2. 在 Hierarchy 选择 Ground Bot、Spike 或 Ophanim 的根对象（带 EnemyTarget 的对象）。
3. Inspector 中会在运行时自动出现 EnemyControlEffectController。
4. 点击该组件右上角菜单，选择：
   - DEBUG/Apply Full Stun 0.6s
   - DEBUG/Apply Half Stun 0.3s
   - DEBUG/Apply Full Stun + 2m Knockback
   - DEBUG/Apply Half Stun + 1m Knockback

预期：
- 全身白色闪烁。
- AI 暂停，当前蓄力/瞄准/移动被中断。
- 硬直期间不造成接触伤害。
- 2m 击退约 0.18 秒，1m 击退约 0.12 秒。
- 遇到 Environment 墙体时停止在墙前，不穿模。
- 平台边缘不会阻止击退，可以被推出平台。
- 结束后敌人重新判断当前状态，而不是继续旧攻击进度。

Spike 额外测试：
- 让 Spike 正常冲锋撞墙。
- 原有撞墙硬直现在应由共用控制器接管。
- 仍保持约 1.4 秒白色硬直，冲锋被取消，结束后重新判断状态。

通过标准：
- Console 无红色报错。
- 不再出现 Phase 0 已修复的 Kinematic Rigidbody 速度警告。
- 三类敌人的原有基础行为在未触发调试效果时保持正常。

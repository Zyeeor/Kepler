# Tutorial × Death Relay / Soul Shrine 接口契约表 v1

> 交付对象：战斗程序（Death Relay / Soul Shrine 玩法实现方）
> 需求依据：`.vibe/doc/Canonical/Content/Tutorial_Delivery_Baseline_v1.0.md`（TUT-06 首次真实 Death Relay、TUT-07 首次真实 Soul/Shrine）
> 状态：教学侧（TutorialFactBus）已就绪，只等玩法侧接入。**玩法实现前请先对齐本契约，避免事后返工。**

## 一、背景与原则

- 教学侧采用"事实总线"模式：玩法侧**只需报告事实，无需了解教学细节**。
- 报告入口：`TutorialFactBus.Report(TutorialFact.XXX)`（静态方法，`Assets/Scripts/Tutorial/TutorialFactBus.cs`）。
- 报告幂等：教学侧对重复报告天然免疫（事实只做边沿处理），但请勿在同一次玩法事件中重复调用。
- Debug 模拟入口已就绪：Editor 菜单 `Kepler/Tutorial/模拟事实: 死亡接力(M3 预留)` 等，教学侧可先行开发测试。

## 二、事实契约

### 契约 1：DeathRelaySucceeded（死亡接力成功）

| 项 | 约定 |
|---|---|
| 枚举 | `TutorialFact.DeathRelaySucceeded` |
| 报告时机 | **接力完成的瞬间**：灵魂在当前 Possessed Body 死亡后，于"接力窗口"内成功附身到下一具合法尸体（附身成功、`OnPossessionStarted` 触发的那一帧内） |
| 判定责任 | **玩法侧**：接力窗口的起止、合法尸体集合、超时判定均由玩法侧负责；教学侧只认"成功"这一个事实 |
| 不报告的情况 | 窗口超时未附身（判定为普通死亡而非接力）、玩家主动脱离后附身（非死亡触发）、系统读档重置 |
| 报告位置建议 | 玩法侧在"接力附身完成"回调内调用（勿在窗口开始时报告） |

### 契约 2：SoulShrineRestored（灵魂神龛恢复）

| 项 | 约定 |
|---|---|
| 枚举 | `TutorialFact.SoulShrineRestored` |
| 报告时机 | **神龛恢复流程完成的瞬间**：灵魂进入神龛（Soul Shrine）→ 身体恢复完成的时刻 |
| 判定责任 | **玩法侧**：神龛交互范围、恢复流程、完成判定由玩法侧负责 |
| 不报告的情况 | 进入神龛但未完成恢复（中断/失败）、恢复流程被战斗打断 |

## 三、教学侧对这两个事实的处理（供参考，无需玩法侧关心）

- `TUT-06`（首次真实 Death Relay）：`startFacts = 空`（TUT-05 完成后激活），`completeFacts = [DeathRelaySucceeded]`；可跨多 Run 等待（延迟触发，不强制制造死亡）。
- `TUT-07`（首次真实 Soul/Shrine）：`startFacts = [DeathRelaySucceeded]`，`completeFacts = [SoulShrineRestored]`。
- 教学侧不主动制造死亡/神龛事件，全部由真实玩法触发。

## 四、待玩法侧回填的开放问题

1. **接力窗口时长**：死亡后灵魂有多少秒可附身下一具尸体？（教学侧 TUT-06 文案若需提示窗口时长，请提供数值）
2. **神龛是地图建筑还是波间状态？** 首次出现的位置/波次？（决定 TUT-07 是否需要"目标指向"提示）
3. **接力是否需要玩法侧额外标记**：如"接力中"UI 状态，教学 Banner 需要避让时请同步。

## 五、变更流程

- 事实枚举语义变更 → 先更新本契约表 → 再改 `TutorialFactBus.cs` 枚举注释（双方同步）。
- 新增事实（如"接力窗口超时"若教学需要）→ 在本表追加契约条目。

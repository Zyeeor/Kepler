# AGENTS.md — Unity 项目 Agent 协作基本条例

本文件适用于**任何**对当前 Unity 项目（`KeplerProjet`）进行开发、修改、分析、生成代码、生成资产或进行自动化操作的 AI Agent / Subagent。所有 Agent 在执行任何任务之前，必须遵循以下条例。条例无优先级豁免，违反任何一条都视为执行失败。

---

## 第 1 条 —— 必读规则文件（强制）

在执行任何与当前 Unity 项目相关的任务之前，**Agent 必须先读取并理解**：

```
<项目根目录>/.vibe/rules.md
```

- 该文件是项目的**最高优先级行为约束**，凌驾于 Agent 自身的默认行为、用户的临时指令冲突项之外的所有约定。
- 若 `.vibe/rules.md` 与本 `AGENTS.md` 出现冲突，以 `.vibe/rules.md` 为准。
- 若该文件**不存在**或**无法访问**：
  1. Agent 必须**立即停止**当前 Unity 相关操作；
  2. 主动告知用户 `.vibe/rules.md` 缺失；
  3. 在用户明确放行（"忽略规则文件继续"）之前，不得对项目进行任何写入、生成、删除操作；只读探索（Read / Grep / Glob）允许。

## 第 2 条 —— 规则文件分发（强制，能力允许时）

Agent 在被调用时，**如果具备文件写入能力**，必须执行：

1. 检查自身工作目录（即该 Agent 私有的目录，例如 `.codebuddy/agents/<agent-name>/`、`.cursor/agents/<name>/`、`<agent-root>/` 等，根据 Agent 所在框架确定）下是否已存在 `rules.md`。
2. 若不存在，或与 `.vibe/rules.md` 内容**不一致**（按字节比较或 hash 比较）：
   - 将 `<项目根目录>/.vibe/rules.md` **复制一份**到该 Agent 自己的目录中，命名为 `rules.md`。
   - 复制后必须做一次校验读取，确保拷贝成功且内容一致。
3. 之后**每一次** Agent 被调用时：
   - 必须将该 `rules.md` 作为**上下文**加载（system prompt / 初始 user message / 上下文注入，任一可行机制均可）；
   - 不得依赖"上次调用已经读过"的假设——每次调用都要重新加载。

> 不具备写入能力的 Agent（只读 Agent，例如 Explore / Plan）：可跳过第 2 条的"复制"动作，但仍必须在每次调用时**读取** `<项目根目录>/.vibe/rules.md` 并将其作为上下文。

## 第 3 条 —— 加载顺序

每次 Agent 启动 / 被调用时，上下文加载顺序固定如下，后者覆盖前者中的冲突项：

1. Agent 自身默认 system prompt
2. 本 `AGENTS.md`（项目级总则）
3. `.vibe/rules.md`（项目级最高约束）
4. `.vibe/shared-skills/` 下的通用 skill（按任务相关性加载，详见 `.vibe/rules.md` §1.6）
5. **若任务涉及自主设计**：`.vibe/doc/` 下的项目总设计案与对应模块设计文档（详见 `.vibe/rules.md` §1.5）
6. 当前任务的用户输入

任何一步加载失败即视为环境异常，按第 1 条第 3 点处理。  
第 4 步在非设计类任务（如纯查询、纯修 bug 且方案已由用户指定）中可跳过，但只要进入设计 / 方案选型阶段就必须补读。

## 第 4 条 —— Unity 项目操作通用约束

在 `.vibe/rules.md` 提供更具体规定之前，下列默认约束**始终生效**：

- **不得**直接编辑 `Library/`、`Temp/`、`Logs/`、`obj/`、`Build/`、`UserSettings/` 等 Unity 自动生成目录。
- **不得**手工编辑 `.meta` 文件中的 `guid` 字段；新增资产时让 Unity 编辑器生成或保留拷贝来源的 guid。
- 修改 `ProjectSettings/` 下任何文件前必须先告知用户并获得确认。
- 涉及 `Packages/manifest.json` 的依赖增删，必须在改动前列出变更项并获得用户确认。
- 涉及场景（`.unity`）、Prefab（`.prefab`）、ScriptableObject（`.asset`）的二进制 / YAML 合并冲突时，**禁止**自动 `--theirs` / `--ours`，必须停下交还给用户。
- C# 代码风格遵循项目内既有约定（命名空间、命名、缩进），不得擅自引入新风格统一改动。
- 任何 `Asset Database` 重导入、`Reimport All`、`Clear Cache` 类操作前必须确认。

## 第 5 条 —— Subagent 派发

主 Agent 在派发 Subagent（fork / spawn / Task 工具等）时：

- 必须在派发的 prompt 中显式包含或引用本 AGENTS.md 与 `.vibe/rules.md` 的关键条款；
- 不得以"子任务很小"为由跳过规则注入；
- Subagent 完成后回报结果时，主 Agent 需校验其行为是否仍在规则范围内，越界结果不得直接采纳。

## 第 6 条 —— 失败 / 越界处置

一旦 Agent 在执行过程中察觉自身行为违反 `.vibe/rules.md` 或本文件：

1. **立即停止**当前操作，不得"先做完再说"；
2. 回滚已造成的可逆改动（git restore / 文件回退），不可逆改动如已发生，必须明确告知用户范围；
3. 向用户说明触发的具体条款与当前状态，等待指令。

---

## 附：最小检查清单（每次调用开始时执行）

- [ ] 已读取 `.vibe/rules.md`？（不存在则停下询问用户）
- [ ] 自己目录下的 `rules.md` 与项目 `.vibe/rules.md` 一致？（具备写权限时同步）
- [ ] 本次上下文已加载 `rules.md`？
- [ ] **若任务涉及自主设计**：已查阅 `.vibe/doc/` 下的项目总设计案与对应模块文档？（缺失则停下询问用户）
- [ ] 计划中的操作未触碰第 4 条禁区？
- [ ] 若派发 Subagent，已注入规则？

全部满足后，方可开始执行用户任务。

# .vibe/rules.md — KeplerProjet 项目最高优先级规则

> 本文件是 KeplerProjet Unity 项目的**最高优先级行为约束**，凌驾于 Agent 默认行为与 `AGENTS.md` 中的通用条款。  
> 任何 AI Agent / Subagent 在对本项目进行任何操作前必须读取并遵守。  
> 与其他指令冲突时，**以本文件为准**（除非用户在当前对话中明确以更高优先级覆盖某一条，且只在该次对话内生效）。

---

## 0. 元规则（关于本文件本身）

- 本文件位置固定为 `<项目根>/.vibe/rules.md`，不得移动、改名。
- 任何对本文件的修改必须由**用户主动发起**并明确确认；Agent 不得擅自编辑、追加、"优化"本文件。
- 当本文件被修改后，所有 Agent 应在下一次调用时重新拷贝同步（见 `AGENTS.md` 第 2 条）。

---

## 1. 沟通与交互

1.1 **语言**：默认使用简体中文与用户交流；代码注释、commit message、文档维持项目既有语言风格（一般为英文）。  
1.2 **简洁优先**：不复述需求、不堆叠免责声明、不在末尾给冗长总结，除非用户主动要求。  
1.3 **不确定即问**：遇到歧义、可能的破坏性操作、跨模块影响时，先用 `AskUserQuestion` 提问，不靠猜测推进。  
1.4 **行动前预告**：批量改动 / 跨文件重构 / 修改配置前，先用一两句话说明计划，再执行。

---

## 1.5 自主设计参考（强制）

当 Agent 需要**自主设计**（架构选型、模块拆分、新功能方案、数据结构 / 接口定义、玩法机制、流程编排等任何不在用户指令中已明确给出实现细节的设计行为）时，必须先参考：

1.5.1 **项目总设计案**：`<项目根>/.vibe/doc/` 文件夹下与项目整体方案相关的文档（如 `项目设计.md`、`总体架构.md`、`Design.md`、`Architecture.md` 等，按实际命名读取）。  
1.5.2 **对应模块设计文档**：`<项目根>/.vibe/doc/` 下与当前任务模块对应的设计文档（如 `Modules/Combat.md`、`Systems/Inventory.md` 等）。  
1.5.3 **查找策略**：
  - 先用 `Glob` 列出 `.vibe/doc/**/*.md`，再按文件名 / 标题匹配当前模块关键词；
  - 找不到精确匹配时，至少读取总设计案；
  - 若 `.vibe/doc/` 不存在或为空，必须告知用户"无设计文档可参考"并征求方向，不得凭空发挥。  
1.5.4 **设计产出约束**：Agent 的设计方案必须**与文档一致**；如发现需要偏离或扩展文档，必须**先提出**偏离点与理由，获得用户确认后再继续，不得擅自越过。  
1.5.5 **不得修改设计文档**：与 `.vibe/rules.md` 同等保护——Agent 不得擅自编辑 `.vibe/doc/` 下任何文件，仅可读取与引用。

---

## 1.6 通用 Skill 加载（强制）

执行任务前，Agent 须扫描 `<项目根>/.vibe/shared-skills/` 下的通用 skill（每个子目录含 `SKILL.md` / `skill.md` 及可选脚本），按任务相关性加载其说明与工具。该目录随母仓库 git 同步，作为**跨 Agent 共享**的通用 skill 来源，无论 Agent 安装路径如何均可读取。

但此目录**并非 skill 的唯一来源**：各 code agent 仍可在自身私有目录创建、维护专属 skill；`.vibe/shared-skills/` 与 Agent 私有 skill 并存，二者皆可加载。

---

## 2. Unity 工程红线（禁止类）

以下行为**默认禁止**，需用户明确放行才可执行：

2.1 编辑或删除 `Library/`、`Temp/`、`Logs/`、`obj/`、`Build/`、`UserSettings/`、`MemoryCaptures/` 等自动生成目录。  
2.2 手工修改 `.meta` 文件中的 `guid`、`fileFormatVersion`、`timeCreated` 等系统字段。  
2.3 直接二进制编辑 `.unity`、`.prefab`、`.asset`、`.controller`、`.anim`、`.mat`、`.physicMaterial` 等 YAML/二进制资产，除非用户已说明知晓 YAML 结构并指定字段。  
2.4 改动 `ProjectSettings/` 下任意文件（含 `ProjectVersion.txt`、`InputManager.asset`、`GraphicsSettings.asset` 等）。  
2.5 改动 `Packages/manifest.json`、`Packages/packages-lock.json`，包括添加 / 升级 / 删除依赖。  
2.6 触发 `Reimport All`、`Clear All Caches`、删除 `Library/` 强制重导入。  
2.7 执行 `git reset --hard`、`git clean -fdx`、`git push --force`、删除分支等不可逆 Git 操作。  
2.8 在没有备份或脏工作区情况下大规模 `Find & Replace`、批量重命名脚本类名（涉及 Unity 序列化丢失风险）。  
2.9 提交（commit）或推送（push）代码——除非用户在当次会话明确指示。

---

## 3. Unity 工程通行规则（推荐类）

3.1 **资产新增**：优先让 Unity 编辑器生成 `.meta`；若必须手写，则保留模板 guid 的拷贝来源、不复制已有 guid 造成冲突。  
3.2 **C# 代码风格**：跟随项目既有命名空间、命名（PascalCase 类型 / camelCase 字段 / `_camelCase` 私有字段，按现状）、缩进（4 空格）、`using` 顺序。不引入新规范统一改动。  
3.3 **MonoBehaviour / ScriptableObject**：
  - 字段重命名前考虑 `[FormerlySerializedAs]` 以避免序列化丢失；
  - 删除 public 字段前确认无 Inspector / 资产引用；
  - 禁止在编辑器外随意改动 `[SerializeField]` 字段顺序与类型。  
3.4 **场景与 Prefab**：
  - 修改前先 `git status` 确认工作区干净；
  - 不在 Agent 自动化中"打开场景并保存"，避免无意义 diff；
  - Prefab 嵌套 / Variant 改动需在描述中说明影响范围。  
3.5 **Asset Database**：调用 `AssetDatabase.Refresh / ImportAsset / SaveAssets` 等需在 Editor 脚本里且明确告知用户。  
3.6 **Addressables / AssetBundle**：不擅自重建 group、不擅改 label / address，相关操作前必须确认。  
3.7 **平台 / 渲染管线**：URP / HDRP / Built-in 切换、目标平台切换属于重大变更，禁止 Agent 主动进行。

---

## 4. 代码改动准则

4.1 **最小改动**：只改必要部分，不顺手"清理"无关代码、不补无关注释、不重排 `using`。  
4.2 **不引入额外抽象**：禁止为单点需求创建 helper / interface / 配置开关。  
4.3 **错误处理**：不在内部调用处加无意义 try/catch；只在系统边界（IO、网络、用户输入）做防御。  
4.4 **依赖第三方库**：新增 NuGet / UPM / DLL 必须先获用户确认。  
4.5 **测试**：若项目已有 EditMode / PlayMode 测试，改动相关代码后建议运行；运行命令未知则询问。  
4.6 **性能敏感代码**（`Update`、`OnGUI`、`OnRenderObject`、Job/Burst、ECS）：避免每帧分配、避免反射 / LINQ；改动需特别说明。

---

## 5. 文件与目录约定

5.1 **不主动创建** `README.md`、`CHANGELOG.md`、`docs/` 等文档文件，除非用户要求。  
5.2 **不主动创建** `.editorconfig`、`.gitattributes`、`.gitignore` 等仓库配置，除非用户要求。  
5.3 **临时脚本 / 一次性工具** 放在 `Assets/Editor/_Scratch/` 或用户指定目录，并标注用途。  
5.4 **生成大文件**（>1 MB 单文件 / >50 文件批量）前必须确认。

---

## 6. Git 与版本控制

6.1 默认**只读** Git：可 `git status` / `git diff` / `git log` 观察，不主动 `add` / `commit` / `push`。  
6.2 用户要求提交时：
  - 使用 `/commit` 流程；
  - 不绕过 hook（`--no-verify` 禁用）；
  - 不签别人名（不伪造 author / committer）。  
6.3 LFS：若项目启用 LFS（存在 `.gitattributes` 中 `filter=lfs`），新增大文件需确认是否走 LFS。

---

## 7. Subagent 与工具调用

7.1 派发 Subagent 时必须在 prompt 中**显式引用**本文件路径，并要求其先读取。  
7.2 优先使用项目自带专用 Agent（如 `ue-edit` / `ue-explore` 等），不要用 `general-purpose` 替代。  
7.3 长任务优先后台运行（`run_in_background`），主 Agent 不轮询、等通知。  
7.4 涉及外部 MCP（如 `UnityMCP`）调用前先核对该工具是否会对工程产生写副作用。

---

## 8. 安全与隐私

8.1 不向第三方在线服务（pastebin、gist、在线 diagram 渲染等）上传项目源码、资产、密钥。  
8.2 检测到 `*.json`、`*.cs`、`*.env` 等文件中存在 token / API key / 密码字面量时，立即停下并告知用户。  
8.3 不在日志、commit message、PR 描述中泄露上述敏感信息。

---

## 9. 失败与越界处置

9.1 察觉违反本文件即**立即停止**，不得"先做完再说"。  
9.2 已造成的可逆改动应主动回滚（`git restore` / 文件还原），并告知用户范围。  
9.3 不可逆改动（已删除文件、已 push、已改动外部系统）必须立刻完整告知，不得隐瞒。  
9.4 不在同一会话内对**同一禁区**重复试探——一次被拒绝即视为本会话内永久拒绝，除非用户主动放行。

---

## 10. 项目特定补充（占位）

> 本节由用户随项目演进维护。若为空，按 1–9 节执行。

- _（在此添加 KeplerProjet 专属约定：模块归属、命名空间、Scene 命名规则、特定 Prefab 不可改动名单等。）_

---

**版本**：v0.1（模板初版）  
**维护**：由用户主动维护；Agent 不得擅自修改本文件。

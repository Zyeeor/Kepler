# Agent Task Start Prompt

```text
开始 <TASK-ID>。

请先读取：
1. AGENTS.md
2. .vibe/rules.md
3. 当前Task
4. Parent Feature
5. 相关Canonical
6. Open Decisions
7. 当前实现

现在不要修改文件。

先向我报告：
- 你理解的目标
- Scope
- Out of Scope
- 计划修改哪些文件
- 实现方案
- 测试方案
- 风险
- 是否存在需求矛盾
- 是否涉及Shared Original或高风险文件

如果发现规则不明确、Open Decision或高风险修改，请停止并说明。

如果没有阻塞，再进入执行。

执行完成后：
1. 运行可执行的测试/检查；
2. 检查git diff；
3. 检查是否越界修改；
4. 检查是否出现新增严重错误；
5. 输出变更摘要；
6. 输出需要人类Review的重点。
```

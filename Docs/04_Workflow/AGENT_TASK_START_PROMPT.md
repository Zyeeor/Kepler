# Agent Task Start Prompt

```text
开始 <TASK-ID>。

请先：
1. 读取 AGENTS.md；
2. 读取 .vibe/rules.md；
3. 找到并读取对应 GitHub Issue；
4. 检查 Issue 是否链接 Detailed Contract；
5. 如果存在 Contract，读取对应 Tasks/*.md；
6. 按需读取 Parent Feature、相关 Canonical、Open Decisions；
7. 读取当前实现。

Issue-only Task 不存在 Markdown 时，不得仅因缺少 Tasks/*.md 拒绝执行。
如果 Issue 存在 Detailed Contract，则必须读取并遵守。
如果 Issue 及 Contract（如有）信息不足，请停止并要求补充，不要自行猜测规则。

现在不要修改文件。

先向我报告：
- Goal
- Scope
- Out of Scope
- Files
- Plan
- Test Plan
- Risks
- Requirement Conflicts
- Shared Asset Risk

如果发现规则不明确、Open Decision、需求冲突或高风险修改，请停止并说明。

如果没有阻塞，再进入执行。

执行完成后：
1. 运行可执行的测试/检查；
2. 检查 git diff；
3. 检查是否越界修改；
4. 检查是否出现新增严重错误；
5. 输出变更摘要；
6. 输出需要人类 Review 的重点。
```

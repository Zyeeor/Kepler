# VFX Plugins

这里存放项目中供游戏特效师使用的 Unity 编辑器插件。

## 插件列表

### 子特效模板菜单

脚本：`Editor/VfxTemplateMenu.cs`

用途：快速将预制好的特效模板添加为母粒子系统的子对象。

使用方法：

1. 在 Unity Hierarchy 中右击带有 `ParticleSystem` 组件的母对象。
2. 选择 `VFX > 添加子特效模板...`。
3. 在弹出的模板选择窗口中，按分类选择需要的特效 Prefab。

模板来源：

`Assets/Art folder/VFX/VFX-Tex/M`

材质副本输出目录：

`Assets/Art folder/VFX/VFX-Tex/MaterialCopies`

功能说明：

- 自动递归扫描模板目录中的全部 Prefab。
- 菜单分类与模板目录的子文件夹结构保持一致。
- 新增模板 Prefab 后无需修改插件代码。
- 选中的模板会作为当前母粒子系统的子对象创建。
- 新对象的本地位置和本地旋转会自动归零。
- 添加后自动完全解包，不保留 Unity Prefab 实例关联。
- 自动复制全部子 Renderer 使用的材质，并改为引用独立材质副本。
- 同一特效内原本共用的材质仍共用同一份副本。
- 支持 `Ctrl+Z` 撤销。
- 创建完成后自动选中新对象。
- 播放模式下不可使用。

### Panda 后期材质切换器

运行时脚本：`PandaPostProcessSwitcher.cs`

Inspector 脚本：`Editor/PandaPostProcessSwitcherEditor.cs`

用途：在同一个 GameObject 上的多个 `PandaPostProcess` 后期材质之间快速切换，并保证任意时刻只启用其中一个。

使用方法：

1. 在挂有多个 `Panda Post Process (Script)` 的 `Global Volume` 对象上添加 `VFX > Panda 后期材质切换器` Component。
2. 切换器会自动查找同一对象上的全部 `PandaPostProcess`。
3. 通过“启用后期材质”下拉框、快捷选择按钮或“上一个/下一个”按钮切换效果。
4. 选择“全部关闭”可关闭所有 Panda 后期材质。

功能说明：

- 自动使用各脚本引用的材质名称作为选项名称。
- 切换时自动启用选中项并关闭其他项。
- 编辑模式和播放模式均可使用。
- 支持 `Ctrl+Z` 撤销 Inspector 中的切换操作。
- 可关闭自动查找并手动维护效果列表。
- 提供 `SetActiveEffect`、`SelectPreviousEffect`、`SelectNextEffect` 和 `DisableAllEffects` 公共方法，可供按钮、Timeline 或其他脚本调用。

## 目录约定

- 仅编辑器使用的插件放在 `Plugins/Editor` 下。
- 需要在播放模式运行的 Component 放在 `Plugins` 根目录，不能放入 `Editor` 目录。
- 新增或删除插件后，请同步更新本文件中的“插件列表”。

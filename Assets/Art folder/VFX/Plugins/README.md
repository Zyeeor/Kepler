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

## 目录约定

- 编辑器插件统一放在 `Plugins/Editor` 下，避免被编译进游戏运行时。
- 新增或删除插件后，请同步更新本文件中的“插件列表”。

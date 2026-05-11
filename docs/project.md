# DarkFlare 项目概览

## 项目状态

`DarkFlare` 当前已完成 Unity 项目的基础目录初始化、核心插件接入和架构入口落地，整体仍处于骨架搭建阶段。

当前可确认的现状：

- Unity 版本为 `6000.4.3f1`
- 主架构基于 `QFramework`
- 已引入 `UniTask`
- 已启用 `Addressables`
- 已接入新输入系统 `InputSystem`
- 已引入 `Odin Inspector`
- 已引入 `PrimeTween`
- 已引入 `unity-mcp`
- 当前构建场景只有 `Assets/Scenes/SampleScene.unity`
- 当前自定义脚本主要集中在 `Assets/Scripts/Core`

## 核心入口

### 架构入口

- `Assets/Scripts/Core/GameArchitecture.cs`
  - 定义 `GameArchitecture : Architecture<GameArchitecture>`
  - 当前 `Init()` 为空，说明业务模块尚未开始注册

### 基础框架

- `Assets/Scripts/Core/QFramework.cs`
  - 项目内直接放置 QFramework 基础实现
  - 后续 `Model`、`System`、`Controller`、`Utility` 应围绕该架构扩展

## 当前技术栈

根据 `Packages/manifest.json` 与项目目录，当前已接入的关键能力如下：

- 架构：QFramework
- 异步：UniTask
- 资源管理：Addressables
- 输入：Input System
- 编辑器增强：Odin Inspector
- 动画补间：PrimeTween
- 渲染管线：URP
- UI：UGUI
- 导航：AI Navigation
- 工具链：unity-mcp

## 当前资源与配置现状

- `Assets/Settings` 下已存在输入系统、URP、场景相关资源
- `Assets/AddressableAssetsData` 已建立 Addressables 配置目录
- `Assets/Data/Preset`、`Assets/Data/Saves` 已预留数据目录
- `Assets/Art`、`Assets/Prefabs` 等资源目录目前仍以占位为主，尚未形成具体业务内容

说明：
当前 `Assets/Scripts/UI` 目录已创建，但资源层的 UI 目录尚未单独建立；UI 资源后续可结合实际方案继续细化。

## 当前开发判断

从目录和代码现状看，项目已经完成“工程模板化”这一步，但还没有进入具体玩法或系统实现阶段。当前最适合继续补齐的是：

1. 明确首个可运行玩法闭环和场景职责
2. 按 QFramework 规划首批 `Model`、`System`、`Controller`
3. 明确配置资源类型与 ScriptableObject 数据入口
4. 为 UI 层确定 UIToolkit/UGUI 的实际分工边界

## 文档维护约定

- 文档内容以代码和目录现状为准
- 目录调整后，应同步更新 `docs/project-structure.md`
- 新增核心模块后，应补充项目概览中的入口说明

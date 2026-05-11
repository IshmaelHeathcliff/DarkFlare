# DarkFlare 目录结构

## 根目录

```text
DarkFlare/
  Assets/
  design/
  docs/
  Packages/
  ProjectSettings/
```

## Assets 目录

```text
Assets/
  AddressableAssetsData/
  Art/
    Animaitons/
      AnimationClips/
      Animators/
    Audio/
    Materials/
    Textures/
  Data/
    Preset/
    Saves/
  Plugins/
    Roslyn/
    Sirenix/
  Prefabs/
  Scenes/
    SampleScene.unity
  Scripts/
    Core/
    Data/
    Editor/
    Gameplay/
    UI/
    Utilities/
  Settings/
    Scenes/
  TextMesh Pro/
```

## 目录职责说明

### `Assets/AddressableAssetsData`

Addressables 的配置目录，包含资源组、模板和构建器配置。后续资源加载应优先基于这里维护，而不是使用 `Resources`。

### `Assets/Art`

美术资源目录，当前已预留：

- `Animaitons`
- `Audio`
- `Materials`
- `Textures`

说明：
当前目录名为 `Animaitons`，文档按现状记录，若后续修正拼写，需要同步调整文档与资源引用。

### `Assets/Data`

项目数据目录，当前分为：

- `Preset`：适合放预设型配置资源
- `Saves`：适合放存档或运行期持久化数据

### `Assets/Plugins`

第三方插件目录，当前已可确认：

- `Sirenix`：Odin Inspector
- `Roslyn`：编译相关依赖

### `Assets/Prefabs`

预制体目录，当前已创建，尚未看到具体业务资源。

### `Assets/Scenes`

场景目录。当前仅有默认场景：

- `Main.unity`

同时 `ProjectSettings/EditorBuildSettings.asset` 里当前也只注册了这个场景。

### `Assets/Scripts`

代码目录已按职责分层：

- `Core`：基础架构与全局入口
- `Data`：数据定义与配置类型
- `Editor`：编辑器扩展
- `Gameplay`：玩法逻辑
- `UI`：界面逻辑
- `Utilities`：通用工具

当前真实代码主要在 `Core`：

- `QFramework.cs`
- `GameArchitecture.cs`

其余目录目前主要是占位，为后续模块扩展预留。

### `Assets/Settings`

项目资源级设置目录。当前可见内容主要用于：

- 输入系统配置
- URP 配置
- 场景相关设置资源

### `Assets/TextMesh Pro`

TextMesh Pro 默认资源目录。

## 非 Assets 目录

### `design`

设计资料目录，当前未见明确内容。适合后续放玩法草案、流程稿、界面草图等非运行时资源。

### `docs`

项目文档目录。当前已建立基础文档，应作为结构说明、开发约定和系统设计的统一入口。

### `Packages`

Unity 包管理目录。当前关键依赖包括：

- `com.coplaydev.unity-mcp`
- `com.cysharp.unitask`
- `com.kyrylokuzyk.primetween`
- `com.unity.addressables`
- `com.unity.inputsystem`
- `com.unity.ugui`
- `com.unity.render-pipelines.universal`

### `ProjectSettings`

Unity 工程级设置目录，包括版本、构建场景、图形设置等。

## 当前结构判断

当前结构已经具备继续开发的基础条件，特点是：

- 资源目录、代码目录、设置目录已经先分层
- 插件和核心依赖已经接入
- 代码架构入口已经就位
- 业务模块、配置定义和具体内容仍基本为空

因此，后续工作重点不在“再拆目录”，而在把每一层真正填上首批可运行内容。


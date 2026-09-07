# 项目指南

## 基础

- 始终使用中文回答。
- 实现功能前先做计划。
- 编码前思考。不要假设。不要隐藏困惑，困惑时停下来，适时提出异议。
- 简洁优先。用最少的代码解决问题，不要过度推测。
- 精准修改。只碰必须碰的。只清理自己造成的混乱。
- 目标驱动执行。定义成功标准，循环验证直到达成。
- 优先使用已有工具来完成功能，减少自行造轮子。
- 为模块化和低耦合性努力。

## 文档维护

- 使用 UTF-8 编码
- 所有项目文档统一放在 `Docs/` 容器下，目录入口为 `Docs/README.md`。
- 除游戏设计文档外，Agent 管理的模块文档放在 `Docs/docs/` 目录下。
- 要区分计划文档与模块功能文档，计划文档放在 `Docs/docs/plan/` 下。
- 计划完成时，应该总结模块文档，然后将原本的计划文档归档到 `Docs/docs/plan/archive/`。
- 文档应分模块管理，可创建子目录管理，单个文档不应该过长。
- 修改代码前先阅读相关文档。
- 如果文档与代码冲突，以代码现状为准，并同步修正文档。
- 新功能完成后，主动检查是否需要更新文档 。
- 不要让文档长期落后于实现。
- `Docs/design/` 目录仅存放主要由人工管理的游戏设计文档。只有在纯游戏设计任务中显式调用 `/design`（`$design`）Skill 时，才允许 Agent 读取或修改其中的文档。
- 开发、实现、调试、测试等任务禁止读取、引用或修改 `Docs/design/`；同时包含设计与开发的请求必须先拆分任务。

## 设计原则

- 玩家操作上充分考虑键鼠与手柄的兼容
- 在合适的地方加入Debug信息方便测试与验证
- 在需要的情况下对核心逻辑写测试

### 测试维护

- 新增测试前先评估需要长期防止的故障、现有覆盖和维护成本；优先补充已有模块测试，不为每个版本或一次性任务新建测试套件。
- 长期保留核心规则、事务失败回退、历史存档迁移、输入可达性、资源与 Session 生命周期、玩家数据隔离等有持续价值的回归。
- 临时排查、发布记录核验、一次性资产迁移和截图采集测试在完成验证后清理；需要保留的证据归档到 `Docs/docs/`，不保留反复核验历史成功记录的运行测试。
- 不以固定版本号、文件 / 字段数量、源码字符串、旧 UI 像素尺寸代替行为测试；配置 / 架构策略检查只保留明确的长期合同，避免重复扫描。
- 每次功能收尾审查新增及受影响测试，删除过时项、合并重复项和无用夹具；不得为了减少数量而删除独有覆盖、真实迁移历史夹具或数据隔离保护。
- 详细规则见 [测试维护规范](Docs/docs/testing/test-maintenance.md)。

## 基础工具

- 使用 [QFramework.cs](Assets/Scripts/Core/QFramework.cs) 作为基础架构，遵守规则，降低模块耦合性
  - 将模块分为 `Model` 、 `System` 、 `Controller` 、 `Utility`
  - 大多数 `Monobehaviour` 都应该实现 `IController` 来接入QFramework
  - 在 Controller 中使用 Command 对 `Model` 进行操作
  - 在 Controller 中使用 Query 对 `Model` 信息进行查询
  - 在 Controller 中注册 Event 来实现事件回调
  - Controller 中只能注册 Event，不能发送 Event
  - 在 System、Model、Command中完成操作时发送 Event 通知 Controller
- 使用 `UniTask` 处理异步操作
  - 优先使用异步操作代替协程、 `Update` 等
  - 积极使用UniTask进阶工具，如Channel、UniTaskAsyncEnumerable、UniTaskCompletionSource、AsyncReactiveProperty等
  - 所有 UniTask 都需要完善的取消逻辑，并可统一取消
- 使用Unity的 `Addressables` 进行资源加载与管理，不要使用 Resource 加载
  - 正确处理资源的加载与卸载
- 使用 Unity 的新输入系统 InputSystem
- 在开发中优先使用 `Odin` 构建方便使用的Editor工具，充分发挥 Odin 的功能
- 使用 `SerializedScriptableObject` 或 `ScriptableObject` 作为主要的数据配置类
  - 数据配置类中各种字段、枚举等标注中文
  - 使用 Odin 原生功能
- 使用 `PrimeTween` 进行动画和补间操作
- 性能敏感的大量数据优先使用 DOTS 处理
- 在 Windows 上优先使用 PowerShell 7
- 不要使用过时 Unity API
- 使用 Unity Localization 处理本地化

### Unity UI

- UI脚本所需组件应设为 `SerializeField`，并在 `OnValidate` 中检查并尝试创建
- 优先使用 UIToolkit 创建UI，当涉及复杂的动画等逻辑时使用 UGUI

### MCP

- 需要使用unity mcp时，如果mcp没有开启，需要先尝试开启mcp，否则停下来并提示
- 使用 unityMCP 进行 UI 创建与布局
- 需要美术资源时，调用image gen、 mcp 或 skill 生成
- 不能在 Unity Editor 已打开场景时直接修改场景文件，可通过 unityMCP 或脚本修改

## 编码规范

### 基础设置
- 字符编码：UTF-8
- 行尾：LF
- 文件末尾：保留空行
- 缩进：使用空格
- 缩进大小：4个空格

### 命名规范
#### 私有字段
- 使用下划线前缀，驼峰命名
- 不声明 private

#### 公共成员
- 使用帕斯卡命名法
- 不使用公开字段，使用属性访问私有字段
- 适用于：属性、方法、事件、委托
- 访问级别：public、internal、protected、protected_internal

#### 私有成员
- 不声明 private

#### 接口
- 必须以 "I" 开头
- 使用帕斯卡命名法
- 不声明 public 方法

#### 私有常量
- 使用帕斯卡命名法

#### 私有静态字段
- 使用 "s_" 前缀
- 驼峰命名法

#### 私有静态只读字段
- 使用帕斯卡命名法

#### 类型参数
- 使用帕斯卡命名法

### 代码风格
#### 大括号风格（Allman 风格）
- 大括号前换行
- else 前换行
- catch 前换行
- finally 前换行
- 对象初始化器成员前换行
- 匿名类型成员前换行
- 查询表达式子句间换行

#### 空格使用
- 类型转换后不加空格
- 控制流语句关键字后加空格
- 方法声明参数列表括号内不加空格
- 方法调用参数列表括号内不加空格
- 括号内不加空格
- 继承子句冒号前后加空格
- 二元运算符前后加空格
- 空参数列表括号内不加空格
- 方法调用名称和左括号间不加空格

#### 其他代码风格
- 省略默认访问修饰符
- 强制使用 readonly 字段
- 内置类型不使用 var
- 类型明显时使用 var
- 其他情况不使用 var
- 不使用表达式体方法
- 使用表达式体属性
- 使用表达式体索引器
- 使用表达式体访问器
- 保留单行代码块
- 保留单行语句
- System 指令优先排序
- 不分离导入指令组

### 其他文件类型设置
#### YAML、JSON、Markdown文件
- 缩进：2个空格

### 代码质量规则
- 强制使用大括号
- 使用 throw 表达式
- 使用内联变量声明
- 使用模式匹配替代 is 类型检查
- 使用模式匹配替代 as 空检查

### Unity 特定规范
- 使用 `[SerializeField]` 标记需要在 Inspector 中显示的私有字段
- 使用 `[ShowInInspector]` 标记需要在 Inspector 中显示的属性

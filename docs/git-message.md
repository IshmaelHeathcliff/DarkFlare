# Git Commit Message 规范

使用中文 message

```cs
<type>: <subject>

<body>
```

## header

- 主要
  - feat
    - 增加新功能
  - improve
    - 旧功能改进
  - fix
    - 修复bug
  - art
  - ui
- 特殊
  - doc
    - 只改动了文档相关的内容
  - style
    - 不影响代码含义的改动，例如去掉空格、改变缩进、增删分号
  - build
    - 构造工具的或者外部依赖的改动，例如webpack，npm
  - refactor
    - 代码重构时使用
  - revert
    - 执行git revert打印的message
- 暂不使用
  - test
    - 添加测试或者修改现有测试
  - perf
    - 提高性能的改动
  - ci
    - 与CI（持续集成服务）有关的改动
  - chore
    - 不修改src或者test的其余修改，例如构建过程或辅助工具的变动

## subject

简短描述，不超过50个字符

- 以动词开头，使用第一人称现在时，比如change，而不是changed或changes
- 第一个字母小写
- 结尾不加句号（.）

## Body

- 使用第一人称现在时
- 说明代码变动动机，与之前行为对比

## revert

```cs
revert: feat(...): ...

This reverts commit <hash>.
```
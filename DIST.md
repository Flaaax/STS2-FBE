## DIST
如果用户忘记了版本分派的流程，或者需要更新游戏版本，就遵循此内容。

# FBE 多版本分发说明

本文档说明 FBE 如何在同一个项目中同时支持多个《Slay the Spire 2》游戏版本，以及这个机制如何影响代码、构建、发布和后续版本迭代。

当前目标版本：

- `0.107.1`：稳定版
- `0.110.0`：beta 版 / 当前主要开发版

版本号在项目中应始终写完整，例如 `0.107.1`、`0.110.0`。不要使用 `107`、`110` 这种简称。

---

## 1. 总体机制

FBE 采用 JML Dispatch 的“入口 DLL + 多 Runtime DLL”结构。

最终发布目录大致如下：

```text
FBE/
  FBE.json
  FBE.pck
  FBE.dll
  FBE.dispatch.json
  runtimes/
    0.107.1/
      FBE.Runtime.dll
    0.110.0/
      FBE.Runtime.dll
```

其中：

- `FBE.dll` 是 Dispatch Bootstrap，即游戏首先加载的入口 DLL。
- `FBE.dispatch.json` 描述不同游戏版本应该加载哪个 Runtime DLL。
- `runtimes/<version>/FBE.Runtime.dll` 才是真正包含 FBE 模组逻辑的 DLL。
- `FBE.json` 仍然是模组 manifest。
- `FBE.pck` 是 Godot 资源包。

Dispatch Bootstrap 会读取当前游戏版本，然后根据 `FBE.dispatch.json` 选择对应的 Runtime：

```text
游戏版本 0.107.1 -> runtimes/0.107.1/FBE.Runtime.dll
游戏版本 0.110.0 -> runtimes/0.110.0/FBE.Runtime.dll
```

这个方案的核心收益是：

- 同一个模组文件夹可以支持多个游戏 ABI。
- 不需要让一个 DLL 同时兼容多个不兼容 ABI。
- 不需要把 JML 作为 FBE 的运行时依赖。
- 可以只更新某一个版本的 Runtime DLL，用于开发和快速测试。

其中，JML Dispatch 不是运行时依赖，只出现在本地构建中。因此不需要依赖此mod。

---

## 3. 本地依赖目录

不同游戏版本的 DLL 不再直接引用当前 Steam 安装目录，而是引用本机缓存目录。

当前本机路径：

```text
E:\STS\STS2\STS2 binary\0.107.1\data_sts2_windows_x86_64\
E:\STS\STS2\STS2 binary\0.110.0\data_sts2_windows_x86_64\
```

至少需要包含：

```text
sts2.dll
0Harmony.dll
```

---

## 4. 构建工具目录

JML Dispatch targets 放在本机固定路径：

```text
E:\STS\STS2\BuildTools\JmcModLib\JmcModLib.Dispatch.targets
```

---

## 5. 构建脚本

见 DEV.md

## 6. 两种构建目标

项目主要使用两个 MSBuild Target。

### 6.1 `CurrentVersion`

用途：只构建当前配置对应的游戏版本。

示例：

```powershell
dotnet build -t:CurrentVersion -c "Release 0.110.0"
```

行为：

1. 根据 `Release 0.110.0` 解析出 `Sts2GameVersion=0.110.0`。
2. 使用 `0.110.0` 的游戏 DLL 编译 Runtime。
3. 输出 `runtimes/0.110.0/FBE.Runtime.dll`。
4. 生成 Dispatch Bootstrap：`FBE.dll`。
5. 生成单版本 `FBE.dispatch.json`。
6. 导出 `FBE.pck`。
7. 复制 `FBE.json`。

适合开发时使用，因为只需要维护当前正在改动的版本。

### 6.2 `AllVersion`

用途：构建所有支持版本。

示例：

```powershell
dotnet build -t:AllVersion -c "Release 0.110.0"
```

行为：

1. 清理目标目录中的 `runtimes`，避免旧版本残留。
2. 遍历版本矩阵 `@(Sts2SupportedVersion)`。
3. 分别构建每个版本的 Runtime。
4. 生成全版本 Dispatch Bootstrap 和 `FBE.dispatch.json`。
5. 导出 `FBE.pck`。
6. 复制 `FBE.json`。

适合发布前使用。

---

## 7. 版本矩阵

支持版本集中写在 `FBE.csproj` 的 `Sts2SupportedVersion` ItemGroup 中。

示例：

```xml
<ItemGroup>
    <Sts2SupportedVersion Include="0.107.1">
        <Configuration>Release 0.107.1</Configuration>
        <MaxGameVersionExclusive>0.108.0</MaxGameVersionExclusive>
    </Sts2SupportedVersion>

    <Sts2SupportedVersion Include="0.110.0">
        <Configuration>Release 0.110.0</Configuration>
        <MaxGameVersionExclusive>0.111.0</MaxGameVersionExclusive>
    </Sts2SupportedVersion>
</ItemGroup>
```

含义：

- `Include`：该 Runtime 对应的最小游戏版本，也作为 runtime 目录名。
- `Configuration`：用于构建该版本 Runtime 的 MSBuild 配置名。
- `MaxGameVersionExclusive`：该 Runtime 支持的最大游戏版本上界，不包含该版本。

例如：

```xml
<Sts2SupportedVersion Include="0.107.1">
    <Configuration>Release 0.107.1</Configuration>
    <MaxGameVersionExclusive>0.108.0</MaxGameVersionExclusive>
</Sts2SupportedVersion>
```

表示：

```text
支持范围：[0.107.1, 0.108.0)
Runtime 路径：runtimes/0.107.1/FBE.Runtime.dll
构建配置：Release 0.107.1
```

---

## 8. 版本如何影响代码

不同游戏版本的 ABI 可能不同，例如：

- 类型名不同
- 方法名不同
- 虚函数签名不同
- 构造函数参数不同
- 属性或字段增删

这些差异通过 C# 条件编译处理。

`FBE.csproj` 会根据 `Sts2GameVersion` 自动生成条件编译符号。

规则：

```text
0.107.1 -> STS2_0_107_1
0.110.0 -> STS2_0_110_0
```

代码中应这样写：

```csharp
#if STS2_0_107_1
// 0.107.1 ABI
#elif STS2_0_110_0
// 0.110.0 ABI
#else
#error Unsupported STS2 game version.
#endif
```

推荐原则：

1. 优先只包住 ABI 差异最小的代码片段。
2. 不要把整个类都包进 `#if`，除非差异非常大。
3. 如果同一文件中的 `#if` 变得过多，可以改为多文件方案。
4. 文件拆分可以作为 fallback，但初期用 `#if` 更直接。

例如方法名不同：

```csharp
#if STS2_0_107_1
OldApiName(arg);
#elif STS2_0_108_0
NewApiName(arg);
#else
#error Unsupported STS2 game version.
#endif
```

例如 override 签名不同：

```csharp
#if STS2_0_107_1
protected override void SomeMethod()
{
    // 0.107.1 implementation
}
#elif STS2_0_108_0
protected override void SomeMethod(SomeArg arg)
{
    // 0.108.0 implementation
}
#else
#error Unsupported STS2 game version.
#endif
```

---

## 13. 版本迭代流程

假设未来需要从：

```text
0.107.1 + 0.108.0
```

升级到：

```text
0.108.0 + 0.109.0
```

推荐流程如下。

### 13.1 缓存新版本游戏 DLL

从 Steam 切到新版本后，复制游戏 DLL 到：

```text
E:\STS\STS2\STS2 binary\0.109.0\data_sts2_windows_x86_64\
```

至少确认有：

```text
sts2.dll
0Harmony.dll
```

### 13.2 修改版本矩阵

例如改成：

```xml
<ItemGroup>
    <Sts2SupportedVersion Include="0.108.0">
        <Configuration>Release 0.108.0</Configuration>
        <MaxGameVersionExclusive>0.109.0</MaxGameVersionExclusive>
    </Sts2SupportedVersion>

    <Sts2SupportedVersion Include="0.109.0">
        <Configuration>Release 0.109.0</Configuration>
        <MaxGameVersionExclusive>0.110.0</MaxGameVersionExclusive>
    </Sts2SupportedVersion>
</ItemGroup>
```

### 13.3 添加构建配置

需要确保项目存在对应配置：

```text
Release 0.109.0
```

如果配置名遵循 `Release <完整版本号>`，`Sts2GameVersion` 会自动解析为：

```text
0.109.0
```

### 13.4 修改代码中的 ABI 条件编译

如果新版本没有 ABI 差异，可能只需要让旧分支继续兼容。

如果有 ABI 差异，添加：

```csharp
#if STS2_0_109_0
// 0.109.0 ABI
#endif
```

如果旧版本不再支持，可以删除对应的 `#if STS2_0_107_1` 分支。

### 13.5 更新版本保护文件

如果项目中有 `Sts2VersionGuards.cs`，同步添加新版本宏。

### 13.6 更新 build.bat 最新版本

把：

```bat
set "LATEST_VERSION=0.108.0"
```

改成：

```bat
set "LATEST_VERSION=0.109.0"
```

### 13.7 测试单版本构建

```powershell
.\build 0.109.0
```

确认新版本可以进入游戏加载。

### 13.8 测试全版本构建

```powershell
.\build all
```

确认所有支持版本 Runtime 都存在，并且 `FBE.dispatch.json` 中包含正确版本范围。

---

## 14. 开发建议

### 14.1 开发新功能时

通常只构建当前最新版本：

```powershell
.\build
```

如果功能只改了 `0.110.0` 分支，不需要立刻修 `0.107.1`。

### 14.2 修稳定版兼容时

只构建稳定版：

```powershell
.\build 0.107.1
```

### 14.3 发布前

必须全量构建：

```powershell
.\build all
```

然后检查：

```text
mods/FBE/FBE.dll
mods/FBE/FBE.dispatch.json
mods/FBE/FBE.json
mods/FBE/FBE.pck
mods/FBE/runtimes/0.107.1/FBE.Runtime.dll
mods/FBE/runtimes/0.110.0/FBE.Runtime.dll
```

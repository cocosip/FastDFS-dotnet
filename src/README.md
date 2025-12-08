# src 目录说明

## 项目结构

```
src/
├── common.props                           # 公共项目属性文件
├── FastDFS.Client/                        # 核心客户端库
└── FastDFS.Client.DependencyInjection/    # 依赖注入扩展库
```

## common.props

`common.props` 文件包含了 `FastDFS.Client` 和 `FastDFS.Client.DependencyInjection` 项目的公共属性。

### 包含的属性：

1. **目标框架和语言设置**
   - `TargetFramework`: netstandard2.0
   - `LangVersion`: latest
   - `Nullable`: enable
   - `GenerateDocumentationFile`: true

2. **NuGet 包元数据**
   - `Authors`: FastDFS Team
   - `RepositoryType`: git
   - `PackageLicenseExpression`: MIT
   - `Version`: 1.0.0

### 使用方式

在项目文件中通过 `<Import>` 导入：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- 导入公共属性 -->
  <Import Project="..\common.props" />

  <!-- 项目特定属性 -->
  <PropertyGroup>
    <PackageId>YourPackageId</PackageId>
    <Description>Your description</Description>
  </PropertyGroup>
</Project>
```

### 与 Directory.Packages.props 的关系

- **common.props**: 管理项目属性（编译选项、元数据等）
- **Directory.Packages.props** (位于解决方案根目录): 管理 NuGet 包版本

这两个文件配合使用，实现：
- ✅ 项目属性统一管理
- ✅ 包版本集中控制
- ✅ 避免重复配置
- ✅ 易于维护和更新

## 项目说明

### FastDFS.Client

核心客户端库，零外部依赖。提供：
- FastDFS 协议实现
- TCP 连接和连接池
- Tracker 和 Storage 客户端
- 文件上传、下载、删除等功能

### FastDFS.Client.DependencyInjection

依赖注入扩展库，依赖 Microsoft.Extensions.* 包。提供：
- IServiceCollection 扩展方法
- IOptions 模式支持
- 多集群配置支持
- ILogger 集成

## 版本管理策略

1. **common.props 负责**:
   - 框架版本
   - 语言特性
   - 编译选项
   - 包元数据模板

2. **Directory.Packages.props 负责**:
   - NuGet 包版本号
   - 所有项目的包版本统一

3. **各项目 csproj 负责**:
   - 项目特定的描述
   - 项目特定的标签
   - 具体引用哪些包（不包含版本号）

这种分离确保了：
- 公共属性在一处维护
- 包版本在一处管理
- 项目文件保持简洁

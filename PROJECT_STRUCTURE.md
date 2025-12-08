# 项目结构说明

## 解决方案概览

```
FastDFS.Client.sln
├── src/
│   ├── FastDFS.Client/                          # 核心客户端库（无外部依赖）
│   └── FastDFS.Client.DependencyInjection/      # DI 扩展包
├── tests/
│   └── FastDFS.Client.Tests/                    # 单元测试和集成测试
├── samples/
│   └── FastDFS.Client.Samples/                  # 使用示例
├── Directory.Packages.props                      # 中央包管理
├── .gitignore
├── README.md
├── CLAUDE.md
└── TODO.md
```

## 项目说明

### 1. FastDFS.Client (核心库)

**目标**: netstandard2.0
**依赖**: 无外部依赖
**NuGet包**: `FastDFS.Client`

这是核心客户端库，不依赖任何外部包。提供：
- FastDFS 二进制协议实现
- TCP 连接和连接池管理
- Tracker 和 Storage 客户端
- 文件上传、下载、删除等核心功能
- 配置类（POCO 对象）

**目录结构**:
```
FastDFS.Client/
├── Protocol/              # 协议层
│   ├── FastDFSHeader.cs           # 协议头（10字节）
│   ├── FastDFSPacket.cs           # 数据包基类
│   ├── TrackerCommand.cs          # Tracker 命令定义
│   ├── StorageCommand.cs          # Storage 命令定义
│   ├── FastDFSConstants.cs        # 常量定义
│   ├── Requests/                  # 请求类
│   └── Responses/                 # 响应类
├── Connection/            # 连接层
│   ├── FastDFSConnection.cs       # TCP 连接封装
│   ├── IConnectionPool.cs         # 连接池接口
│   ├── ConnectionPool.cs          # 连接池实现
│   └── ConnectionPoolOptions.cs   # 连接池配置
├── Tracker/               # Tracker 客户端
│   ├── ITrackerClient.cs
│   ├── TrackerClient.cs
│   └── StorageServerInfo.cs       # Storage 服务器信息
├── Storage/               # Storage 客户端
│   ├── IStorageClient.cs
│   ├── StorageClient.cs
│   ├── FastDFSFileInfo.cs         # 文件信息
│   └── FastDFSMetadata.cs         # 元数据
├── Configuration/         # 配置
│   ├── FastDFSOptions.cs          # 核心配置类
│   └── ConnectionPoolOptions.cs   # 连接池配置
├── Exceptions/            # 异常
│   ├── FastDFSException.cs
│   ├── FastDFSNetworkException.cs
│   ├── FastDFSProtocolException.cs
│   └── FastDFSFileNotFoundException.cs
├── Utilities/             # 工具类
│   ├── ByteConverter.cs           # 字节序转换
│   └── ByteExtensions.cs          # 字节数组扩展
├── IFastDFSClient.cs      # 客户端接口
├── FastDFSClient.cs       # 客户端实现
└── FastDFSClientBuilder.cs # 非 DI 模式的构建器
```

**使用示例（无 DI）**:
```csharp
var options = new FastDFSOptions
{
    TrackerServers = new[] { "192.168.1.100:22122" },
    ConnectionPool = new ConnectionPoolOptions
    {
        MaxConnectionPerServer = 50
    }
};

var client = FastDFSClientBuilder.CreateClient(options);
var fileId = await client.UploadAsync(bytes, ".jpg");
```

### 2. FastDFS.Client.DependencyInjection (DI 扩展库)

**目标**: netstandard2.0
**依赖**:
- FastDFS.Client
- Microsoft.Extensions.DependencyInjection.Abstractions (8.0.0)
- Microsoft.Extensions.Logging.Abstractions (8.0.0)
- Microsoft.Extensions.Options (8.0.0)
- Microsoft.Extensions.Options.ConfigurationExtensions (8.0.0)

**NuGet包**: `FastDFS.Client.DependencyInjection`

提供依赖注入集成，包括：
- IOptions 模式支持
- 多集群配置支持
- IServiceCollection 扩展方法
- IFastDFSClientFactory 实现
- 日志集成

**目录结构**:
```
FastDFS.Client.DependencyInjection/
├── FastDFSServiceCollectionExtensions.cs  # AddFastDFS 扩展方法
├── IFastDFSClientFactory.cs               # 客户端工厂接口
├── FastDFSClientFactory.cs                # 客户端工厂实现
└── LoggingFastDFSClient.cs                # 带日志的客户端装饰器
```

**使用示例（DI）**:
```csharp
// 单集群
services.AddFastDFS(options => {
    options.TrackerServers = new[] { "192.168.1.100:22122" };
});

// 多集群
services.AddFastDFS("default", options => { /* ... */ });
services.AddFastDFS("backup", options => { /* ... */ });

// 从配置
services.AddFastDFS(configuration.GetSection("FastDFS"));
```

### 3. FastDFS.Client.Tests

**目标**: net10.0
**测试框架**: xUnit

包含单元测试和集成测试：
- 协议层测试
- 连接池测试
- 客户端功能测试
- 多集群场景测试

### 4. FastDFS.Client.Samples

**目标**: net10.0

提供各种使用示例：
- 基本上传下载
- DI 模式使用
- 非 DI 模式使用
- 多集群配置
- 故障转移示例

## 包管理策略

使用 **中央包管理（Central Package Management）**:

- `Directory.Packages.props`: 定义所有包的版本
- 项目文件中只需要 `<PackageReference Include="PackageName" />` 无需指定版本

**优点**:
- 统一管理所有项目的包版本
- 避免版本冲突
- 易于升级

## 版本兼容性策略

### FastDFS.Client (核心库)
- **无外部依赖**: 可以在任何支持 netstandard2.0 的环境中使用
- 兼容: .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+

### FastDFS.Client.DependencyInjection (扩展库)
- 依赖 Microsoft.Extensions.* 8.0.0 版本
- 这些是 .NET 8 LTS 的稳定版本
- 如果用户需要更低版本的 Microsoft.Extensions.*，可以：
  1. 只使用核心库 `FastDFS.Client`
  2. 或者自己实现 DI 集成

## 设计原则

1. **核心与扩展分离**: 核心库零依赖，扩展库提供额外功能
2. **向后兼容**: 目标 netstandard2.0，最大兼容性
3. **可选依赖**: 日志、DI 都是可选的，通过扩展包提供
4. **多集群支持**: DI 扩展默认支持多集群配置
5. **灵活使用**: 支持 DI 和非 DI 两种使用方式

## 发布策略

发布两个独立的 NuGet 包：

1. **FastDFS.Client** (核心包)
   - 适用于所有场景
   - 无依赖冲突风险
   - 用户完全控制

2. **FastDFS.Client.DependencyInjection** (扩展包)
   - 适用于现代 .NET 应用（ASP.NET Core 等）
   - 提供开箱即用的 DI 支持
   - 依赖 FastDFS.Client

用户可以根据需求选择：
- 只用核心库：最小依赖
- 使用扩展库：便捷的 DI 集成

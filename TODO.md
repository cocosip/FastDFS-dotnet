# FastDFS .NET Client SDK 开发任务清单

## 开发进度概览

**总体进度**: 4/25 任务完成 (16%)

**当前状态**: ✅ 协议层完成，准备进入连接层实现

**最后更新**: 2024-12-08

---

## 项目目标
实现一个完整的 FastDFS .NET 客户端 SDK，支持：
- 目标框架：netstandard2.0
- TCP 连接池管理
- 支持 IOptions 配置模式和直接配置模式
- 支持依赖注入（DI）和非 DI 使用方式
- **支持多 FastDFS 集群配置（命名客户端模式）**

---

## 第一阶段：基础架构搭建 ✅ 已完成

### 1. ✅ 创建项目结构和解决方案文件
- [x] 创建解决方案 FastDFS.Client.sln
- [x] 创建主项目 FastDFS.Client (netstandard2.0) - 核心库，零依赖
- [x] 创建扩展项目 FastDFS.Client.DependencyInjection - DI 和 IOptions 支持
- [x] 创建测试项目 FastDFS.Client.Tests (net10.0)
- [x] 创建示例项目 FastDFS.Client.Samples
- [x] 配置项目引用和中央包管理 (Directory.Packages.props)
- [x] 创建目录结构（Protocol, Connection, Tracker, Storage, Configuration, Exceptions, Utilities）

**项目结构建议：**
```
FastDFS.Client/
├── Protocol/           # 协议层
├── Connection/         # 连接和连接池
├── Tracker/           # Tracker 客户端
├── Storage/           # Storage 客户端
├── Configuration/     # 配置相关
├── Exceptions/        # 异常定义
└── DependencyInjection/ # DI 扩展
```

---

## 第二阶段：协议层实现 ✅ 已完成

### 2. ✅ 实现协议层 - 基础数据包封装
- [x] 创建 `FastDFSHeader` 类（10字节头部）
  - 8 字节 body length (long, big-endian)
  - 1 字节 command
  - 1 字节 status
- [x] 创建 `FastDFSPacket` 基类（Header + Body）
- [x] 实现大端序转换工具类 `ByteConverter`（支持 Int32/Int64 大端序转换）
- [x] 实现字节数组扩展方法 `ByteExtensions`（固定长度字符串读写、编码转换）
- [x] 创建 `IFastDFSRequest` 和 `IFastDFSResponse` 接口
- [x] 创建 `FastDFSRequest<TResponse>` 和 `FastDFSResponse` 基类

**已实现文件**:
- `Utilities/ByteConverter.cs` - 大端序转换
- `Utilities/ByteExtensions.cs` - 字节数组操作扩展
- `Protocol/FastDFSHeader.cs` - 协议头
- `Protocol/FastDFSPacket.cs` - 数据包基类
- `Protocol/IFastDFSRequest.cs` / `IFastDFSResponse.cs` - 接口
- `Protocol/FastDFSRequest.cs` / `FastDFSResponse.cs` - 基类

### 3. ✅ 实现协议层 - 命令定义和常量
- [x] 定义 `TrackerCommand` 常量类（命令码 91-107）
  - QueryStoreWithoutGroupOne = 101
  - QueryFetchOne = 102
  - QueryUpdate = 103
  - QueryStoreWithGroupOne = 104
  - 以及其他管理命令
- [x] 定义 `StorageCommand` 常量类（命令码 11-42）
  - UploadFile = 11
  - DeleteFile = 12
  - SetMetadata = 13
  - DownloadFile = 14
  - GetMetadata = 15
  - QueryFileInfo = 22
  - UploadAppenderFile = 23
  - AppendFile = 24
- [x] 定义 `FastDFSConstants` 常量类（协议常量、长度限制等）
- [x] 定义 `FastDFSErrorCode` 枚举（错误码映射）
- [x] 定义 `MetadataFlag` 枚举（Overwrite/Merge）

**已实现文件**:
- `Protocol/TrackerCommand.cs`
- `Protocol/StorageCommand.cs`
- `Protocol/FastDFSConstants.cs`
- `Protocol/FastDFSErrorCode.cs`
- `Protocol/MetadataFlag.cs`

### 4. ✅ 实现协议层 - 请求/响应序列化器
- [x] 实现 Tracker 请求/响应类
  - `QueryStoreWithoutGroupRequest/Response` - 查询上传存储服务器
  - `QueryFetchRequest/Response` - 查询下载存储服务器
- [x] 实现 Storage 请求/响应类
  - `UploadFileRequest/Response` - 上传文件
  - `DownloadFileRequest/Response` - 下载文件
  - `DeleteFileRequest/Response` - 删除文件
  - `QueryFileInfoRequest/Response` - 查询文件信息
- [x] 创建模型类
  - `StorageServerInfo` - Storage 服务器信息
  - `FastDFSFileInfo` - 文件信息（大小、创建时间、CRC32、源IP）

**已实现文件**:
- `Protocol/Requests/QueryStoreWithoutGroupRequest.cs`
- `Protocol/Requests/QueryFetchRequest.cs`
- `Protocol/Requests/UploadFileRequest.cs`
- `Protocol/Requests/DownloadFileRequest.cs`
- `Protocol/Requests/DeleteFileRequest.cs`
- `Protocol/Requests/QueryFileInfoRequest.cs`
- `Protocol/Responses/QueryStoreResponse.cs`
- `Protocol/Responses/QueryFetchResponse.cs`
- `Protocol/Responses/UploadFileResponse.cs`
- `Protocol/Responses/DownloadFileResponse.cs`
- `Protocol/Responses/DeleteFileResponse.cs`
- `Protocol/Responses/QueryFileInfoResponse.cs`
- `Tracker/StorageServerInfo.cs`
- `Storage/FastDFSFileInfo.cs`

**说明**: 元数据操作的请求/响应将在后续实现时补充

---

## 第三阶段：连接层实现 ⏳ 进行中

### 5. ⏳ 实现 TCP 连接封装类
- [ ] 创建 `FastDFSConnection` 类
  - 封装 TcpClient/NetworkStream
  - 实现异步 Send/Receive 方法
  - 实现 `SendRequestAsync<TRequest, TResponse>` 泛型方法
  - 连接健康检查 `IsAlive` 属性
  - 连接创建时间和最后使用时间跟踪
- [ ] 实现连接超时控制
- [ ] 实现连接异常处理和自动重连逻辑

### 6. 实现连接池核心逻辑
- [ ] 创建 `IConnectionPool` 接口
- [ ] 创建 `ConnectionPool` 实现类
  - 最小/最大连接数控制
  - 连接空闲超时管理
  - 获取连接 `GetConnectionAsync()`
  - 归还连接 `ReturnConnection()`
  - 连接验证和清理
  - 线程安全的连接管理（使用 ConcurrentQueue 或 Channels）
- [ ] 实现连接池监控指标（活跃连接数、空闲连接数等）
- [ ] 实现优雅关闭 `Dispose` 逻辑

---

## 第四阶段：客户端实现

### 7. 实现 Tracker 客户端
- [ ] 创建 `ITrackerClient` 接口
- [ ] 创建 `TrackerClient` 实现类
  - `QueryStorageForUploadAsync()` - 查询可上传的 Storage
  - `QueryStorageForDownloadAsync(string fileId)` - 查询文件所在 Storage
  - `QueryStorageForUpdateAsync(string fileId)` - 查询可更新的 Storage
  - 支持多个 Tracker 地址的故障转移
- [ ] 集成 Tracker 连接池

### 8. 实现 Storage 客户端 - 上传功能
- [ ] 创建 `IStorageClient` 接口
- [ ] 实现 `UploadFileAsync(byte[] content, string extension)`
- [ ] 实现 `UploadFileAsync(Stream stream, string extension)`
- [ ] 实现 `UploadFileAsync(string filePath)`
- [ ] 实现 `UploadAppenderFileAsync()` - 支持追加的文件
- [ ] 返回完整的 FileId（group_name/path/filename）

### 9. 实现 Storage 客户端 - 下载功能
- [ ] 实现 `DownloadFileAsync(string fileId)` 返回 byte[]
- [ ] 实现 `DownloadFileAsync(string fileId, Stream outputStream)`
- [ ] 实现 `DownloadFileToPathAsync(string fileId, string savePath)`
- [ ] 实现分片下载 `DownloadFileAsync(string fileId, long offset, long length)`

### 10. 实现 Storage 客户端 - 删除功能
- [ ] 实现 `DeleteFileAsync(string fileId)`
- [ ] 实现批量删除支持（可选）

### 11. 实现 Storage 客户端 - 文件信息查询
- [ ] 创建 `FastDfsFileInfo` 模型类
  - FileSize
  - CreateTime
  - Crc32
  - SourceIpAddr
- [ ] 实现 `QueryFileInfoAsync(string fileId)`

### 12. 实现 Storage 客户端 - 元数据操作
- [ ] 创建 `FastDfsMetadata` 模型类（键值对集合）
- [ ] 实现 `SetMetadataAsync(string fileId, Dictionary<string, string> metadata, MetadataFlag flag)`
  - MetadataFlag: Overwrite / Merge
- [ ] 实现 `GetMetadataAsync(string fileId)`

---

## 第五阶段：配置和依赖注入

### 13. 实现配置类
- [ ] 创建 `FastDFSOptions` 配置类（注意大小写：DFS 全大写）
  - TrackerServers (List<string>)
  - ConnectionPoolOptions
    - MaxConnectionPerServer
    - MinConnectionPerServer
    - ConnectionIdleTimeout
    - ConnectionLifetime
  - NetworkTimeout
  - Charset (默认 UTF-8)
- [ ] 创建 `FastDFSClientOptions` 配置类（支持多集群）
  - Dictionary<string, FastDFSOptions> Clusters
  - 或者 List<NamedFastDFSOptions> 结构
- [ ] 实现配置验证 (IValidateOptions)
- [ ] 支持从 appsettings.json 读取多集群配置

### 14. 实现 IOptions 模式集成和 DI 注册扩展
- [ ] 创建 `IServiceCollection` 扩展方法（支持单集群和多集群）
  - `AddFastDFS(this IServiceCollection services, Action<FastDFSOptions> configure)` - 单集群，默认名称
  - `AddFastDFS(this IServiceCollection services, string name, Action<FastDFSOptions> configure)` - 命名集群
  - `AddFastDFS(this IServiceCollection services, IConfiguration configuration)` - 从配置读取（支持多集群）
- [ ] 创建 `IFastDFSClientFactory` 接口
  - `IFastDFSClient GetClient()` - 获取默认客户端
  - `IFastDFSClient GetClient(string name)` - 根据名称获取客户端
- [ ] 实现 `FastDFSClientFactory` 类
  - 管理多个命名客户端实例
  - 每个集群独立的连接池
- [ ] 注册服务生命周期
  - `IFastDFSClientFactory` - Singleton
  - 每个命名客户端的 `IConnectionPool` - Singleton（独立实例）
  - 每个命名客户端的 `ITrackerClient` - Singleton（独立实例）
  - 每个命名客户端的 `IStorageClient` - Singleton（独立实例）
  - 默认客户端同时注册为 `IFastDFSClient` - Singleton

### 15. 实现非 DI 模式的客户端工厂类
- [ ] 创建 `FastDFSClientBuilder` 静态工厂类
  - `CreateClient(FastDFSOptions options)` - 创建单个客户端
  - `CreateClient(string configFilePath)` - 从配置文件创建
- [ ] 创建非 DI 模式的多客户端管理器（可选）
  - 允许在非 DI 场景下管理多个命名客户端
- [ ] 确保工厂创建的客户端也使用连接池

### 16. 实现统一的 FastDFSClient 门面类
- [ ] 创建 `IFastDFSClient` 接口（整合 Tracker 和 Storage 操作）
- [ ] 创建 `FastDFSClient` 实现类
  - 内部协调 Tracker 和 Storage 调用
  - 提供简化的 API（用户无需直接操作 Tracker）
  - 例如：`UploadAsync()` 自动查询 Tracker 然后上传到 Storage
  - 包含客户端名称标识（用于多集群场景）

---

## 第六阶段：增强功能

### 17. 添加日志集成
- [ ] 集成 `Microsoft.Extensions.Logging.Abstractions`
- [ ] 在关键位置添加日志
  - 连接创建/销毁
  - 请求发送/响应接收
  - 错误和异常
  - 性能指标（可选）

### 18. 实现异常处理和错误码映射
- [ ] 创建自定义异常类
  - `FastDfsException` 基类
  - `FastDfsNetworkException`
  - `FastDfsProtocolException`
  - `FastDfsFileNotFoundException`
- [ ] 实现 FastDFS 错误码到异常的映射
- [ ] 添加友好的错误消息

---

## 第七阶段：测试

### 19. 编写单元测试 - 协议层测试
- [ ] 测试 Header 序列化/反序列化
- [ ] 测试大端序转换
- [ ] 测试各种 Request/Response 的序列化
- [ ] 使用 Mock 测试协议层逻辑

### 20. 编写单元测试 - 连接池测试
- [ ] 测试连接获取和归还
- [ ] 测试并发访问
- [ ] 测试连接数限制
- [ ] 测试连接超时和清理
- [ ] 测试连接健康检查

### 21. 编写集成测试
- [ ] 搭建测试用 FastDFS 环境（Docker）
- [ ] 测试完整的上传流程
- [ ] 测试下载流程
- [ ] 测试删除流程
- [ ] 测试元数据操作
- [ ] 测试故障转移（停止一个 Tracker）
- [ ] 测试大文件上传/下载
- [ ] 测试并发场景

---

## 第八阶段：文档和发布

### 22. 编写使用示例和文档
- [ ] README.md 完善
  - 功能介绍
  - 快速开始
  - 配置说明
- [ ] 示例代码
  - DI 模式使用示例
  - 非 DI 模式使用示例
  - 配置文件示例
  - 常见场景示例（上传、下载、删除等）
- [ ] API 文档（XML 注释）

### 23. 性能测试和优化
- [ ] 使用 BenchmarkDotNet 进行性能测试
- [ ] 测试连接池效率
- [ ] 测试内存分配（减少 GC 压力）
- [ ] 优化大文件传输
- [ ] 使用 ArrayPool 优化缓冲区

### 24. 创建 NuGet 打包配置
- [ ] 配置 .csproj 元数据
  - PackageId
  - Version
  - Authors
  - Description
  - Repository URL
- [ ] 添加 Icon 和 License
- [ ] 生成 NuGet 包
- [ ] 发布到 NuGet.org

---

## 可选增强功能

### 扩展功能（按需实现）
- [ ] 支持 HTTPS/TLS 连接
- [ ] 支持文件秒传（先查询后上传）
- [ ] 支持断点续传
- [ ] 支持 Token 认证
- [ ] 支持主从文件上传
- [ ] 支持修改文件
- [ ] 支持图片处理（如果 Storage 支持）
- [ ] 添加 Polly 重试策略集成
- [ ] 添加分布式追踪支持（OpenTelemetry）
- [ ] 添加健康检查集成（IHealthCheck）

---

## 开发注意事项

1. **线程安全**: 连接池必须线程安全
2. **资源释放**: 确保连接、Stream 等资源正确释放
3. **异常处理**: 网络异常要优雅处理，避免连接泄漏
4. **性能优化**: 使用 ArrayPool、减少内存分配
5. **兼容性**: 确保 netstandard2.0 兼容性
6. **测试覆盖**: 核心功能要有充分的测试
7. **文档完善**: 公共 API 要有 XML 注释

---

## 里程碑

- **Milestone 1**: ✅ 完成基础架构和协议层（任务 1-4）**[已完成 2024-12-08]**
  - ✅ 项目结构搭建
  - ✅ 协议层完整实现（Header/Body、命令定义、请求/响应序列化）
  - ✅ 核心库零依赖架构

- **Milestone 2**: ⏳ 完成连接层（任务 5-6）**[进行中]**
  - ⏳ TCP 连接封装
  - ⏳ 连接池实现

- **Milestone 3**: 完成基础客户端功能（任务 7-12）
  - Tracker 客户端
  - Storage 客户端（上传、下载、删除、查询、元数据）

- **Milestone 4**: 完成配置和 DI 集成（任务 13-17）
  - 配置类和验证
  - IOptions 模式
  - 多集群支持
  - 日志集成

- **Milestone 5**: 完成测试和文档（任务 18-23）
  - 单元测试
  - 集成测试
  - 使用文档

- **Milestone 6**: 发布 v1.0.0（任务 24-25）
  - 性能优化
  - NuGet 打包发布

---

## 多 FastDFS 集群架构设计

### 设计目标
支持在同一个应用中同时连接和操作多个独立的 FastDFS 集群，每个集群拥有独立的配置和连接池。

### 使用场景
1. **多数据中心部署**: 不同地域的 FastDFS 集群
2. **业务隔离**: 不同业务线使用不同的 FastDFS 集群
3. **主备集群**: 主集群和备份集群，支持故障切换
4. **读写分离**: 分别配置上传集群和下载集群

### 配置结构

**appsettings.json 示例：**
```json
{
  "FastDFS": {
    "Clusters": {
      "default": {
        "TrackerServers": ["192.168.1.100:22122", "192.168.1.101:22122"],
        "ConnectionPool": {
          "MaxConnectionPerServer": 50,
          "MinConnectionPerServer": 5,
          "ConnectionIdleTimeout": 300,
          "ConnectionLifetime": 3600
        },
        "NetworkTimeout": 30,
        "Charset": "UTF-8"
      },
      "backup": {
        "TrackerServers": ["192.168.2.100:22122"],
        "ConnectionPool": {
          "MaxConnectionPerServer": 30,
          "MinConnectionPerServer": 3,
          "ConnectionIdleTimeout": 300,
          "ConnectionLifetime": 3600
        },
        "NetworkTimeout": 30,
        "Charset": "UTF-8"
      },
      "cdn": {
        "TrackerServers": ["cdn.example.com:22122"],
        "ConnectionPool": {
          "MaxConnectionPerServer": 100,
          "MinConnectionPerServer": 10,
          "ConnectionIdleTimeout": 300,
          "ConnectionLifetime": 3600
        },
        "NetworkTimeout": 60,
        "Charset": "UTF-8"
      }
    }
  }
}
```

### DI 注册方式

**方式 1: 单个命名集群注册**
```csharp
services.AddFastDFS("default", options => {
    options.TrackerServers = new[] { "192.168.1.100:22122" };
});

services.AddFastDFS("backup", options => {
    options.TrackerServers = new[] { "192.168.2.100:22122" };
});
```

**方式 2: 从配置批量注册**
```csharp
services.AddFastDFS(configuration.GetSection("FastDFS"));
```

**方式 3: 单集群（默认名称）**
```csharp
services.AddFastDFS(options => {
    options.TrackerServers = new[] { "192.168.1.100:22122" };
});
// 自动注册为 "default" 名称
```

### 客户端使用

**通过工厂获取命名客户端：**
```csharp
public class FileService
{
    private readonly IFastDFSClient _defaultClient;
    private readonly IFastDFSClient _backupClient;
    private readonly IFastDFSClient _cdnClient;

    public FileService(IFastDFSClientFactory factory)
    {
        _defaultClient = factory.GetClient("default");
        _backupClient = factory.GetClient("backup");
        _cdnClient = factory.GetClient("cdn");
    }

    public async Task<string> UploadWithBackup(byte[] content, string extension)
    {
        try
        {
            // 尝试上传到主集群
            return await _defaultClient.UploadAsync(content, extension);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "主集群上传失败，切换到备份集群");
            // 失败后切换到备份集群
            return await _backupClient.UploadAsync(content, extension);
        }
    }

    public async Task<byte[]> DownloadFromCDN(string fileId)
    {
        // 从 CDN 集群下载
        return await _cdnClient.DownloadAsync(fileId);
    }
}
```

**直接注入默认客户端（单集群场景）：**
```csharp
public class SimpleFileService
{
    private readonly IFastDFSClient _client;

    public SimpleFileService(IFastDFSClient client)
    {
        _client = client; // 自动注入 "default" 客户端
    }

    public async Task<string> Upload(byte[] content, string extension)
    {
        return await _client.UploadAsync(content, extension);
    }
}
```

### 连接池隔离

每个命名客户端拥有独立的连接池实例：
- `default` 集群的连接池管理 `192.168.1.100:22122` 的连接
- `backup` 集群的连接池管理 `192.168.2.100:22122` 的连接
- `cdn` 集群的连接池管理 `cdn.example.com:22122` 的连接

各集群的连接池互不干扰，可以独立配置最大连接数、超时等参数。

### 实现要点

1. **IFastDFSClientFactory**：管理所有命名客户端的工厂
2. **命名注册**: 使用 `IOptionsMonitor<FastDFSOptions>` 支持多个命名配置
3. **延迟初始化**: 客户端在首次 `GetClient(name)` 时才创建
4. **线程安全**: 工厂内部使用 `ConcurrentDictionary` 存储客户端实例
5. **资源释放**: 工厂实现 `IDisposable`，释放所有客户端和连接池

### 命名约定

- `"default"` - 默认集群名称（使用无名称的 AddFastDFS 时自动使用）
- 其他名称 - 自定义，建议使用小写，如 `"backup"`, `"cdn"`, `"region-us"` 等

---

## 开发日志

### 2024-12-08

**完成的工作：**

#### Milestone 1 完成 ✅ - 基础架构和协议层

1. **项目架构搭建**
   - 创建了核心库 `FastDFS.Client`（零依赖，netstandard2.0）
   - 创建了扩展库 `FastDFS.Client.DependencyInjection`（提供 DI、IOptions 支持）
   - 采用中央包管理（Directory.Packages.props）
   - 实现了核心库与扩展库分离的架构，确保最大兼容性

2. **协议层完整实现**
   
   **工具类：**
   - `ByteConverter` - 大端序/小端序转换（支持 Int32/Int64）
   - `ByteExtensions` - 字节数组扩展方法（固定长度字符串读写、编码转换）
   
   **协议基础：**
   - `FastDFSHeader` - 10字节协议头（8字节长度 + 1字节命令 + 1字节状态）
   - `FastDFSPacket` - 数据包基类
   - `IFastDFSRequest` / `IFastDFSResponse` - 请求/响应接口
   - `FastDFSRequest<TResponse>` / `FastDFSResponse` - 泛型基类
   
   **命令和常量：**
   - `TrackerCommand` - Tracker 服务器命令定义（101-107, 91-95）
   - `StorageCommand` - Storage 服务器命令定义（11-42）
   - `FastDFSConstants` - 协议常量（端口、长度限制等）
   - `FastDFSErrorCode` - 错误码枚举和扩展方法
   - `MetadataFlag` - 元数据操作标志
   
   **请求/响应实现：**
   
   Tracker 协议：
   - `QueryStoreWithoutGroupRequest/Response` - 查询可上传的 Storage 服务器
   - `QueryFetchRequest/Response` - 查询文件所在的 Storage 服务器
   
   Storage 协议：
   - `UploadFileRequest/Response` - 上传文件，返回 FileId
   - `DownloadFileRequest/Response` - 下载文件（支持偏移量和长度）
   - `DeleteFileRequest/Response` - 删除文件
   - `QueryFileInfoRequest/Response` - 查询文件信息（大小、时间、CRC32）
   
   **模型类：**
   - `StorageServerInfo` - Storage 服务器信息（组名、IP、端口、存储路径索引）
   - `FastDFSFileInfo` - 文件信息（大小、创建时间、CRC32、源IP）

**技术亮点：**
- ✅ 完整的二进制协议序列化/反序列化
- ✅ 类型安全的泛型请求/响应模式
- ✅ 正确的大端序处理
- ✅ 固定长度字段的正确填充和解析
- ✅ 零外部依赖的核心库设计

**代码统计：**
- 已创建文件：约 25+ 个核心文件
- 代码行数：约 2000+ 行
- 编译状态：✅ 0 错误 0 警告

**下一步计划：**
- 开始实现连接层（TCP 连接封装和连接池）

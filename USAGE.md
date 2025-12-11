# FastDFS .NET Client SDK 使用指南

## 安装

```bash
# 安装核心库（无依赖）
dotnet add package FastDFS.Client

# 安装 DI 扩展（如果需要依赖注入）
dotnet add package FastDFS.Client.DependencyInjection
```

## 快速开始

### 方式 1: 使用依赖注入 (推荐)

#### 1.1 单集群配置

**Program.cs / Startup.cs:**

```csharp
using FastDFS.Client.DependencyInjection;

// 方式 A: 代码配置
services.AddFastDFS(options =>
{
    options.TrackerServers = new List<string>
    {
        "192.168.1.100:22122",
        "192.168.1.101:22122"
    };
    options.ConnectionPool.MaxConnectionPerServer = 50;
    options.ConnectionPool.MinConnectionPerServer = 5;
});

// 方式 B: 从配置文件读取
services.AddFastDFS(Configuration.GetSection("FastDFS"));
```

**appsettings.json:**

```json
{
  "FastDFS": {
    "TrackerServers": [
      "192.168.1.100:22122",
      "192.168.1.101:22122"
    ],
    "ConnectionPool": {
      "MaxConnectionPerServer": 50,
      "MinConnectionPerServer": 5,
      "ConnectionIdleTimeout": 300,
      "ConnectionLifetime": 3600,
      "ConnectionTimeout": 30000,
      "SendTimeout": 30000,
      "ReceiveTimeout": 30000
    },
    "NetworkTimeout": 30,
    "Charset": "UTF-8"
  }
}
```

**使用客户端:**

```csharp
using FastDFS.Client;

public class FileService
{
    private readonly IFastDFSClient _client;

    public FileService(IFastDFSClient client)
    {
        _client = client;
    }

    public async Task<string> UploadFileAsync(byte[] content, string extension)
    {
        // 上传文件，返回 fileId (例如: "group1/M00/00/00/xxx.jpg")
        var fileId = await _client.UploadAsync(null, content, extension);
        return fileId;
    }

    public async Task<byte[]> DownloadFileAsync(string fileId)
    {
        // 下载文件
        return await _client.DownloadAsync(fileId);
    }

    public async Task DeleteFileAsync(string fileId)
    {
        // 删除文件
        await _client.DeleteAsync(fileId);
    }
}
```

#### 1.2 多集群配置

**appsettings.json:**

```json
{
  "FastDFS": {
    "Clusters": {
      "default": {
        "TrackerServers": ["192.168.1.100:22122"]
      },
      "backup": {
        "TrackerServers": ["192.168.2.100:22122"]
      },
      "cdn": {
        "TrackerServers": ["cdn.example.com:22122"]
      }
    }
  }
}
```

**Program.cs:**

```csharp
services.AddFastDFS(Configuration.GetSection("FastDFS"));

// 或者代码配置多个集群
services.AddFastDFS("default", options => {
    options.TrackerServers = new[] { "192.168.1.100:22122" };
});

services.AddFastDFS("backup", options => {
    options.TrackerServers = new[] { "192.168.2.100:22122" };
});
```

**使用多个客户端:**

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

    public async Task<string> UploadWithBackupAsync(byte[] content, string extension)
    {
        try
        {
            // 尝试上传到主集群
            return await _defaultClient.UploadAsync(null, content, extension);
        }
        catch (Exception ex)
        {
            // 失败后切换到备份集群
            _logger.LogWarning(ex, "主集群上传失败，切换到备份集群");
            return await _backupClient.UploadAsync(null, content, extension);
        }
    }

    public async Task<byte[]> DownloadFromCDNAsync(string fileId)
    {
        // 从 CDN 集群下载
        return await _cdnClient.DownloadAsync(fileId);
    }
}
```

#### 1.3 运行时动态注册客户端

对于需要在程序运行时才能获取配置的场景（例如从数据库、配置中心等动态加载），可以使用 `RegisterClient` 方法：

**场景示例：从数据库加载配置**

```csharp
using FastDFS.Client;
using FastDFS.Client.Configuration;

public class DynamicClusterService
{
    private readonly IFastDFSClientFactory _factory;
    private readonly ILogger<DynamicClusterService> _logger;

    public DynamicClusterService(IFastDFSClientFactory factory, ILogger<DynamicClusterService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    // 从数据库动态加载并注册新集群
    public async Task<IFastDFSClient> RegisterClusterFromDatabaseAsync(string tenantId)
    {
        // 从数据库获取租户的 FastDFS 配置
        var clusterConfig = await GetClusterConfigFromDatabaseAsync(tenantId);

        // 创建配置对象
        var configuration = new FastDFSConfiguration
        {
            TrackerServers = clusterConfig.TrackerServers.ToList(),
            ConnectionPool = new ConnectionPoolConfiguration
            {
                MaxConnectionPerServer = clusterConfig.MaxConnections,
                MinConnectionPerServer = clusterConfig.MinConnections,
                ConnectionIdleTimeout = 300,
                ConnectionLifetime = 3600
            },
            NetworkTimeout = 30,
            Charset = "UTF-8"
        };

        // 动态注册客户端（如果已存在则会被替换）
        var client = _factory.RegisterClient($"tenant_{tenantId}", configuration);

        _logger.LogInformation("Successfully registered FastDFS client for tenant {TenantId}", tenantId);

        return client;
    }

    // 获取或注册客户端
    public async Task<IFastDFSClient> GetOrRegisterClientAsync(string tenantId)
    {
        var clientName = $"tenant_{tenantId}";

        // 检查客户端是否已注册
        if (_factory.HasClient(clientName))
        {
            return _factory.GetClient(clientName);
        }

        // 如果不存在，则动态注册
        return await RegisterClusterFromDatabaseAsync(tenantId);
    }

    // 移除不再使用的客户端
    public void RemoveClient(string tenantId)
    {
        var clientName = $"tenant_{tenantId}";
        if (_factory.RemoveClient(clientName))
        {
            _logger.LogInformation("Removed FastDFS client for tenant {TenantId}", tenantId);
        }
    }

    private async Task<ClusterConfig> GetClusterConfigFromDatabaseAsync(string tenantId)
    {
        // 实际实现：从数据库查询配置
        // 这里仅作示例
        await Task.CompletedTask;
        return new ClusterConfig
        {
            TrackerServers = new[] { "192.168.1.100:22122" },
            MaxConnections = 50,
            MinConnections = 5
        };
    }
}

// 配置数据模型
public class ClusterConfig
{
    public string[] TrackerServers { get; set; } = Array.Empty<string>();
    public int MaxConnections { get; set; }
    public int MinConnections { get; set; }
}
```

**使用示例：**

```csharp
public class FileUploadController : ControllerBase
{
    private readonly DynamicClusterService _clusterService;

    public FileUploadController(DynamicClusterService clusterService)
    {
        _clusterService = clusterService;
    }

    [HttpPost("upload/{tenantId}")]
    public async Task<IActionResult> UploadFile(string tenantId, IFormFile file)
    {
        // 获取或注册租户专属的 FastDFS 客户端
        var client = await _clusterService.GetOrRegisterClientAsync(tenantId);

        using var stream = file.OpenReadStream();
        var fileId = await client.UploadAsync(null, stream, Path.GetExtension(file.FileName));

        return Ok(new { FileId = fileId });
    }
}
```

**启动时配置（可选）：**

如果启动时不需要注册任何客户端，只需要注册 Factory 即可：

```csharp
// Program.cs
using FastDFS.Client.DependencyInjection;

// 只注册 Factory，不注册任何客户端
// 后续通过 RegisterClient 动态注册
services.TryAddSingleton<IFastDFSClientFactory, FastDFSClientFactory>();
services.AddSingleton<IOptionsMonitor<FastDFSConfiguration>, OptionsMonitor<FastDFSConfiguration>>();

// 或者也可以注册一个默认客户端，然后在运行时注册其他客户端
services.AddFastDFS("default", options => {
    options.TrackerServers = new[] { "192.168.1.100:22122" };
});
```

**关键特性：**

- **运行时注册**：无需在启动时知道所有配置，可在程序运行时动态注册
- **配置来源灵活**：支持从数据库、配置中心、API 等任何来源获取配置
- **替换现有客户端**：如果客户端名称已存在，`RegisterClient` 会先释放旧客户端再创建新的
- **移除客户端**：使用 `RemoveClient` 可以释放不再使用的客户端资源
- **线程安全**：所有操作都是线程安全的，可在多线程环境中安全使用

**适用场景：**

- **多租户 SaaS 应用**：每个租户使用独立的 FastDFS 集群
- **动态配置中心**：从 Apollo、Nacos 等配置中心动态加载配置
- **配置热更新**：在运行时更新集群配置而无需重启应用
- **按需加载**：只在需要时才加载特定集群的配置，减少启动时间

### 方式 2: 非 DI 模式

```csharp
using FastDFS.Client;

// 创建单个客户端
var client = FastDFSClientBuilder.CreateClient("192.168.1.100:22122");

// 或者提供多个 Tracker
var client = FastDFSClientBuilder.CreateClient(
    new[] { "192.168.1.100:22122", "192.168.1.101:22122" },
    poolOptions =>
    {
        poolOptions.MaxConnectionPerServer = 50;
        poolOptions.MinConnectionPerServer = 5;
    }
);

// 使用客户端
var fileId = await client.UploadAsync(null, fileContent, "jpg");
var content = await client.DownloadAsync(fileId);
await client.DeleteAsync(fileId);
```

**多集群管理:**

```csharp
using FastDFS.Client;

var manager = new FastDFSClientManager();

// 添加多个集群配置
manager.AddClient("default", new[] { "192.168.1.100:22122" });
manager.AddClient("backup", new[] { "192.168.2.100:22122" });
manager.AddClient("cdn", new[] { "cdn.example.com:22122" });

// 获取客户端
var defaultClient = manager.GetClient("default");
var backupClient = manager.GetClient("backup");
var cdnClient = manager.GetClient("cdn");

// 使用完毕记得释放
manager.Dispose();
```

## API 文档

### 上传操作

```csharp
// 从字节数组上传
Task<string> UploadAsync(string? groupName, byte[] content, string fileExtension);

// 从流上传
Task<string> UploadAsync(string? groupName, Stream stream, string fileExtension);

// 从本地文件上传
Task<string> UploadFileAsync(string? groupName, string localFilePath);

// 上传可追加文件
Task<string> UploadAppenderFileAsync(string? groupName, byte[] content, string fileExtension);

// 追加数据到文件
Task AppendFileAsync(string fileId, byte[] content);
```

### 下载操作

```csharp
// 下载为字节数组
Task<byte[]> DownloadAsync(string fileId);

// 下载到流
Task DownloadAsync(string fileId, Stream outputStream);

// 下载到本地文件
Task DownloadFileAsync(string fileId, string localFilePath);

// 分片下载
Task<byte[]> DownloadAsync(string fileId, long offset, long length);
```

### 删除操作

```csharp
// 删除文件
Task DeleteAsync(string fileId);
```

### 查询操作

```csharp
// 查询文件信息
Task<FastDFSFileInfo> QueryFileInfoAsync(string fileId);

// 检查文件是否存在
Task<bool> FileExistsAsync(string fileId);
```

### 元数据操作

```csharp
// 设置文件元数据
Task SetMetadataAsync(string fileId, FastDFSMetadata metadata, MetadataFlag flag = MetadataFlag.Overwrite);
Task SetMetadataAsync(string? groupName, string fileName, FastDFSMetadata metadata, MetadataFlag flag = MetadataFlag.Overwrite);

// 获取文件元数据
Task<FastDFSMetadata> GetMetadataAsync(string fileId);
Task<FastDFSMetadata> GetMetadataAsync(string? groupName, string fileName);
```

### Tracker 查询操作 (高级)

```csharp
// 查询可上传的 Storage 服务器
Task<StorageServerInfo> QueryStorageForUploadAsync(string? groupName = null);

// 查询文件所在的 Storage 服务器
Task<StorageServerInfo> QueryStorageForDownloadAsync(string fileId);

// 查询可修改文件的 Storage 服务器
Task<StorageServerInfo> QueryStorageForUpdateAsync(string fileId);
```

## FileId 格式说明

FastDFS 的 FileId 有两种格式：

1. **完整格式** (包含组名): `"group1/M00/00/00/xxx.jpg"`
2. **简单格式** (不含组名): `"M00/00/00/xxx.jpg"`

SDK 会自动处理这两种格式：

```csharp
// 上传返回完整格式 FileId
var fileId = await client.UploadAsync(content, "jpg");
// fileId = "group1/M00/00/00/wKgBaFxxx.jpg"

// 下载时自动识别格式
var content = await client.DownloadAsync(fileId);

// 也支持简单格式（需要指定 groupName）
var content = await storageClient.DownloadAsync("group1", "M00/00/00/xxx.jpg");
```

## 高级用法

### Tracker 查询 (高级控制)

如果你需要更细粒度的控制，可以直接查询 Tracker 获取 Storage 服务器信息：

```csharp
// 查询可上传的 Storage 服务器
var storageInfo = await client.QueryStorageForUploadAsync();
Console.WriteLine($"Upload to: {storageInfo.IpAddress}:{storageInfo.Port}");
Console.WriteLine($"Group: {storageInfo.GroupName}");
Console.WriteLine($"Store Path Index: {storageInfo.StorePathIndex}");

// 查询文件所在的 Storage 服务器（用于下载）
var downloadStorage = await client.QueryStorageForDownloadAsync(fileId);
Console.WriteLine($"Download from: {downloadStorage.IpAddress}:{downloadStorage.Port}");

// 查询可修改文件的 Storage 服务器（用于删除/更新）
var updateStorage = await client.QueryStorageForUpdateAsync(fileId);
Console.WriteLine($"Update on: {updateStorage.IpAddress}:{updateStorage.Port}");
```

**使用场景：**
- 负载监控：获取 Storage 服务器分布信息
- 智能路由：根据 Storage 服务器位置选择最近的节点
- 故障诊断：检查文件实际存储位置
- 手动控制：自己实现上传/下载逻辑（配合 IStorageClient）

### Appender 文件 (日志文件场景)

```csharp
// 上传 appender 文件
var fileId = await client.UploadAppenderFileAsync(null, initialContent, "log");

// 追加数据
await client.AppendFileAsync(fileId, additionalContent1);
await client.AppendFileAsync(fileId, additionalContent2);
```

### 分片下载大文件

```csharp
var fileInfo = await client.QueryFileInfoAsync(fileId);
long fileSize = fileInfo.FileSize;

// 分片下载
long chunkSize = 1024 * 1024; // 1MB
for (long offset = 0; offset < fileSize; offset += chunkSize)
{
    long length = Math.Min(chunkSize, fileSize - offset);
    var chunk = await client.DownloadAsync(fileId, offset, length);
    // 处理分片数据...
}
```

### 从本地文件上传

```csharp
// 自动检测文件扩展名
var fileId = await client.UploadFileAsync(null, @"C:\images\photo.jpg");

// 下载到本地文件
await client.DownloadFileAsync(fileId, @"C:\downloads\photo.jpg");
```

### 文件元数据 (自定义属性)

FastDFS 支持为文件附加自定义元数据（key-value 键值对），常用于存储文件的额外信息：

```csharp
using FastDFS.Client;

// 上传文件
var fileId = await client.UploadAsync(null, imageContent, "jpg");

// 创建元数据
var metadata = new FastDFSMetadata();
metadata.Add("author", "张三");
metadata.Add("description", "产品图片");
metadata.Add("created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
metadata.Add("width", "1920");
metadata.Add("height", "1080");

// 设置元数据（覆盖模式）
await client.SetMetadataAsync(fileId, metadata, MetadataFlag.Overwrite);

// 获取元数据
var retrievedMetadata = await client.GetMetadataAsync(fileId);
Console.WriteLine($"作者: {retrievedMetadata["author"]}");
Console.WriteLine($"描述: {retrievedMetadata["description"]}");
Console.WriteLine($"元数据数量: {retrievedMetadata.Count}");

// 遍历所有元数据
foreach (var key in retrievedMetadata.Keys)
{
    Console.WriteLine($"{key}: {retrievedMetadata[key]}");
}

// 更新元数据（合并模式）- 只更新指定的键，不影响其他键
var updateMetadata = new FastDFSMetadata();
updateMetadata.Add("modified_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
updateMetadata.Add("modified_by", "李四");
await client.SetMetadataAsync(fileId, updateMetadata, MetadataFlag.Merge);
```

**元数据标志：**
- `MetadataFlag.Overwrite`: 覆盖模式，完全替换现有元数据
- `MetadataFlag.Merge`: 合并模式，添加或更新指定的键，保留未指定的键

**使用场景：**
- 存储图片/视频的分辨率、拍摄时间、作者等信息
- 记录文件的上传者、上传时间、业务 ID
- 保存文件的审核状态、标签、分类等业务属性
- 存储文件的 MD5、SHA1 等校验信息

## 错误处理

```csharp
using FastDFS.Client.Exceptions;

try
{
    var content = await client.DownloadAsync(fileId);
}
catch (FastDFSException ex) when (ex.ErrorCode == 2)
{
    // 文件不存在 (ENOENT)
    Console.WriteLine("文件未找到");
}
catch (FastDFSNetworkException ex)
{
    // 网络错误
    Console.WriteLine($"网络错误: {ex.RemoteEndpoint}");
}
catch (FastDFSProtocolException ex)
{
    // 协议错误
    Console.WriteLine($"协议错误: {ex.Message}");
}
```

## 连接池配置

```csharp
services.AddFastDFS(options =>
{
    options.ConnectionPool.MaxConnectionPerServer = 100;  // 最大连接数
    options.ConnectionPool.MinConnectionPerServer = 10;   // 最小连接数（预热）
    options.ConnectionPool.ConnectionIdleTimeout = 300;   // 空闲超时（秒）
    options.ConnectionPool.ConnectionLifetime = 3600;     // 最大生命周期（秒）
    options.ConnectionPool.ConnectionTimeout = 30000;     // 连接超时（毫秒）
    options.ConnectionPool.SendTimeout = 30000;           // 发送超时（毫秒）
    options.ConnectionPool.ReceiveTimeout = 30000;        // 接收超时（毫秒）
});
```

## 最佳实践

1. **单例模式**: 在 DI 中，`IFastDFSClient` 和 `IFastDFSClientFactory` 都是单例，无需手动管理生命周期
2. **连接池**: SDK 内置连接池，无需每次创建新连接
3. **异常处理**: 始终捕获 `FastDFSException` 及其子类
4. **FileId 存储**: 上传后保存返回的完整 FileId（包含组名），便于后续操作
5. **多集群**: 生产环境建议配置主备集群，实现故障转移
6. **超时设置**: 根据网络状况和文件大小调整超时参数

## 示例项目

完整示例请参考 `samples/FastDFS.Client.Samples` 项目。

## 许可证

MIT License

# SharpDevFramework AI 开发指南

> 本文档专门面向 AI 代码助手编写，包含精确的 API 签名、类型约束、模块依赖、代码模板和注意事项，确保 AI 能正确使用框架生成代码。

---

## 1. 概述与架构总览

### 1.1 框架定位

SharpDevFramework 是基于 **.NET 10.0 / C# 13.0** 的全栈 Web 开发框架，以 NuGet 包分发，提供开箱即用的认证、后台任务、实时通信、操作日志等能力。

### 1.2 项目组成

| 项目 | 类型 | 说明 |
|------|------|------|
| `SharpDevFramework` | 类库 (NuGet) | 框架核心，版本 1.0.5 |
| `SharpDevFramework.Demo` | Web 应用 | 演示项目，引用框架 NuGet 包 |
| `SharpDevFramework.Template` | NuGet 模板 | `dotnet new sharpdev` 项目模板 |

### 1.3 技术栈与依赖

| 依赖 | 版本 | 用途 |
|------|------|------|
| .NET | 10.0 | 运行时 |
| Mapster | 10.0.7 | 对象映射（`entity.Adapt<Dto>()`） |
| Serilog.AspNetCore | 10.0.0 | 结构化日志 |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.7 | SQLite ORM |
| SharpDevLib | 2.1.4 | 基础工具库（JWT、序列化、随机数等） |
| Microsoft.Extensions.Hosting.WindowsServices | 10.0.7 | Windows 服务支持 |

**关键说明**：
- 响应类型 `EmptyReply`、`DataReply<T>`、`PageReply<T>`、`PageRequest` 来自 **SharpDevLib** 包
- JWT 操作 `JwtHelper.CreateWithHmacSha256` / `JwtHelper.VerifyWithHmacSha256` 来自 **SharpDevLib** 包
- 扩展方法如 `.ToUtcTimestamp()`、`.CombinePath()`、`.SplitToList()`、`.NullOrWhiteSpace()`、`.IsNullOrEmpty()` 来自 **SharpDevLib** 包

### 1.4 请求处理管线

```
HTTP 请求
  → AuthFilter（JWT 验证，角色检查，Special Token 限制）
  → OperationLogFilter（记录操作日志）
  → ExceptionFilter（全局异常捕获）
  → Controller Action
  → 响应
```

### 1.5 核心数据流

```
请求 → AuthFilter 验证 JWT → 将 JwtPayload 存入 HttpContext.Items["payload"]
     → OperationLogFilter 记录请求信息 → 执行 Action → 记录响应信息
     → 操作日志每 2 分钟批量写入数据库

Controller 中 → 创建 TaskEntity 保存到数据库 → taskCenter.PublishAsync(taskId)
             → TaskCenter 从 Channel 读取 → 实例化 BaseTask 子类 → HandleAsync
             → 状态变更 → SignalR 广播 TaskUpdated → 前端实时更新
```

---

## 2. 启动与配置

### 2.1 框架入口方法（精确签名）

```csharp
// 命名空间：SharpDevFramework
// 文件：FrameworkExtensions.cs

// 注册所有框架服务
public static WebApplicationBuilder AddSharpDevFramework<TDbContext>(
    this WebApplicationBuilder builder,
    Assembly[] assemblies
) where TDbContext : FrameworkDbContext

// 启用所有框架中间件
public static WebApplication UseSharpDevFramework(
    this WebApplication app,
    Assembly[] assemblies
)
```

**泛型约束**：`TDbContext` 必须继承自 `FrameworkDbContext`（抽象类）。

**assemblies 参数**：框架扫描这些程序集来发现 Controller、BaseTask 子类、MockEnum 类、标记接口实现类。通常传入 `[Assembly.GetExecutingAssembly()]`。框架内部会自动合并自身程序集。

### 2.2 最小启动代码

```csharp
using SharpDevFramework;
using YourProject.Data;
using System.Reflection;

Assembly[] assemblies = [Assembly.GetExecutingAssembly()];

var builder = WebApplication.CreateBuilder(args);
builder.AddSharpDevFramework<AppDbContext>(assemblies);

var app = builder.Build();
app.UseSharpDevFramework(assemblies);
app.Run();
```

### 2.3 AddSharpDevFramework 内部注册顺序

| 步骤 | 方法 | 功能 | 配置开关 |
|------|------|------|----------|
| 1 | `AddSettings` | 加载配置文件链 | 无 |
| 2 | `AddSerialog` | Serilog 结构化日志 | `Serilog:IsEnabled` |
| 3 | `ConfigSizeLimit` | 请求体大小限制 | `FrameworkSettings:RequestSizeLimit` |
| 4 | `AddCors` | CORS 跨域策略 | `FrameworkSettings:Cors:IsEnabled` |
| 5 | `AddFilters` | AuthFilter + ExceptionFilter + OperationLogFilter | 各自独立开关 |
| 6 | `AddWindowsService` | Windows 服务支持 | `FrameworkSettings:UseWindowsService` |
| 7 | `AddDbContext<TDbContext>` | EF Core SQLite | 无 |
| 8 | `AddServices` | 自动 DI 注册 + HttpClient + HttpContextAccessor | 无 |
| 9 | `AddSignalR` | SignalR 实时通信 | `FrameworkSettings:UseSignalR` |
| 10 | `AddTasks` | 后台任务托管服务 | `FrameworkSettings:Tasks:IsEnabled` |

### 2.4 UseSharpDevFramework 中间件顺序

| 步骤 | 操作 | 说明 |
|------|------|------|
| 1 | `UseDefaultFiles` + `UseStaticFiles` | 静态文件服务（wwwroot） |
| 2 | `UseCors` | CORS 中间件 |
| 3 | `UseSignalR` | SignalR Hub 映射 |
| 4 | `UseEnums` | 枚举控制器初始化 |
| 5 | `MapControllers` | 控制器路由映射 |
| 6 | `SeedDatabase` | 数据库迁移 + 创建默认管理员 |

### 2.5 配置文件加载链（优先级从低到高）

1. `appsettings.json` — 应用基础配置（必选）
2. `appsettings.local.json` — 应用本地覆盖（可选，不提交 Git）
3. `framework.json` — 框架配置（可选，随 NuGet 包自动生成）
4. `framework.local.json` — 框架本地覆盖（可选，不提交 Git）

**重要**：`framework.json` 通过 MSBuild targets 在构建前自动从 NuGet 包复制到项目根目录（如不存在）。首次构建后可修改。

### 2.6 framework.json 完整配置 Schema

```json
{
  "Serilog": {
    "IsEnabled": true,                    // bool, 是否启用 Serilog
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",           // 日志级别
      "Override": { /* 命名空间级别覆盖 */ }
    },
    "Enrich": ["FromLogContext"],
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs.log",             // 会自动被框架覆盖为 LogsPath
          "rollOnFileSizeLimit": true,
          "fileSizeLimitBytes": 2097152,
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      },
      { "Name": "Console" }
    ]
  },
  "FrameworkSettings": {
    "UseExceptionFilter": true,           // bool, 全局异常过滤器
    "UseWindowsService": true,            // bool, Windows 服务支持
    "UseSignalR": true,                   // bool, SignalR 实时通信
    "HubUrl": "/hubs/notifications",      // string, SignalR Hub 端点路径
    "RequestSizeLimit": 10737418240,      // long, 请求体最大字节数 (默认 10GB)
    "JwtExpirationMinutes": 120,          // int, JWT Token 过期时间（分钟）
    "JwtSpecialExpirationHours": 12,      // int, Special Token 过期时间（小时）

    "Cors": {
      "IsEnabled": true,                  // bool, 是否启用 CORS
      "PolicyName": "DefaultCorsPolicy",  // string, 策略名称
      "Origins": ["http://127.0.0.1:*", "http://localhost:*"],  // string[], 允许的来源（支持通配符端口）
      "Methods": ["GET", "POST", "PUT", "DELETE"],              // string[], 允许的 HTTP 方法
      "Headers": [],                      // string[], 允许的请求头（空数组=允许所有）
      "AllowCredentials": true,           // bool, 是否允许凭证
      "ExposedHeaders": [],               // string[], 暴露的响应头
      "MaxAgeSeconds": 3600               // int, 预检缓存秒数（注意配置键为 MaxAge）
    },

    "Tasks": {
      "IsEnabled": true,                  // bool, 是否启用后台任务
      "MaxRetryCount": 3,                 // int, 最大重试次数
      "MaxProcessCount": 5                // int, 最大并发任务数
    },

    "Auth": {
      "IsEnabled": true,                  // bool, 是否启用认证过滤器
      "LockMinutes": 30,                  // int, 账户锁定时长（分钟）
      "MaxFailedCount": 10,               // int, 触发锁定的最大失败次数
      "FailedMinutes": 10                 // int, 失败计数重置窗口（分钟）
    },

    "OperationLog": {
      "IsEnabled": true                   // bool, 是否启用操作日志
    },

    "DataCleanup": {
      "IsEnabled": true,                  // bool, 是否启用数据清理
      "Tables": [
        {
          "Name": "UserOperationLogs",    // string, DbSet 属性名（非表名）
          "RetentionDays": 30             // int, 数据保留天数
        },
        {
          "Name": "Tasks",
          "RetentionDays": 30
        }
      ]
    }
  }
}
```

**注意**：CORS 配置中 `MaxAgeSeconds` JSON 键对应代码中的 `MaxAge`（`configuration.GetValue<int?>("FrameworkSettings:Cors:MaxAge")`）。

---

## 3. 数据库层

### 3.1 FrameworkDbContext（抽象基类）

```csharp
namespace SharpDevFramework;

public abstract class FrameworkDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<TaskEntity> Tasks { get; set; }
    public DbSet<UserOperationLogEntity> UserOperationLogs { get; set; }
}
```

**使用方式**：项目必须创建一个类继承 `FrameworkDbContext`，只需添加业务 DbSet：

```csharp
using Microsoft.EntityFrameworkCore;
using SharpDevFramework;

namespace YourProject.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : FrameworkDbContext(options)
{
    public DbSet<DemoEntity> Demos { get; set; }
}
```

**重要约束**：
- 泛型参数必须是自定义子类本身：`DbContextOptions<AppDbContext>`
- 该类被传入 `AddSharpDevFramework<AppDbContext>()`
- 框架自动注册 `FrameworkDbContext → TDbContext` 的 DI 映射，因此通过 `FrameworkDbContext` 也能访问自定义 DbSet
- 使用 SQLite，数据库文件路径：`data/db/database.db`

### 3.2 BaseEntity（抽象基类）

```csharp
namespace SharpDevFramework;

public abstract class BaseEntity
{
    public int Id { get; set; }                          // 自增主键
    public long CreatedAt { get; set; } = DateTime.Now.ToUtcTimestamp();  // UTC 时间戳（毫秒）
    public bool IsDeleted { get; set; }                  // 软删除标记
}
```

**关键点**：
- `CreatedAt` 类型是 `long`（UTC 毫秒时间戳），不是 `DateTime`
- `CreatedAt` 在对象创建时自动赋值
- 框架**不自动过滤**软删除数据，查询时需手动 `.Where(x => !x.IsDeleted)`

**自定义实体示例**：

```csharp
using SharpDevFramework;

namespace YourProject.Data.Entities;

public class DemoEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long? UpdatedAt { get; set; }
}
```

### 3.3 框架内置实体

**UserEntity**：`UserEntity : BaseEntity`
- `Name` (string) — 用户名
- `PasswordHash` (string) — 密码哈希
- `IsActive` (bool, 默认 true) — 是否激活
- `FailedLoginAttempts` (int) — 连续登录失败次数
- `LockoutUntil` (long?) — 锁定截止时间戳
- `LastFailedLoginTime` (long?) — 上次登录失败时间戳
- `LastLoginAt` (long?) — 上次登录时间戳
- `LastLoginIp` (string?) — 上次登录 IP
- `Role` (string) — 用户角色（逗号分隔多角色）

**TaskEntity**：`TaskEntity : BaseEntity`
- `UserId` (int) — 创建用户 ID
- `Type` (string) — 任务类型标识（对应 `[TaskRegister]` 的 Type）
- `Data` (string?) — 任务数据（JSON）
- `Status` (TaskStates) — 任务状态枚举
- `ErrorMessage` (string?) — 错误信息
- `RetryCount` (int) — 重试次数
- `UpdatedAt` (long) — 更新时间戳

**UserOperationLogEntity**：`UserOperationLogEntity : BaseEntity`
- `UserId` (int), `UserName` (string?), `OperationType` (string?), `ControllerName` (string?), `ActionName` (string?)
- `RoutePath` (string?), `HttpMethod` (string?), `IpAddress` (string?), `UserAgent` (string?)
- `RequestData` (string?), `ResponseData` (string?), `DurationMs` (long), `IsSuccess` (bool), `ErrorMessage` (string?)

### 3.4 DbHelper.BuildOrLikeExpression（精确签名）

```csharp
namespace SharpDevFramework;

public static class DbHelper
{
    public static Expression<Func<TEntity, bool>> BuildOrLikeExpression<TEntity>(
        string propertyName,
        string[] filters
    ) where TEntity : class
}
```

**用途**：动态构建多条件 OR LIKE 查询。对指定属性用逗号包裹后进行模糊匹配（`%,value,%`），适用于角色、类型等多选字段筛选。

**使用示例**：

```csharp
var roles = request.Role.SplitToList();  // SharpDevLib 扩展方法，按逗号分割
var predicate = DbHelper.BuildOrLikeExpression<UserEntity>("Role", [.. roles]);
query = query.Where(predicate);
```

### 3.5 迁移命令

```bash
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
```

框架启动时自动执行 `context.Database.Migrate()`，无需手动 `dotnet ef database update`。

### 3.6 数据库文件路径

| 路径常量 | 值 | 说明 |
|----------|-----|------|
| `SharpFrameworkStatics.DatabasePath` | `{root}/data/db/database.db` | SQLite 数据库文件 |
| `SharpFrameworkStatics.JwtSecretPath` | `{root}/data/db/jwt-secret.txt` | JWT 签名密钥 |

初始用户名/密码为root/12345678
---

## 4. 认证与授权

### 4.1 JWT 认证完整流程

1. 用户 `POST /api/users/login` 登录
2. 框架验证密码（ASP.NET Core Identity `PasswordHasher`），签发 JWT Token
3. 前端将 Token 存储在 `localStorage`
4. 后续请求通过 `Authorization: Bearer <token>` 头携带
5. 也可通过 `?access_token=<token>` QueryString 携带
6. `AuthFilter` 验证 Token，将 `JwtPayload` 存入 `HttpContext.Items["payload"]`
7. Controller 中通过 `HttpContext.GetJwtPayload()` 获取当前用户信息

### 4.2 TokenService（Singleton，实现 ISingletonService）

```csharp
namespace SharpDevFramework;

public class TokenService(IConfiguration configuration) : ISingletonService
{
    // 生成标准 JWT Token（有效期按分钟，配置项 FrameworkSettings:JwtExpirationMinutes，默认 120）
    public string GenerateToken(int userId, string username, string role)

    // 生成 Special Token（有效期按小时，配置项 FrameworkSettings:JwtSpecialExpirationHours，默认 24）
    // Special Token 的 Type 为 "special"，只能访问标记 [SpecialToken] 的接口
    public string GenerateSpecialToken(int userId, string username, string role)

    // 验证 JWT Token 签名和过期时间，返回解析后的负载
    // Token 无效或已过期时抛出 Exception
    public JwtPayload VerifyToken(string token)
}
```

**JWT 密钥管理**：密钥首次使用时自动生成（255 字符混合随机字符串），持久化到 `data/db/jwt-secret.txt`。标准 Token 和 Special Token 共用同一密钥。

### 4.3 JwtPayload

```csharp
namespace SharpDevFramework;

public class JwtPayload
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public long Exp { get; set; }          // UTC Unix 秒时间戳
    public string Role { get; set; } = string.Empty;  // 逗号分隔多角色
    public string Type { get; set; } = string.Empty;   // 空/""/"api" = 普通 Token, "special" = Special Token
}
```

### 4.4 AuthFilter 行为规则

**跳过条件**（以下请求不进行认证）：
- 标记了 `[AllowAnonymous]` 的端点
- 路径以 `/wwwroot/`、`/hub/`、`/signalr/` 开头的请求

**Token 获取来源**（按优先级）：
1. `Authorization` 请求头（格式：`Bearer <token>`）
2. `access_token` QueryString 参数

**认证后行为**：
- 将 `JwtPayload` 存入 `HttpContext.Items["payload"]`
- Special Token（`Type == "special"`）只能访问标记了 `[SpecialToken]` 特性的端点，未标记则返回 401
- 检查端点上的 `[Role]` 特性，用户角色列表中必须至少匹配一个

**认证失败响应**：HTTP 401（无 JSON 响应体，是 `StatusCodeResult`）
**角色不匹配响应**：HTTP 403（`StatusCodeResult`）

### 4.5 角色与权限特性

```csharp
// 标记 Controller 或 Action 所需的角色（OR 逻辑，匹配任一即可）
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RoleAttribute(string[] roles) : Attribute
{
    public string[] Roles { get; } = roles;
}

// 用法示例
[Role([UserRoleTypes.Admin])]
public EmptyReply AdminOnly() { ... }

// 标记接口允许 Special Token 访问
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SpecialTokenAttribute : Attribute

// 用法示例
[SpecialToken]
[HttpGet("special-data")]
public DataReply<string> SpecialEndpoint() { ... }

// 允许匿名访问（跳过 AuthFilter）
[AllowAnonymous]
[HttpGet("public")]
public EmptyReply PublicEndpoint() { ... }
```

### 4.6 AuthExtensions.GetJwtPayload()

```csharp
namespace SharpDevFramework;

public static class AuthExtensions
{
    // 从 HttpContext.Items["payload"] 提取 JwtPayload
    // 若负载不存在则抛出 Exception
    public static JwtPayload GetJwtPayload(this HttpContext httpContext)
}
```

### 4.7 账户锁定机制

- 在 `FailedMinutes`（默认 10）分钟时间窗口内连续失败 `MaxFailedCount`（默认 10）次后，账户锁定 `LockMinutes`（默认 30）分钟
- 超出失败窗口后失败计数重置为 0
- 登录成功后失败计数和锁定状态清除
- 锁定期间登录返回错误信息（含剩余秒数）

### 4.8 内置角色常量

```csharp
namespace SharpDevFramework;

[MockEnum(nameof(UserRoleTypes))]
public class UserRoleTypes
{
    [Description("管理员")]
    public const string Admin = nameof(Admin);  // 值为 "Admin"
}
```

扩展角色方式：创建 `UserRoleTypesExtend` 类，用 `[MockEnum("UserRoleTypes")]` 标记（MockEnumName 必须与框架内置值 `UserRoleTypes` 完全一致），值会合并到同一类别。

### 4.9 UsersController API 端点

路由前缀：`api/Users`

| HTTP | 路由 | 认证 | 方法签名 | 说明 |
|------|------|------|----------|------|
| POST | `login` | 匿名 | `Task<DataReply<LoginResponse>> Login(LoginRequest)` | 登录 |
| POST | `special-token` | 已认证 | `DataReply<string> SpecialToken()` | 生成 Special Token |
| GET | `/` | 已认证 | `PageReply<UserDto> GetPage(UserPageRequest)` | 分页查询（Admin 看全部，普通用户只看自己） |
| GET | `/{id}` | 已认证 | `DataReply<UserDto> Get(int id)` | 获取详情（Admin 或本人） |
| POST | `/` | Admin | `EmptyReply Create(CreateUserRequest)` | 创建用户 |
| PUT | `/{id}` | 已认证 | `EmptyReply Update(int id, UpdateUserRequest)` | 更新用户（不可修改自己角色） |
| DELETE | `/{id}` | Admin | `EmptyReply Delete(int id)` | 软删除（不可删除自己） |

**DTO 类型**：

```csharp
public class LoginRequest { public string? Username; public string? Password; }
public class LoginResponse { public int UserId; public string Username; public string Token; public string Role; }
public class UserPageRequest : PageRequest { public string? Name; public string? Role; }
public class CreateUserRequest { public string Name; public string Password; public string Role; }
public class UpdateUserRequest { public string? Name; public string? Password; public string Role; public bool IsActive; }
public class UserDto { public int Id; public string Name; public long CreatedAt; public bool IsActive; public int FailedLoginAttempts; public long? LockoutUntil; public long? LastFailedLoginTime; public long? LastLoginAt; public string? LastLoginIp; public string Role; }
```

### 4.10 UserOperationLogsController API 端点

路由前缀：`api/UserOperationLogs`，仅 Admin 可访问。

| HTTP | 路由 | 方法签名 | 说明 |
|------|------|----------|------|
| GET | `/` | `PageReply<UserOperationLogDto> GetPage(PageUserOperationLogRequest)` | 分页查询 |
| GET | `/{id}` | `DataReply<UserOperationLogDto> Get(int id)` | 获取详情 |

---

## 5. 后台任务系统

### 5.1 BaseTask（抽象基类，实现 IScopedService）

```csharp
namespace SharpDevFramework;

public abstract class BaseTask : IScopedService
{
    protected readonly IServiceProvider _serviceProvider;
    protected readonly ILogger _logger;
    private protected readonly FrameworkDbContext _dbContext;  // 注意：private protected
    protected readonly TaskCenter _taskCenter;
    protected readonly IServiceScope _serviceScope;

    protected BaseTask(IServiceProvider serviceProvider)

    // 任务执行入口（公开方法），自动管理状态流转和异常处理
    public async Task HandleAsync(int taskId, CancellationToken cancellationToken)

    // 是否自动标记完成（默认 true），子类可 override 返回 false
    protected virtual bool AutoComplete { get; } = true;

    // 子类必须实现的核心处理逻辑
    protected abstract Task ProcessAsync(int id, CancellationToken cancellationToken);
}
```

**HandleAsync 执行流程**：
1. 从数据库获取 TaskEntity
2. 调用 `TaskCenter.ChangeTaskState(taskId, Processing)`
3. 调用子类的 `ProcessAsync(taskId, cancellationToken)`
4. 如果 `AutoComplete == true`，调用 `ChangeTaskState(taskId, Completed)`
5. 如果 `TaskCanceledException`，状态设为 `Cancled`
6. 如果其他异常，状态设为 `Failed`，错误信息含异常消息和堆栈
7. finally 块中 `_serviceScope.Dispose()`

**重要**：BaseTask 构造函数中创建了自己的 `IServiceScope`，任务执行完毕后自动释放。不要在 `ProcessAsync` 外部持有 `_serviceProvider` 或 `_dbContext` 的引用。

### 5.2 任务注册与串行特性

```csharp
// 标记任务处理器类并指定任务类型标识（必须与 TaskTypes 常量值一致）
[AttributeUsage(AttributeTargets.Class)]
public class TaskRegisterAttribute(string type) : Attribute
{
    public string Type { get; set; } = type;
}

// 标记任务为串行执行模式（同类型任务不允许并发）
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class TaskSequentialAttribute : Attribute
```

### 5.3 TaskCenter（Singleton，实现 ISingletonService）

```csharp
namespace SharpDevFramework;

public class TaskCenter : ISingletonService
{
    // 发布任务 ID 到 Channel 队列
    // 调用前 TaskCenter 必须已启动（由 TaskHostedService 在启动时调用 StartAsync）
    public async Task PublishAsync(int taskId)

    // 取消正在执行的任务（通过 CancellationTokenSource）
    public async Task CancelTaskAsync(int taskId)

    // 更改任务状态，自动广播 SignalR 通知
    // 失败时自动检查重试次数，未达上限则重新入队
    public async Task ChangeTaskState(int taskId, TaskStates state, string? error = null)
}
```

### 5.4 任务创建与发布标准模式

```csharp
// 在 Controller 中
[ApiController]
[Route("api/[controller]")]
public class DemosController(AppDbContext context, TaskCenter taskCenter) : ControllerBase
{
    [HttpPut("{id}")]
    public async Task<EmptyReply> Update(int id, [FromBody] UpdateDemoRequest request)
    {
        // ... 业务逻辑 ...

        // 1. 创建 TaskEntity 并指定 Type
        var task = new TaskEntity { Type = TaskTypes.Demo, UserId = HttpContext.GetJwtPayload().UserId };
        context.Tasks.Add(task);
        context.SaveChanges();  // 必须先保存以获取 task.Id

        // 2. 发布任务到队列
        await taskCenter.PublishAsync(task.Id);

        return EmptyReply.Succeed();
    }
}
```

**关键顺序**：必须先 `SaveChanges()` 获取 `task.Id`，再调用 `PublishAsync(task.Id)`。

### 5.5 TaskStates 枚举

```csharp
namespace SharpDevFramework;

public enum TaskStates
{
    Pending = 0,      // 待处理
    Processing = 1,   // 处理中
    Completed = 2,    // 已完成
    Failed = 3,       // 失败（可重试）
    Cancled = 4       // 已取消（注意拼写：Cancled 而非 Cancelled/Canceled）
}
```

### 5.6 任务状态流转

```
Pending → Processing → Completed（AutoComplete=true 时自动）
                    → Failed → (RetryCount < MaxRetryCount) → 重新入队 → Pending → ...
                            → (RetryCount >= MaxRetryCount) → 停留在 Failed
                    → Cancled（TaskCanceledException 触发）
```

### 5.7 重试机制

- 任务失败时 `ChangeTaskState` 自动增加 `RetryCount` 并重新调用 `PublishAsync`
- 当 `RetryCount >= MaxRetryCount`（默认 3）时不再重试
- 重试时任务状态仍为 `Failed`，重新入队后变为 `Processing`

### 5.8 SignalR 实时通知

- 任务状态变更时 `TaskCenter.ChangeTaskState` 自动调用 `NotificationService.BroadcastTaskUpdatedAsync(task)`
- 前端通过 SignalR Hub（默认 `/hubs/notifications`）监听 `TaskUpdated` 事件
- 消息格式：`{ type: "TaskUpdated", data: TaskEntity }`

### 5.9 DataCleanupTask

框架内置数据清理任务，按配置的保留天数清理指定表（基于 `CreatedAt` 字段）：

```csharp
[TaskRegister(nameof(FrameworkTaskTypes.DataCleanup))]
public class DataCleanupTask(IServiceProvider serviceProvider) : BaseTask(serviceProvider)
```

配置在 `framework.json` 的 `FrameworkSettings:DataCleanup:Tables` 数组中。触发方式：
- 框架启动时 `TaskHostedService` 自动恢复未完成任务并执行一次 DataCleanup
- 管理员 `POST /api/tasks/cleandb` 手动触发

### 5.10 框架内置任务类型

```csharp
namespace SharpDevFramework;

[MockEnum("TaskTypes")]
public class FrameworkTaskTypes
{
    [Description("数据清理")]
    public const string DataCleanup = nameof(DataCleanup);  // 值为 "DataCleanup"
}
```

扩展方式：创建 `TaskTypes` 类，用 `[MockEnum(nameof(TaskTypes))]` 标记，值会与 `DataCleanup` 合并到 `TaskTypes` 类别。

### 5.11 TasksController API 端点

路由前缀：`api/Tasks`

| HTTP | 路由 | 认证 | 说明 |
|------|------|------|------|
| GET | `/` | 已认证 | 分页查询任务（支持 Status、Type 过滤） |
| GET | `/{id}` | 已认证 | 获取任务详情 |
| DELETE | `/{id}` | Admin | 删除任务 |
| POST | `/{id}/retry` | 已认证 | 重试失败任务（重置状态和重试计数） |
| POST | `/{id}/cancel` | 已认证 | 取消正在执行的任务 |
| POST | `cleandb` | Admin | 触发数据库清理 |

---

## 6. 枚举系统

### 6.1 标准 C# 枚举

```csharp
using System.ComponentModel;

namespace YourProject.Enums;

public enum DemoTypes
{
    [Description("甲")]
    Foo,
    [Description("乙")]
    Bar
}
```

自动暴露到 `/api/enums`，类别名为枚举类型名 `DemoTypes`，值为 int（0, 1, ...）。

### 6.2 MockEnum 扩展类

用于字符串常量枚举场景和扩展框架内置枚举：

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class MockEnumAttribute(string mockEnumName) : Attribute
{
    public string MockEnumName { get; } = mockEnumName;
}
```

**合并规则**：
- `MockEnumName` 相同的多个类，其常量字段会**合并**到同一类别
- 扩展框架内置角色：`[MockEnum("UserRoleTypes")]` — 与框架的 `UserRoleTypes` 合并
- 扩展框架内置任务类型：`[MockEnum(nameof(TaskTypes))]` — 等价于 `[MockEnum("TaskTypes")]`，与框架的 `FrameworkTaskTypes` 合并

**示例**：

```csharp
// 扩展用户角色（与框架 Admin 合并到 UserRoleTypes 类别）
[MockEnum("UserRoleTypes")]
public class UserRoleTypesExtend
{
    [Description("程序员")]
    public const string Programmer = nameof(Programmer);
}

// 扩展任务类型（与框架 DataCleanup 合并到 TaskTypes 类别）
[MockEnum(nameof(TaskTypes))]
public class TaskTypes
{
    [Description("演示任务")]
    public const string Demo = nameof(Demo);
}
```

**关键**：`MockEnumName` 必须与框架内置名称完全一致才能合并：
- 角色扩展：`"UserRoleTypes"`（不是 `"UserRoles"` 或 `"RoleTypes"`）
- 任务类型扩展：`"TaskTypes"`（不是 `"FrameworkTaskTypes"`）

### 6.3 /api/enums 响应格式

```json
{
  "success": true,
  "data": [
    {
      "categoryName": "DemoTypes",
      "items": [
        { "displayName": "甲", "value": 0 },
        { "displayName": "乙", "value": 1 }
      ]
    },
    {
      "categoryName": "UserRoleTypes",
      "items": [
        { "displayName": "管理员", "value": "Admin" },
        { "displayName": "程序员", "value": "Programmer" }
      ]
    },
    {
      "categoryName": "TaskTypes",
      "items": [
        { "displayName": "数据清理", "value": "DataCleanup" },
        { "displayName": "演示任务", "value": "Demo" }
      ]
    }
  ]
}
```

**特性**：结果带缓存（首次请求后缓存），匿名访问。

---

## 7. 服务注册（DI）

### 7.1 标记接口

```csharp
namespace SharpDevFramework;

public interface IScopedService { }       // → Scoped 生命周期
public interface ISingletonService { }    // → Singleton 生命周期
public interface ITransientService { }    // → Transient 生命周期
```

实现任一标记接口的非抽象类会被自动注册到 DI 容器，无需手动在 `Program.cs` 中注册。

### 7.2 使用方式

```csharp
public class MyService : IScopedService
{
    public void DoSomething() { ... }
}

// 在 Controller 中通过构造函数注入
public class MyController(MyService myService) : ControllerBase { ... }
```

### 7.3 框架内置服务清单

| 服务 | 生命周期 | 接口 | 说明 |
|------|----------|------|------|
| `TokenService` | Singleton | ISingletonService | JWT Token 生成与验证 |
| `TaskCenter` | Singleton | ISingletonService | 任务队列引擎 |
| `NotificationService` | Singleton | ISingletonService | SignalR 广播服务 |
| `BaseTask` 子类 | Scoped | IScopedService | 后台任务处理器 |

---

## 8. 过滤器系统

### 8.1 ExceptionFilter

- 实现 `IExceptionFilter`
- `UnauthorizedAccessException` → HTTP 403 + `EmptyReply.Failed(异常消息)`
- 其他异常 → HTTP 500 + `EmptyReply.Failed(异常消息)`
- 异常消息优先取 `InnerException.Message`，其次取 `Exception.Message`
- 配置开关：`FrameworkSettings:UseExceptionFilter`

### 8.2 OperationLogFilter

- 实现 `IAsyncActionFilter`
- 自动记录所有 API 操作（Controller/Action/路由/HTTP方法/IP/UA/请求体/响应体/耗时/成功状态）
- 操作类型推断：POST→Create, PUT→Update, DELETE→Delete, GET→Query
- **跳过规则**：GET 请求到 `/api/tasks` 和 `/api/useroperationlogs` 不记录（高频查询）
- **批量写入**：使用 `List` + `Lock` 缓存，`OperationLogHostedService` 每 2 分钟批量写入数据库
- 配置开关：`FrameworkSettings:OperationLog:IsEnabled`

---

## 9. 统一响应类型

来自 **SharpDevLib** 包：

```csharp
// 无数据返回
EmptyReply.Succeed()
EmptyReply.Failed("错误消息")

// 返回单个数据
DataReply<T>.Succeed(data)
DataReply<T>.Failed("错误消息")

// 返回分页数据
PageReply<T>.Succeed(items, total, request)
PageReply<T>.Failed("错误消息")
```

### PageRequest 基类

```csharp
// 来自 SharpDevLib
public class PageRequest
{
    public int Index { get; set; }  // 页码，从 1 开始
    public int Size { get; set; }   // 每页条数
}
```

### Controller 标准模式

```csharp
[ApiController]
[Route("api/[controller]")]
public class DemosController(AppDbContext context, TaskCenter taskCenter) : ControllerBase
{
    [HttpGet]
    public PageReply<DemoDto> GetPage([FromQuery] DemoPageRequest request)
    {
        var query = context.Demos.Where(x => !x.IsDeleted);
        if (request.Name.NotNullOrWhiteSpace()) query = query.Where(x => x.Name.Contains(request.Name));
        var total = query.Count();
        var items = query.OrderByDescending(x => x.CreatedAt)
            .Skip((request.Index - 1) * request.Size).Take(request.Size).ToList();
        return PageReply.Succeed(items.Adapt<List<DemoDto>>(), total, request);
    }

    [HttpGet("{id}")]
    public DataReply<DemoDto> Get(int id)
    {
        var demo = context.Demos.Find(id) ?? throw new Exception("data not found");
        return DataReply.Succeed(demo.Adapt<DemoDto>());
    }

    [HttpPost]
    public EmptyReply Create([FromBody] CreateDemoRequest request)
    {
        var demo = new DemoEntity { Name = request.Name, Type = request.Type };
        context.Demos.Add(demo);
        context.SaveChanges();
        return EmptyReply.Succeed();
    }

    [HttpPut("{id}")]
    public async Task<EmptyReply> Update(int id, [FromBody] UpdateDemoRequest request)
    {
        var demo = context.Demos.Find(id) ?? throw new Exception("data not found");
        demo.Name = request.Name;
        demo.Type = request.Type;
        context.Demos.Update(demo);
        context.SaveChanges();
        return EmptyReply.Succeed();
    }

    [HttpDelete("{id}")]
    public EmptyReply Delete(int id)
    {
        var demo = context.Demos.Find(id) ?? throw new Exception("data not found");
        demo.IsDeleted = true;
        context.Demos.Update(demo);
        context.SaveChanges();
        return EmptyReply.Succeed();
    }
}
```

**约定**：
- 分页查询用 `PageReply<T>`，排序默认 `OrderByDescending(x => x.CreatedAt)`
- 单条查询用 `DataReply<T>`
- 创建/更新/删除用 `EmptyReply`
- 软删除：设置 `IsDeleted = true`，查询时过滤 `!x.IsDeleted`
- DTO 映射使用 Mapster：`entity.Adapt<Dto>()`
- 数据不存在时 `throw new Exception("data not found")`（被 ExceptionFilter 捕获返回 500）

---

## 10. 静态路径与文件系统

```csharp
namespace SharpDevFramework;

public static class SharpFrameworkStatics
{
    public static readonly string RootDirectory = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string DataDirectory = RootDirectory.CombinePath("data");
    public static readonly string DatabaseDirectory = DataDirectory.CombinePath("db");
    public static readonly string LogsDirectory = DataDirectory.CombinePath("logs");
    public static readonly string TempDirectory = DataDirectory.CombinePath("temp");
    public static readonly string DatabasePath = DatabaseDirectory.CombinePath("database.db");
    public static readonly string LogsPath = LogsDirectory.CombinePath("logs-.txt");
    public static readonly string JwtSecretPath = DatabaseDirectory.CombinePath("jwt-secret.txt");
}
```

**自动创建**：静态构造函数自动创建 `DatabaseDirectory`、`LogsDirectory`、`TempDirectory`。

---

## 11. 前端架构（模板专属）

### 11.1 技术栈

| 技术 | 用途 | 引入方式 |
|------|------|----------|
| Vue 3 | 响应式框架 | `libs/vue.js`（CDN 本地化） |
| Vue Router | SPA 路由（Hash 模式） | `libs/vue-router.js` |
| Axios | HTTP 请求 | `libs/axios.min.js` |
| SignalR Client | 实时通信 | `libs/signalr.min.js` |
| Tailwind CSS | 原子化样式 | `libs/tailwindcss.js`（Play CDN） |

**设计特点**：无构建工具，纯 ES Module，零 Node.js 依赖。

### 11.2 文件结构

```
wwwroot/
├── index.html              # SPA 入口（主题检测、加载第三方库）
├── css/app.css             # 设计系统（CSS 变量 + 亮暗主题）
├── js/
│   ├── app.js              # Vue 应用入口 + ThemeManager + 路由守卫
│   ├── api.js              # Axios 实例封装 + API 命名空间
│   ├── auth.js             # JWT 解析 + Token 管理
│   ├── enums.js            # 枚举加载缓存 + getEnumName()
│   ├── router.js           # 路由定义
│   ├── signalr.js          # SignalR 连接管理 + onTaskUpdated
│   ├── utils.js            # 工具函数（日期格式化等）
│   ├── components/         # UI 组件库
│   │   ├── index.js        # 组件导出汇总
│   │   ├── icon.js         # SVG 图标系统（30+ 图标）
│   │   ├── FButton.js      # 按钮
│   │   ├── FInput.js       # 输入框
│   │   ├── FCheckbox.js    # 复选框
│   │   ├── FSingleSelect.js # 单选下拉
│   │   ├── FMultiSelect.js # 多选下拉
│   │   ├── FTable.js       # 数据表格
│   │   ├── FModal.js       # 模态框
│   │   ├── FDropdown.js    # 下拉菜单
│   │   ├── FPagination.js  # 分页器
│   │   ├── Toast.js        # Toast 通知
│   │   └── Layout.js       # 整体布局（侧边栏+头部+内容区）
│   └── views/              # 页面视图
│       ├── Login.js        # 登录页
│       ├── DemoManager.js  # Demo CRUD 管理
│       ├── UserManager.js  # 用户管理
│       ├── TaskManager.js  # 任务管理（SignalR 实时更新）
│       └── OperationLogManager.js  # 操作日志
└── libs/                   # 第三方库文件
```

### 11.3 主题系统

- 支持 `light` / `dark` / `system` 三种模式
- 通过 CSS 变量定义在 `app.css` 中：`:root`（亮色）、`[data-theme="dark"]`（暗色）
- `ThemeManager`（在 `app.js` 中）通过 Vue `provide/inject` 共享
- 持久化在 `localStorage`

### 11.4 API 客户端层（api.js）

- Axios 实例 baseURL 为 `/api`
- 请求拦截器自动注入 `Authorization: Bearer <token>` 头
- 响应拦截器：`success: false` 自动 Toast 提示；401 自动登出
- 命名空间：`api.tasks`、`api.users`、`api.demos`、`api.enums`、`api.logs`
- 每个方法支持 `onerror` 回调参数

### 11.5 认证流程（auth.js）

- `parseJwt(token)` — 解析 JWT Payload
- `isTokenValid(token)` — 检查 Token 是否过期
- `setAuth(token)` / `clearAuth()` / `getAuth()` — 管理 localStorage 中的认证状态
- 路由守卫：未认证跳转登录页，已认证首次加载枚举并初始化 SignalR

### 11.6 枚举缓存（enums.js）

- `loadEnums()` — 从 `/api/enums` 一次性加载并缓存
- `getEnumName(category, value, isMultiple)` — 根据类别和值获取中文显示名
- 响应式 getter：`enums.userRoleTypes`、`enums.taskStates`、`enums.taskTypes`、`enums.demoTypes`

### 11.7 SignalR 客户端（signalr.js）

- `initSignalR(token)` — 建立连接（Token 通过 QueryString 传递）
- `stopSignalR()` — 断开连接
- `onTaskUpdated(callback)` — 注册任务更新事件回调
- 自动重连策略：1s → 2s → 5s → 10s → 30s → 120s

### 11.8 添加新页面的步骤

1. 在 `wwwroot/js/views/` 创建视图组件
2. 在 `wwwroot/js/router.js` 添加路由
3. 在 `wwwroot/js/components/Layout.js` 添加侧边栏导航项
4. 在 `wwwroot/js/api.js` 添加 API 方法（如需要）

---

## 12. 扩展指南（标准操作模板）

### 12.1 添加新业务实体 + CRUD

**步骤 1**：创建实体（`Data/Entities/XxxEntity.cs`）

```csharp
using SharpDevFramework;

namespace YourProject.Data.Entities;

public class XxxEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}
```

**步骤 2**：在 `AppDbContext` 中添加 DbSet

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : FrameworkDbContext(options)
{
    public DbSet<XxxEntity> Xxxs { get; set; }
    // ... 其他 DbSet
}
```

**步骤 3**：创建 DTO（可与 Controller 同文件）

```csharp
public class XxxDto { public int Id { get; set; } public string Name { get; set; } = string.Empty; public long CreatedAt { get; set; } }
public class XxxPageRequest : PageRequest { public string? Name { get; set; } }
public class CreateXxxRequest { public string Name { get; set; } = string.Empty; }
public class UpdateXxxRequest { public string Name { get; set; } = string.Empty; }
```

**步骤 4**：创建 Controller（`Controllers/XxxsController.cs`）

```csharp
using Mapster;
using Microsoft.AspNetCore.Mvc;
using SharpDevFramework;
using YourProject.Data;
using YourProject.Data.Entities;

namespace YourProject.Controllers;

[ApiController]
[Route("api/[controller]")]
public class XxxsController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    public PageReply<XxxDto> GetPage([FromQuery] XxxPageRequest request)
    {
        var query = context.Xxxs.Where(x => !x.IsDeleted);
        if (request.Name.NotNullOrWhiteSpace()) query = query.Where(x => x.Name.Contains(request.Name));
        var total = query.Count();
        var items = query.OrderByDescending(x => x.CreatedAt)
            .Skip((request.Index - 1) * request.Size).Take(request.Size).ToList();
        return PageReply.Succeed(items.Adapt<List<XxxDto>>(), total, request);
    }

    [HttpGet("{id}")]
    public DataReply<XxxDto> Get(int id)
    {
        var entity = context.Xxxs.Find(id) ?? throw new Exception("data not found");
        return DataReply.Succeed(entity.Adapt<XxxDto>());
    }

    [HttpPost]
    public EmptyReply Create([FromBody] CreateXxxRequest request)
    {
        var entity = new XxxEntity { Name = request.Name };
        context.Xxxs.Add(entity);
        context.SaveChanges();
        return EmptyReply.Succeed();
    }

    [HttpPut("{id}")]
    public EmptyReply Update(int id, [FromBody] UpdateXxxRequest request)
    {
        var entity = context.Xxxs.Find(id) ?? throw new Exception("data not found");
        entity.Name = request.Name;
        context.Xxxs.Update(entity);
        context.SaveChanges();
        return EmptyReply.Succeed();
    }

    [HttpDelete("{id}")]
    public EmptyReply Delete(int id)
    {
        var entity = context.Xxxs.Find(id) ?? throw new Exception("data not found");
        entity.IsDeleted = true;
        context.Xxxs.Update(entity);
        context.SaveChanges();
        return EmptyReply.Succeed();
    }
}
```

**步骤 5**：生成迁移

```bash
dotnet ef migrations add AddXxxEntity --output-dir Data/Migrations
```

### 12.2 添加新后台任务

**步骤 1**：定义任务类型（`Enums/TaskTypesExtend.cs`，如文件不存在则创建）

```csharp
using System.ComponentModel;
using SharpDevFramework;

namespace YourProject.Enums;

[MockEnum(nameof(TaskTypes))]
public class TaskTypes
{
    [Description("演示任务")]
    public const string Demo = nameof(Demo);

    [Description("新任务")]
    public const string MyNewTask = nameof(MyNewTask);
}
```

**步骤 2**：实现任务处理器（`Tasks/MyNewTask.cs`）

```csharp
using SharpDevFramework;
using YourProject.Enums;

namespace YourProject.Tasks;

[TaskRegister(nameof(TaskTypes.MyNewTask))]
//[TaskSequential]  // 取消注释以启用串行执行
public class MyNewTask(IServiceProvider serviceProvider) : BaseTask(serviceProvider)
{
    protected override async Task ProcessAsync(int id, CancellationToken cancellationToken)
    {
        var task = _dbContext.Tasks.Find(id) ?? throw new Exception($"task not found with id:{id}");
        var data = task.Data;  // 获取任务数据 JSON

        _logger.LogInformation("Processing task {Id}", id);

        // ... 业务逻辑 ...

        await Task.CompletedTask;
    }
}
```

**步骤 3**：在 Controller 中发布任务

```csharp
var task = new TaskEntity { Type = TaskTypes.MyNewTask, UserId = HttpContext.GetJwtPayload().UserId };
context.Tasks.Add(task);
context.SaveChanges();
await taskCenter.PublishAsync(task.Id);
```

### 12.3 添加新枚举

**标准枚举**（`Enums/XxxTypes.cs`）：

```csharp
using System.ComponentModel;

namespace YourProject.Enums;

public enum XxxTypes
{
    [Description("选项A")]
    OptionA,
    [Description("选项B")]
    OptionB
}
```

**MockEnum 扩展**（扩展现有类别）：

```csharp
using System.ComponentModel;
using SharpDevFramework;

namespace YourProject.Enums;

[MockEnum("UserRoleTypes")]
public class UserRoleTypesExtend
{
    [Description("新角色")]
    public const string NewRole = nameof(NewRole);
}
```

### 12.4 添加新角色

通过 MockEnum 扩展（见 12.3），然后可以在 Controller 中使用 `[Role(["Admin", "NewRole"])]`。

### 12.5 配置数据清理

在 `framework.json` 的 `DataCleanup:Tables` 数组中添加新条目：

```json
{
  "Name": "Xxxs",           // DbSet 属性名（非数据库表名）
  "RetentionDays": 30       // 保留天数
}
```

---

## 13. 易错点与注意事项

### 13.1 数据库层

| 易错点 | 正确做法 |
|--------|----------|
| DbContext 继承 `DbContext` | 必须继承 **`FrameworkDbContext`** |
| 自定义 DbContext 泛型参数写成 `DbContextOptions<FrameworkDbContext>` | 必须写 **`DbContextOptions<AppDbContext>`**（自定义子类名） |
| 忘记过滤软删除数据 | 查询时必须 `.Where(x => !x.IsDeleted)`，框架不自动过滤 |
| `CreatedAt` 当作 `DateTime` 使用 | 类型是 **`long`**（UTC 毫秒时间戳），使用 `DateTime.Now.ToUtcTimestamp()` 获取 |
| 实体不继承 `BaseEntity` | 所有自定义实体必须继承 `BaseEntity` |
| 手动 `dotnet ef database update` | 框架启动时自动执行迁移，无需手动操作 |

### 13.2 认证授权

| 易错点 | 正确做法 |
|--------|----------|
| `GetJwtPayload()` 在匿名接口上调用 | 匿名接口没有认证，Items 中无 payload，会抛异常 |
| Special Token 访问普通接口 | Special Token 只能访问标记 **`[SpecialToken]`** 的接口 |
| 角色匹配用 `==` 精确匹配 | Role 字段是逗号分隔多角色，用 `.SplitToList().Contains()` 或 `DbHelper.BuildOrLikeExpression` |
| `[Role]` 特性参数类型写错 | 参数是 **`string[]`**，用法 `[Role(["Admin"])]`（C# 12 集合表达式）或 `new string[] { "Admin" }` |
| MockEnum 角色扩展名称写错 | 扩展角色必须 `[MockEnum("UserRoleTypes")]`，不是 `"UserRoles"` 或 `"RoleTypes"` |

### 13.3 后台任务

| 易错点 | 正确做法 |
|--------|----------|
| `TaskEntity` 保存前调用 `PublishAsync` | 必须 **先 `SaveChanges()` 获取 Id**，再 `PublishAsync(task.Id)` |
| `[TaskRegister]` 的 Type 与 TaskTypes 常量不一致 | Type 必须与 TaskTypes 常量值**完全一致**，推荐用 `nameof(TaskTypes.Xxx)` |
| 重复注册相同 TaskRegister Type | 同一 Type **不可重复注册**，会抛异常 |
| 忘记给 BaseTask 子类加 `[TaskRegister]` | 框架扫描时发现无此特性的 BaseTask 子类会输出 Warning 并跳过 |
| 在 BaseTask.ProcessAsync 外持有 `_dbContext` | `_serviceScope` 在 `HandleAsync` 的 finally 中 Dispose，之后 `_dbContext` 失效 |
| `TaskStates.Cancled` 拼写 | 注意框架拼写是 **`Cancled`**，不是 `Cancelled` 或 `Canceled` |
| MockEnum 任务类型扩展名称写错 | 扩展任务类型必须 `[MockEnum("TaskTypes")]`（不是 `"FrameworkTaskTypes"`） |

### 13.4 通用

| 易错点 | 正确做法 |
|--------|----------|
| `assemblies` 不包含项目程序集 | 必须传入 `[Assembly.GetExecutingAssembly()]`，否则 Controller/Task/Enum 无法被发现 |
| 使用 `throw new Exception()` 但期望返回特定 HTTP 状态码 | 框架 ExceptionFilter 将所有非 `UnauthorizedAccessException` 返回 500；`UnauthorizedAccessException` 返回 403 |
| 忘记 `using SharpDevFramework;` | 框架所有公共类型在 `SharpDevFramework` 命名空间下 |
| 直接操作 `FrameworkDbContext.Users/Tasks/UserOperationLogs` | 这些是框架内置表，可读取但不要随意修改结构 |
| CORS 配置中 `MaxAge` 的 JSON 键 | JSON 中用 **`MaxAgeSeconds`**，代码读取键为 `FrameworkSettings:Cors:MaxAge` |

---

## 附录 A：框架所有公共类型速查表

| 类别 | 类型 | 名称 | 生命周期/用途 |
|------|------|------|---------------|
| 入口 | 扩展方法 | `AddSharpDevFramework<TDbContext>()` | 框架服务注册 |
| 入口 | 扩展方法 | `UseSharpDevFramework()` | 框架中间件启用 |
| 认证 | 扩展方法 | `HttpContext.GetJwtPayload()` | 获取当前 JWT 负载 |
| 认证 | 服务 | `TokenService` | Singleton，JWT 生成/验证 |
| 认证 | 模型 | `JwtPayload` | JWT 负载数据结构 |
| 认证 | 特性 | `[Role(string[])]` | 角色权限标记 |
| 认证 | 特性 | `[SpecialToken]` | Special Token 接口标记 |
| 认证 | 常量类 | `UserRoleTypes` | 内置角色（Admin），可 MockEnum 扩展 |
| 认证 | 控制器 | `UsersController` | 用户管理 API |
| 认证 | 控制器 | `UserOperationLogsController` | 操作日志 API |
| 认证 | DTO | `LoginRequest`/`LoginResponse` | 登录请求/响应 |
| 认证 | DTO | `UserPageRequest` | 用户分页查询 |
| 认证 | DTO | `CreateUserRequest`/`UpdateUserRequest` | 创建/更新用户 |
| 认证 | DTO | `UserDto` | 用户数据传输 |
| 数据库 | 抽象类 | `FrameworkDbContext` | DbContext 基类（Users/Tasks/UserOperationLogs） |
| 数据库 | 抽象类 | `BaseEntity` | 实体基类（Id/CreatedAt/IsDeleted） |
| 数据库 | 实体 | `UserEntity`/`TaskEntity`/`UserOperationLogEntity` | 内置实体 |
| 数据库 | 工具 | `DbHelper.BuildOrLikeExpression<T>()` | 动态 OR LIKE 表达式 |
| 任务 | 抽象类 | `BaseTask` | 后台任务基类（Scoped） |
| 任务 | 特性 | `[TaskRegister(string)]` | 任务注册标记 |
| 任务 | 特性 | `[TaskSequential]` | 串行执行标记 |
| 任务 | 枚举 | `TaskStates` | 任务状态（Pending/Processing/Completed/Failed/Cancled） |
| 任务 | 服务 | `TaskCenter` | Singleton，任务调度引擎 |
| 任务 | 常量类 | `FrameworkTaskTypes` | 内置任务类型（DataCleanup），可 MockEnum 扩展 |
| 任务 | 控制器 | `TasksController` | 任务管理 API |
| 枚举 | 控制器 | `EnumsController` | 枚举值查询 API |
| 枚举 | 特性 | `[MockEnum(string)]` | 模拟枚举标记 |
| 枚举 | DTO | `EnumsResponse`/`EnumItemsResponse` | 枚举 API 响应 |
| DI | 接口 | `IScopedService`/`ISingletonService`/`ITransientService` | DI 生命周期标记 |
| 配置 | 静态类 | `SharpFrameworkStatics` | 框架路径常量 |

## 附录 B：框架内置 API 端点汇总

| 路由前缀 | HTTP | 路由 | 认证 | 说明 |
|----------|------|------|------|------|
| api/Users | POST | login | 匿名 | 登录 |
| api/Users | POST | special-token | 已认证 | 生成 Special Token |
| api/Users | GET | / | 已认证 | 分页查询用户 |
| api/Users | GET | /{id} | 已认证 | 获取用户详情 |
| api/Users | POST | / | Admin | 创建用户 |
| api/Users | PUT | /{id} | 已认证 | 更新用户 |
| api/Users | DELETE | /{id} | Admin | 软删除用户 |
| api/UserOperationLogs | GET | / | Admin | 分页查询操作日志 |
| api/UserOperationLogs | GET | /{id} | Admin | 获取日志详情 |
| api/Tasks | GET | / | 已认证 | 分页查询任务 |
| api/Tasks | GET | /{id} | 已认证 | 获取任务详情 |
| api/Tasks | DELETE | /{id} | Admin | 删除任务 |
| api/Tasks | POST | /{id}/retry | 已认证 | 重试失败任务 |
| api/Tasks | POST | /{id}/cancel | 已认证 | 取消任务 |
| api/Tasks | POST | cleandb | Admin | 触发数据清理 |
| api/Enums | GET | / | 匿名 | 获取所有枚举值 |
| /hubs/notifications | WebSocket | - | - | SignalR Hub（TaskUpdated 事件） |

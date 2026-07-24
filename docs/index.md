# SharpDevFramework 使用指南

## 目录

- [简介](#简介)
- [快速开始](#快速开始)
- [项目结构规范](#项目结构规范)
- [核心启动流程](#核心启动流程)
- [配置系统](#配置系统)
- [数据库规范](#数据库规范)
- [Controller 规范](#controller-规范)
- [服务注册规范](#服务注册规范)
- [后台任务系统](#后台任务系统)
- [枚举扩展系统](#枚举扩展系统)
- [认证授权](#认证授权)
- [操作日志](#操作日志)
- [数据清理](#数据清理)
- [前端架构](#前端架构)
- [部署](#部署)
- [API 参考](#api-参考)

---

## 简介

SharpDevFramework 是一个基于 .NET 10.0 的全栈 Web 开发框架，旨在提供开箱即用的开发体验。框架内置了以下核心能力：

- **JWT 认证** — 自动生成密钥、Token 签发与验证、账户锁定
- **角色权限** — 基于角色的访问控制，可扩展角色类型
- **后台任务** — 基于通道（Channel）的任务队列，支持并发控制、自动重试、SignalR 实时通知
- **操作日志** — 自动记录所有 API 操作，去重缓存，定时持久化
- **数据清理** — 可配置保留策略的自动数据清理
- **异常过滤** — 全局异常捕获与统一错误响应
- **CORS** — 可配置的跨域策略
- **SignalR** — 实时通信，任务状态变更即时推送
- **Windows 服务** — 支持 Windows Service 宿主
- **Serilog** — 结构化日志，文件滚动 + 控制台输出
- **SQLite** — 零配置数据库，文件存储
- **前端 SPA** — Vue 3 + Tailwind CSS + 组件库 + 暗色主题

框架通过 NuGet 包分发，安装后仅需两行代码即可启动。

---

## 快速开始

### 使用模板创建项目（推荐）

``bash
# 安装模板
dotnet new install SharpDevFramework.Template

# 创建新项目
dotnet new sharpdev -n MyProject

# 进入项目目录
cd MyProject

# 还原依赖
dotnet restore

# 运行项目
dotnet run

# 生成数据库迁移
dotnet ef migrations add InitialCreate --output-dir Data/Migrations

# 再次运行（自动创建数据库和默认管理员）
dotnet run
``

项目启动后：
- 访问 `http://localhost:3000` 进入前端
- 初始管理员账号密码在 `data/db/auth.txt` 文件中

### 手动创建项目

``bash
# 创建 Web API 项目
dotnet new webapi -n MyProject
cd MyProject

# 安装框架
dotnet add package SharpDevFramework
dotnet add package Microsoft.EntityFrameworkCore.Design

# 运行后将自动生成 framework.json
dotnet run
``

修改 `Program.cs` 为：

``csharp
using SharpDevFramework;
using MyProject.Data;
using System.Reflection;

Assembly[] assemblies = [Assembly.GetExecutingAssembly()];

var builder = WebApplication.CreateBuilder(args);
builder.AddSharpDevFramework<AppDbContext>(assemblies);

var app = builder.Build();
app.UseSharpDevFramework(assemblies);
app.Run();
``

---

## 项目结构规范

使用模板创建的项目遵循以下目录结构约定：

```
MyProject/
├── Program.cs                  # 入口文件，仅两行框架代码
├── appsettings.json            # 应用配置（端口等）
├── framework.json              # 框架配置（认证/CORS/任务/日志等）
├── install.bat                 # Windows 服务安装脚本
├── MyProject.csproj            # 项目文件
├── Controllers/                # API 控制器
│   └── DemosController.cs      # 示例 CRUD 控制器
├── Data/                       # 数据层
│   ├── AppDbContext.cs         # 数据库上下文（继承 FrameworkDbContext）
│   ├── Entities/               # 实体定义
│   │   └── DemoEntity.cs       # 示例实体（继承 BaseEntity）
│   └── Migrations/             # EF Core 迁移文件
├── Enums/                      # 枚举与 MockEnum 扩展
│   ├── DemoTypes.cs            # 项目自定义枚举
│   ├── UserRoleTypesExtend.cs  # 扩展框架的用户角色类型
│   └── TaskTypesExtend.cs      # 扩展框架的任务类型
├── Tasks/                      # 后台任务处理器
│   └── DemoTask.cs             # 示例任务（继承 BaseTask）
├── Properties/
│   └── launchSettings.json     # 启动配置
└── wwwroot/                    # 前端 SPA
    ├── index.html              # 入口 HTML
    ├── css/app.css             # 设计系统与主题
    ├── js/                     # JavaScript 模块
    │   ├── app.js              # Vue 应用启动
    │   ├── api.js              # API 客户端
    │   ├── auth.js             # JWT 认证工具
    │   ├── enums.js            # 枚举管理
    │   ├── router.js           # 路由配置
    │   ├── signalr.js          # SignalR 客户端
    │   ├── utils.js            # 工具函数
    │   ├── components/         # UI 组件库
    │   └── views/              # 页面视图
    └── libs/                   # 第三方库（Vue/Axios/SignalR/Tailwind）
```

### 目录职责说明

| 目录 | 职责 | 规范 |
|------|------|------|
| `Controllers/` | API 控制器 | 一个文件一个控制器，路由使用 `api/[controller]` 约定 |
| `Data/` | 数据库上下文 | 必须继承 `FrameworkDbContext`，项目只能有一个 |
| `Data/Entities/` | 数据实体 | 必须继承 `BaseEntity`，一个文件一个实体 |
| `Data/Migrations/` | 迁移文件 | 由 EF Core 自动生成，不要手动修改 |
| `Enums/` | 枚举定义 | 标准 C# 枚举或 `[MockEnum]` 扩展类 |
| `Tasks/` | 后台任务 | 必须继承 `BaseTask` 并标注 `[TaskRegister]` |
---

## 核心启动流程

框架的全部启动逻辑由两个扩展方法完成：

### `builder.AddSharpDevFramework<AppDbContext>(assemblies)`

注册阶段，按顺序执行以下操作：

1. **AddSettings** — 加载配置文件链：`appsettings.json` → `appsettings.local.json` → `framework.json` → `framework.local.json`
2. **AddSerilog** — 配置结构化日志（文件滚动 + 控制台）
3. **ConfigSizeLimit** — 设置请求体大小限制
4. **AddCors** — 注册 CORS 跨域策略
5. **AddFilters** — 注册认证过滤器、异常过滤器、操作日志过滤器
6. **AddWindowsService** — 启用 Windows Service 宿主支持
7. **AddDbContext** — 注册 EF Core SQLite 上下文
8. **AddServices** — 扫描程序集，自动注册 `IScopedService`/`ISingletonService`/`ITransientService`
9. **AddSignalR** — 注册 SignalR 服务
10. **AddTasks** — 注册后台任务系统（`TaskHostedService`）

### `app.UseSharpDevFramework(assemblies)`

中间件阶段，按顺序执行以下操作：

1. **UseDefaultFiles + UseStaticFiles** — 启用 `wwwroot` 静态文件服务
2. **UseCors** — 应用 CORS 中间件
3. **UseSignalR** — 映射 SignalR Hub
4. **UseEnums** — 扫描程序集中的枚举和 MockEnum，供 `/api/enums` 使用
5. **MapControllers** — 映射所有控制器路由
6. **SeedDatabase** — 执行数据库迁移，创建默认管理员

### `assemblies` 参数的作用

框架需要扫描你的项目程序集来发现：
- 自定义的 Controller
- 继承 `BaseTask` 的后台任务处理器
- 标注了 `[MockEnum]` 的枚举扩展类
- 实现了 `IScopedService`/`ISingletonService`/`ITransientService` 的服务类

通常传入 `[Assembly.GetExecutingAssembly()]` 即可。

---

## 配置系统

框架使用分层配置，按优先级从低到高：

1. `appsettings.json` — 应用基础配置
2. `appsettings.local.json` — 应用本地覆盖（不提交 Git）
3. `framework.json` — 框架配置（随 NuGet 包自动生成，首次运行后可修改）
4. `framework.local.json` — 框架本地覆盖（不提交 Git）

### framework.json 完整配置说明

| 配置路径 | 类型 | 默认值 | 说明 |
|----------|------|--------|------|
| `Serilog:IsEnabled` | bool | true | 是否启用 Serilog 日志 |
| `FrameworkSettings:UseExceptionFilter` | bool | true | 是否启用全局异常过滤器 |
| `FrameworkSettings:UseWindowsService` | bool | true | 是否支持 Windows 服务宿主 |
| `FrameworkSettings:UseSignalR` | bool | true | 是否启用 SignalR 实时通信 |
| `FrameworkSettings:HubUrl` | string | `/hubs/notifications` | SignalR Hub 端点路径 |
| `FrameworkSettings:RequestSizeLimit` | long | 10737418240 (10GB) | 请求体最大字节数 |
| `FrameworkSettings:JwtExpirationMinutes` | int | 120 | JWT Token 过期时间（分钟） |

### CORS 配置

| 字段 | 说明 |
|------|------|
| `Origins` | 允许的来源列表，支持通配符端口 `*` |
| `Methods` | 允许的 HTTP 方法 |
| `Headers` | 允许的请求头（空数组表示允许所有） |
| `AllowCredentials` | 是否允许携带凭证（Cookie 等） |
| `MaxAgeSeconds` | 预检请求缓存时间 |

### 任务配置

| 字段 | 说明 |
|------|------|
| `MaxRetryCount` | 任务失败后最大重试次数 |
| `MaxProcessCount` | 同时执行的最大任务数（并发控制） |

### 认证配置

| 字段 | 说明 |
|------|------|
| `LockMinutes` | 账户锁定时长（分钟） |
| `MaxFailedCount` | 触发锁定的最大失败次数 |
| `FailedMinutes` | 失败次数的统计时间窗口（分钟） |

### 数据清理配置

| 字段 | 说明 |
|------|------|
| `Tables` | 需要清理的表列表 |
| `Name` | 表名（对应 DbSet 属性名） |
| `RetentionDays` | 数据保留天数 |
---

## 数据库规范

### FrameworkDbContext

项目必须创建一个继承自 `FrameworkDbContext` 的数据库上下文：

``csharp
using Microsoft.EntityFrameworkCore;
using MyProject.Data.Entities;

namespace MyProject.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : FrameworkDbContext(options)
{
    public DbSet<DemoEntity> Demos { get; set; }
}
``

`FrameworkDbContext` 已内置以下 `DbSet`：
- `Users` — 用户表（`UserEntity`）
- `Tasks` — 任务表（`TaskEntity`）
- `UserOperationLogs` — 操作日志表（`UserOperationLogEntity`）

你只需添加项目自身的业务表。

### BaseEntity

所有实体必须继承 `BaseEntity`，它提供：

- `Id` (int) — 自增主键
- `CreatedAt` (long) — UTC 时间戳（自动设置）
- `IsDeleted` (bool) — 软删除标记

自定义实体示例：

``csharp
namespace MyProject.Data.Entities;

public class DemoEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long? UpdatedAt { get; set; }
}
``

### 数据库存储位置

框架使用 SQLite，数据库文件存储在：

| 路径 | 说明 |
|------|------|
| `data/db/database.db` | 数据库文件 |
| `data/db/jwt-secret.txt` | JWT 签名密钥（首次运行自动生成） |
| `data/db/auth.txt` | 初始管理员账号密码（仅首次生成） |
| `data/logs/` | 日志文件目录 |
| `data/temp/` | 临时文件目录 |

这些路径由 `Statics` 类管理，目录在首次访问时自动创建。

### 迁移命令

``bash
# 创建迁移
dotnet ef migrations add InitialCreate --output-dir Data/Migrations

# 更新数据库（或直接运行项目，框架会自动迁移）
dotnet ef database update
``

> 框架在启动时自动执行 `context.Database.Migrate()`，无需手动更新数据库。

---

## Controller 规范

### 基本结构

``csharp
[ApiController]
[Route("api/[controller]")]
public class DemosController(AppDbContext context, TaskCenter taskCenter) : ControllerBase
{
    [HttpGet]
    public PageReply<DemoDto> GetPage([FromQuery] DemoPageRequest request) { ... }

    [HttpGet("{id}")]
    public DataReply<DemoDto> Get(int id) { ... }

    [HttpPost]
    public EmptyReply Create([FromBody] CreateDemoRequest request) { ... }

    [HttpPut("{id}")]
    public EmptyReply Update(int id, [FromBody] UpdateDemoRequest request) { ... }

    [HttpDelete("{id}")]
    public EmptyReply Delete(int id) { ... }
}
``

### 响应类型约定

框架使用 `SharpDevLib` 提供的统一响应类型：

| 类型 | 用途 | 示例 |
|------|------|------|
| `EmptyReply` | 无数据返回 | `EmptyReply.Succeed()` / `EmptyReply.Failed("msg")` |
| `DataReply<T>` | 返回单个数据 | `DataReply.Succeed(data)` |
| `PageReply<T>` | 返回分页数据 | `PageReply.Succeed(items, total, request)` |

### 分页请求约定

``csharp
public class DemoPageRequest : PageRequest
{
    public string? Name { get; set; }
    public string? Type { get; set; }
}
``

`PageRequest` 基类包含 `Index`（页码，从 1 开始）和 `Size`（每页条数）。

### DTO 映射

使用 Mapster 进行实体到 DTO 的映射：

``csharp
var dto = entity.Adapt<DemoDto>();
var dtoList = entities.Adapt<List<DemoDto>>();
``

### 软删除约定

``csharp
demo.IsDeleted = true;
context.Demos.Update(demo);
context.SaveChanges();
``

查询时过滤已删除数据：

``csharp
var query = context.Demos.Where(x => !x.IsDeleted);
``

### OR-LIKE 查询

使用 `DbHelper.BuildOrLikeExpression` 进行多值模糊匹配：

``csharp
var types = request.Type.SplitToList();
var predicate = DbHelper.BuildOrLikeExpression<DemoEntity>("Type", [.. types]);
query = query.Where(predicate);
``

这在角色/类型等多选筛选场景下特别有用。
---

## 服务注册规范

框架提供三个标记接口，实现它们的类会被自动注册到 DI 容器：

| 接口 | 生命周期 | 说明 |
|------|----------|------|
| `IScopedService` | Scoped | 每次请求一个实例 |
| `ISingletonService` | Singleton | 全局单例 |
| `ITransientService` | Transient | 每次注入一个新实例 |

### 使用方式

``csharp
public class MyService : IScopedService
{
    public void DoSomething() { ... }
}
``

无需手动在 `Program.cs` 中注册，框架在启动时会自动扫描并注册。

### 已知的框架内置服务

| 服务 | 生命周期 | 说明 |
|------|----------|------|
| `TokenService` | Singleton | JWT Token 生成与验证 |
| `NotificationService` | Singleton | SignalR 广播服务 |
| `TaskCenter` | Singleton | 任务队列引擎 |

---

## 后台任务系统

### 创建任务处理器

1. 在 `Tasks/` 目录下创建类，继承 `BaseTask`
2. 用 `[TaskRegister]` 标注任务类型
3. 实现 `ProcessAsync` 方法

``csharp
using MyProject.Enums;

namespace MyProject.Tasks;

[TaskRegister(nameof(TaskTypes.MyTask))]
public class MyTask(IServiceProvider serviceProvider) : BaseTask(serviceProvider)
{
    protected override async Task ProcessAsync(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing task {id}", id);
        await Task.CompletedTask;
    }
}
``

### BaseTask 提供的成员

| 成员 | 类型 | 说明 |
|------|------|------|
| `_serviceProvider` | IServiceProvider | 服务提供者 |
| `_logger` | ILogger | 日志记录器 |
| `_dbContext` | FrameworkDbContext | 数据库上下文 |
| `_taskCenter` | TaskCenter | 任务中心（用于发布子任务） |
| `_notificationService` | NotificationService | SignalR 通知服务 |
| `AutoComplete` | bool | 是否自动完成（默认 true） |

### 发布任务

在 Controller 中通过 `TaskCenter` 发布任务：

``csharp
var task = new TaskEntity { Type = TaskTypes.MyTask };
context.Tasks.Add(task);
context.SaveChanges();
await taskCenter.PublishAsync(task.Id);
``

### 任务状态流转

`
Pending → Processing → Completed
                     → Failed → (自动重试) → Processing → ...
                     → Cancled
`

| 状态 | 值 | 说明 |
|------|---|------|
| Pending | 0 | 等待处理 |
| Processing | 1 | 正在处理 |
| Completed | 2 | 处理完成 |
| Failed | 3 | 处理失败（可重试） |
| Cancled | 4 | 已取消 |

### 重试机制

- 任务失败后，框架自动增加 `RetryCount` 并重新入队
- 当 `RetryCount` 超过 `MaxRetryCount`（默认 3）时，任务不再重试
- 任务状态变更时通过 SignalR 广播 `TaskUpdated` 事件

### SignalR 实时通知

前端通过 SignalR 监听任务状态变更：

``javascript
import { onTaskUpdated } from './signalr.js';

onTaskUpdated((task) => {
    // 更新任务列表中的对应条目
});
``

---

## 枚举扩展系统

框架提供了两种枚举定义方式，均通过 `/api/enums` 端点自动暴露给前端。

### 标准 C# 枚举

``csharp
using System.ComponentModel;

namespace MyProject.Enums;

public enum DemoTypes
{
    [Description("甲")]
    Foo,
    [Description("乙")]
    Bar
}
``

### MockEnum 扩展类

用于扩展框架内置的枚举类型（如角色类型、任务类型），使用字符串常量模拟枚举值：

``csharp
using System.ComponentModel;

namespace MyProject.Enums;

// 扩展框架的 UserRoleTypes，添加自定义角色
[MockEnum("UserRoleTypes")]
public class UserRoleTypesExtend
{
    [Description("程序员")]
    public const string Programmer = nameof(Programmer);
}

// 扩展框架的 TaskTypes，添加自定义任务类型
[MockEnum(nameof(TaskTypes))]
public class TaskTypes
{
    [Description("演示任务")]
    public const string Demo = nameof(Demo);
}
``

关键规则：
- `[MockEnum("UserRoleTypes")]` 表示该类的值会合并到 `UserRoleTypes` 类别中
- 框架内置的 `UserRoleTypes` 已有 `Admin` 角色，你的扩展会追加到同一类别
- `TaskTypes` 的 `nameof(TaskTypes)` 同样会与框架内置的 `DataCleanup` 合并
- 常量值推荐使用 `nameof()` 保持一致性

### 前端枚举使用

``javascript
import { enums, getEnumName } from './enums.js';

// 获取枚举列表（用于下拉选择）
const options = enums.demoTypes;

// 根据值获取显示名
const name = getEnumName('demoTypes', 0); // "甲"

// 多值枚举（逗号分隔）
const names = getEnumName('userRoleTypes', 'Admin,Programmer', true); // "Administrator, 程序员"
``

---

## 认证授权

### JWT 认证流程

1. 用户通过 `POST /api/users/login` 登录
2. 框架验证密码，签发 JWT Token
3. 前端将 Token 存储在 `localStorage`
4. 后续请求通过 `Authorization: Bearer <token>` 头携带
5. `AuthFilter` 自动验证 Token 并提取 `JwtPayload`

### JwtPayload 结构

| 字段 | 类型 | 说明 |
|------|------|------|
| `UserId` | int | 用户 ID |
| `Username` | string | 用户名 |
| `Exp` | long | 过期时间（UTC 时间戳） |
| `Role` | string | 角色列表（逗号分隔） |

### 在 Controller 中获取当前用户

``csharp
var payload = HttpContext.GetJwtPayload();
var userId = payload.UserId;
var username = payload.Username;
var role = payload.Role;
``

### 账户锁定

- 在 `FailedMinutes` 时间窗口内连续失败 `MaxFailedCount` 次后，账户锁定 `LockMinutes` 分钟
- 锁定期间无法登录

### 允许匿名访问

对特定端点使用 `[AllowAnonymous]` 特性：

``csharp
[AllowAnonymous]
[HttpGet("public")]
public EmptyReply PublicEndpoint() { ... }
``

### 角色控制

框架内置的用户管理 API 仅 `Admin` 角色可访问。你可以在自定义 Controller 中检查角色：

``csharp
var payload = HttpContext.GetJwtPayload();
var isAdmin = payload.Role.Split(',').Contains("Admin");
``

---

## 操作日志

框架自动记录所有 API 操作，无需手动编码。

### 记录内容

| 字段 | 说明 |
|------|------|
| `UserId` | 操作用户 ID |
| `UserName` | 操作用户名 |
| `OperationType` | 操作类型（Create/Update/Delete/Query） |
| `ControllerName` | 控制器名 |
| `ActionName` | Action 方法名 |
| `RoutePath` | 请求路由 |
| `HttpMethod` | HTTP 方法 |
| `IpAddress` | 客户端 IP |
| `UserAgent` | 客户端 User-Agent |
| `RequestData` | 请求数据（JSON） |
| `ResponseData` | 响应数据（JSON） |
| `DurationMs` | 执行耗时（毫秒） |
| `IsSuccess` | 是否成功 |
| `ErrorMessage` | 错误信息（失败时） |

### 去重与缓存

- 使用 `BlockingCollection` 缓存日志记录，每 2 分钟批量写入数据库
- 按 `(RoutePath, HttpMethod, UserId, RequestData)` 去重，避免短时间内重复记录
- 跳过 `GET /api/tasks` 和 `GET /api/useroperationlogs` 等高频查询

### 管理端点

- `GET /api/useroperationlogs` — 分页查询操作日志（仅 Admin）
- `GET /api/useroperationlogs/{id}` — 查看日志详情（仅 Admin）

---

## 数据清理

框架内置数据清理任务，定期删除过期数据。

### 配置

在 `framework.json` 中配置：

``json
"DataCleanup": {
  "IsEnabled": true,
  "Tables": [
    { "Name": "UserOperationLogs", "RetentionDays": 30 },
    { "Name": "Tasks", "RetentionDays": 30 }
  ]
}
``

### 触发方式

- 框架启动时自动入队一次 `DataCleanup` 任务
- 管理员可通过 `POST /api/tasks/cleandb` 手动触发
- 清理逻辑基于 `CreatedAt` 字段与 `RetentionDays` 比对，使用原始 SQL 删除

### 添加自定义清理表

在 `framework.json` 的 `Tables` 数组中添加新条目即可，无需编写代码。
---

## 前端架构

前端采用 Vue 3 + Tailwind CSS 构建，无需构建工具，直接在浏览器中运行。

### 技术栈

| 技术 | 用途 |
|------|------|
| Vue 3 | 响应式 UI 框架 |
| Vue Router | 客户端路由（Hash 模式） |
| Tailwind CSS | 原子化 CSS |
| Axios | HTTP 客户端 |
| SignalR | 实时通信 |

### 文件结构

`
wwwroot/
├── index.html                  # 入口 HTML（主题检测、加载第三方库）
├── css/app.css                 # 设计系统（CSS 变量、组件样式、动画）
├── js/
│   ├── app.js                  # Vue 应用创建、路由守卫、组件注册
│   ├── api.js                  # Axios 封装、API 命名空间
│   ├── auth.js                 # JWT 解析、Token 管理
│   ├── enums.js                # 枚举加载与解析
│   ├── router.js               # 路由定义
│   ├── signalr.js              # SignalR 连接管理
│   ├── utils.js                # 工具函数（日期格式化等）
│   ├── components/             # UI 组件库
│   │   ├── FButton.js          # 按钮（类型/尺寸/加载状态）
│   │   ├── FInput.js           # 输入框
│   │   ├── FCheckbox.js        # 复选框
│   │   ├── FSingleSelect.js    # 单选下拉
│   │   ├── FMultiSelect.js     # 多选下拉
│   │   ├── FModal.js           # 模态框
│   │   ├── FTable.js           # 数据表格
│   │   ├── FPagination.js      # 分页器
│   │   ├── FDropdown.js        # 下拉菜单
│   │   ├── Toast.js            # Toast 通知
│   │   ├── Layout.js           # 应用布局（侧边栏+头部+内容）
│   │   ├── icon.js             # SVG 图标集
│   │   └── index.js            # 组件导出汇总
│   └── views/                  # 页面视图
│       ├── Login.js            # 登录页
│       ├── Layout.js           # 布局包装
│       ├── TaskManager.js      # 任务管理页
│       ├── UserManager.js      # 用户管理页
│       ├── DemoManager.js      # Demo 管理页
│       └── OperationLogManager.js  # 操作日志页
└── libs/                       # 第三方库（无 npm，直接引用）
    ├── vue.js
    ├── vue-router.js
    ├── axios.min.js
    ├── signalr.min.js
    └── tailwindcss.js
`

### 主题系统

支持亮色/暗色/跟随系统三种模式，通过 CSS 变量实现：

- 亮色主题：`data-theme="light"`
- 暗色主题：`data-theme="dark"`
- 系统跟随：自动检测 `prefers-color-scheme`

所有颜色使用 CSS 变量定义在 `app.css` 中，切换主题只需修改 `data-theme` 属性。

### API 客户端约定

``javascript
import { api } from './api.js';

// 调用 API
const result = await api.demos.page(name, type, page, size);
const data = await api.demos.get(id);
await api.demos.create(name, type);
``

- 自动注入 JWT Token
- 401 响应自动登出
- 错误自动 Toast 提示

### 添加新页面

1. 在 `wwwroot/js/views/` 创建视图组件
2. 在 `wwwroot/js/router.js` 添加路由
3. 在 `wwwroot/js/components/Layout.js` 添加侧边栏导航项
4. 在 `wwwroot/js/api.js` 添加 API 方法（如需要）

---

## 部署

### Windows 服务部署

项目包含 `install.bat` 脚本，支持一键安装为 Windows 服务：
项目包含 `uninstall.bat` 脚本，支持一键卸载 Windows 服务：

``bash
# 1. 发布项目
dotnet publish -c Release

# 2. 将发布产物复制到目标服务器

# 3. 以管理员身份运行 install.bat
``

脚本会自动：
- 请求管理员权限
- 检查 exe 是否存在
- 停止已有服务（如果存在）
- 创建服务（设置为自动启动）
- 启动服务

### 发布配置

项目默认配置为：
- 单文件发布（Self-contained）
- 目标运行时：win-x64
- 禁用 Debug 符号

### 修改服务名

编辑 `install.bat`和`uninstall.bat` 中的变量：

``batch
set APP_NAME=YourApp.Exe
set SERVICE_NAME=your-service-name
``

或使用模板参数：

``bash
dotnet new sharpdev -n MyProject --serviceName myservice
``

---

## API 参考

### 认证相关

| 方法 | 路由 | 认证 | 说明 |
|------|------|------|------|
| POST | `/api/users/login` | 匿名 | 用户登录 |
| POST | `/api/users/token` | 已认证 | 刷新 Token |

### 用户管理

| 方法 | 路由 | 认证 | 说明 |
|------|------|------|------|
| GET | `/api/users` | Admin | 分页查询用户 |
| GET | `/api/users/{id}` | Admin | 获取用户详情 |
| POST | `/api/users` | Admin | 创建用户 |
| PUT | `/api/users/{id}` | Admin | 更新用户 |
| DELETE | `/api/users/{id}` | Admin | 删除用户（软删除） |

### 任务管理

| 方法 | 路由 | 认证 | 说明 |
|------|------|------|------|
| GET | `/api/tasks` | 已认证 | 分页查询任务 |
| GET | `/api/tasks/{id}` | 已认证 | 获取任务详情 |
| POST | `/api/tasks/{id}/retry` | 已认证 | 重试失败任务 |
| POST | `/api/tasks/{id}/cancel` | 已认证 | 取消进行中任务 |
| DELETE | `/api/tasks/{id}` | Admin | 删除任务 |
| POST | `/api/tasks/cleandb` | Admin | 触发数据清理 |

### 操作日志

| 方法 | 路由 | 认证 | 说明 |
|------|------|------|------|
| GET | `/api/useroperationlogs` | Admin | 分页查询操作日志 |
| GET | `/api/useroperationlogs/{id}` | Admin | 获取日志详情 |

### 枚举

| 方法 | 路由 | 认证 | 说明 |
|------|------|------|------|
| GET | `/api/enums` | 匿名 | 获取所有枚举值 |

### SignalR

| 端点 | 说明 |
|------|------|
| `/hubs/notifications` | SignalR Hub，监听 `TaskUpdated` 事件 |
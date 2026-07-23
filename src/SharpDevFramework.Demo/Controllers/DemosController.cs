using SharpDevFramework.Demo.Data;
using SharpDevFramework.Demo.Data.Entities;
using SharpDevFramework.Demo.Enums;

namespace SharpDevFramework.Demo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemosController(AppDbContext context, TaskCenter taskCenter) : ControllerBase
{
    [HttpGet]
    public PageReply<DemoDto> GetPage([FromQuery] DemoPageRequest request)
    {
        var query = context.Demos.Where(x => !x.IsDeleted);
        if (request.Name.NotNullOrWhiteSpace()) query = query.Where(x => x.Name.Contains(request.Name));
        if (request.Type.NotNullOrWhiteSpace())
        {
            var types = request.Type.SplitToList();
            var predicate = DbHelper.BuildOrLikeExpression<DemoEntity>("Type", [.. types]);
            query = query.Where(predicate);
        }
        var total = query.Count();
        var items = query.OrderByDescending(x => x.CreatedAt).Skip((request.Index - 1) * request.Size).Take(request.Size).ToList();
        return PageReply.Succeed(items.Adapt<List<DemoDto>>(), total, request);
    }

    [HttpGet("{id}")]
    public DataReply<DemoDto> Get(int id)
    {
        var demo = context.Demos.Find(id) ?? throw new Exception("data not found");
        return DataReply.Succeed(demo.Adapt<DemoDto>());
    }

    [HttpPost]
    public EmptyReply Create([FromBody] CreateDemoRequest request, [FromServices] TokenService tokenService, [FromServices] ILogger<DemosController> logger)
    {
        if (request.Name.IsNullOrWhiteSpace()) throw new Exception("名称不能为空");
        if (request.Type.IsNullOrWhiteSpace()) throw new Exception("类型不能为空");
        if (context.Demos.Any(x => x.Name == request.Name && !x.IsDeleted)) throw new Exception("名称已存在");

        var demo = new DemoEntity
        {
            Name = request.Name,
            Type = request.Type,
        };
        context.Demos.Add(demo);
        context.SaveChanges();
        var payload = HttpContext.GetJwtPayload();
        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("special token:'{Token}'", tokenService.GenerateSpecialToken(payload.UserId, payload.Username, payload.Role));
        return EmptyReply.Succeed();
    }

    [HttpPut("{id}")]
    public async Task<EmptyReply> Update(int id, [FromBody] UpdateDemoRequest request)
    {
        if (request.Name.IsNullOrWhiteSpace()) throw new Exception("名称不能为空");
        if (request.Type.IsNullOrWhiteSpace()) throw new Exception("类型不能为空");
        var demo = context.Demos.Find(id) ?? throw new Exception("data not found");
        if (context.Demos.Any(x => x.Name == request.Name && x.Id != id && !x.IsDeleted)) throw new Exception("名称已存在");
        demo.Name = request.Name;
        demo.Type = request.Type;
        demo.UpdatedAt = DateTime.Now.ToUtcTimestamp();
        context.Demos.Update(demo);
        var task1 = new TaskEntity { Type = TaskTypes.Demo };
        context.Tasks.Add(task1);
        var task2 = new TaskEntity { Type = TaskTypes.Demo };
        context.Tasks.Add(task2);
        context.SaveChanges();
        await taskCenter.PublishAsync(task1.Id);
        await taskCenter.PublishAsync(task2.Id);
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

public class DemoPageRequest : PageRequest
{
    public string? Name { get; set; }
    public string? Type { get; set; }
}

public class CreateDemoRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class UpdateDemoRequest
{
    public string? Name { get; set; }
    public string Type { get; set; } = string.Empty;
}

public class DemoDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
    public long? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharpDevLib;
using System.Reflection;

namespace SharpDevFramework;

/// <summary>
/// 枚举值控制器，提供系统中所有枚举值的查询
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EnumsController : ControllerBase
{
    internal static Assembly[] Assemblies = [];
    static List<EnumsResponse>? _cache = null;

    /// <summary>
    /// 获取所有枚举值列表
    /// </summary>
    /// <returns>枚举响应列表</returns>
    [HttpGet]
    [AllowAnonymous]
    public DataReply<List<EnumsResponse>> Get()
    {
        if (_cache is null)
        {
            _cache = [];
            var types = Assemblies.SelectMany(x => x.GetTypes()).Distinct().ToList();

            var enumTypes = types.Where(x => x.IsPublic && x.IsEnum).Select(x => new EnumsResponse { CategoryName = x.Name, Items = x.GetEnumItems() });
            _cache.AddRange(enumTypes);

            var mockTypes = types.Where(x => x.IsPublic && !x.IsAbstract && x.GetCustomAttribute<MockEnumAttribute>() is not null)
                .Select(x =>
                {
                    var categoryName = x.GetCustomAttribute<MockEnumAttribute>()!.MockEnumName;
                    var items = x.GetFields(BindingFlags.Public | BindingFlags.Static)
                        .Select(y => new EnumItemsResponse { DisplayName = y.GetDescription(), Value = y.GetValue(null) });
                    return new { categoryName, items };
                });
            _cache.AddRange(mockTypes.GroupBy(x => x.categoryName).Select(x => new EnumsResponse { CategoryName = x.Key, Items = [.. x.SelectMany(y => y.items)] }));
        }
        return DataReply.Succeed(_cache);
    }
}

/// <summary>
/// 枚举响应
/// </summary>
public class EnumsResponse
{
    /// <summary>
    /// 枚举类别名称
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// 枚举项列表
    /// </summary>
    public List<EnumItemsResponse> Items { get; set; } = [];
}

/// <summary>
/// 枚举项响应
/// </summary>
public class EnumItemsResponse
{
    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 枚举值
    /// </summary>
    public object? Value { get; set; }
}

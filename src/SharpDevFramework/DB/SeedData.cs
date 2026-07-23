using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SharpDevFramework;

/// <summary>
/// 数据库初始化数据管理
/// </summary>
internal static class SeedData
{
    /// <summary>
    /// 初始化数据库，创建默认管理员账号
    /// </summary>
    /// <param name="app">WebApplication 实例</param>
    public static void SeedDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FrameworkDbContext>();
        context.Database.Migrate();

        if (!context.Users.Any())
        {
            var userName = "root";
            var password = "12345678";
            var passwordHasher = new PasswordHasher<UserEntity>();
            var user = new UserEntity
            {
                Name = userName,
                Role = UserRoleTypes.Admin,
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            context.Users.Add(user);
            context.SaveChanges();
        }
    }
}

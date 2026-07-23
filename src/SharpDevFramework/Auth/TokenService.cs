using Microsoft.Extensions.Configuration;
using SharpDevLib;

namespace SharpDevFramework;

/// <summary>
/// JWT Token 服务，用于生成和验证 Token
/// </summary>
public class TokenService(IConfiguration configuration) : ISingletonService
{
    int? _expire = null;
    int? _specialExpireHours = null;
    byte[]? _secret = null;

    /// <summary>
    /// 获取配置信息（过期时间和密钥）
    /// </summary>
    /// <returns>过期时间（分钟）和密钥字节数组</returns>
    (int, byte[]) GetConfig()
    {
        if (_expire is null)
        {
            _expire = configuration.GetValue<int>("FrameworkSettings:JwtExpirationMinutes");
            if (_expire <= 0) _expire = 120;
        }
        if (_secret.IsNullOrEmpty())
        {
            if (!File.Exists(SharpFrameworkStatics.JwtSecretPath)) File.WriteAllText(SharpFrameworkStatics.JwtSecretPath, RandomHelper.GenerateCode(RandomType.Mix, 255));
            _secret = File.ReadAllText(SharpFrameworkStatics.JwtSecretPath).Utf8Decode();
        }
        return (_expire.Value, _secret);
    }

    /// <summary>
    ///获取 Special Token 配置信息（过期时间和密钥）
    /// </summary>
    /// <returns>密钥字节数组和过期时间（小时）</returns>
    (byte[], int) GetSpecialConfig()
    {
        if (_specialExpireHours is null)
        {
            _specialExpireHours = configuration.GetValue<int>("FrameworkSettings:JwtSpecialExpirationHours");
            if (_specialExpireHours <= 0) _specialExpireHours = 24;
        }
        if (_secret.IsNullOrEmpty())
        {
            if (!File.Exists(SharpFrameworkStatics.JwtSecretPath)) File.WriteAllText(SharpFrameworkStatics.JwtSecretPath, RandomHelper.GenerateCode(RandomType.Mix, 255));
            _secret = File.ReadAllText(SharpFrameworkStatics.JwtSecretPath).Utf8Decode();
        }
        return (_secret, _specialExpireHours.Value);
    }

    /// <summary>
    /// 生成 JWT Token
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="username">用户名</param>
    /// <param name="role">用户角色</param>
    /// <returns>JWT Token 字符串</returns>
    public string GenerateToken(int userId, string username, string role)
    {
        var (expire, secret) = GetConfig();
        var payload = new JwtPayload
        {
            UserId = userId,
            Username = username,
            Role = role,
            Type = "api",
            Exp = DateTimeOffset.UtcNow.AddMinutes(expire).ToUnixTimeSeconds(),
        };
        return JwtHelper.CreateWithHmacSha256(payload, secret);
    }

    /// <summary>
    /// 生成专用 JWT Token（用于 API 特殊接口访问，有效期按小时计）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="username">用户名</param>
    /// <param name="role">用户角色</param>
    /// <returns>JWT Token 字符串</returns>
    public string GenerateSpecialToken(int userId, string username, string role)
    {
        var (secret, hours) = GetSpecialConfig();
        var payload = new JwtPayload
        {
            UserId = userId,
            Username = username,
            Role = role,
            Type = "special",
            Exp = DateTimeOffset.UtcNow.AddHours(hours).ToUnixTimeSeconds(),
        };
        return JwtHelper.CreateWithHmacSha256(payload, secret);
    }

    /// <summary>
    /// 验证 JWT Token
    /// </summary>
    /// <param name="token">Token 字符串</param>
    /// <returns>解析后的 JWT 负载对象</returns>
    /// <exception cref="Exception">Token 无效或已过期时抛出异常</exception>
    public JwtPayload VerifyToken(string token)
    {
        var (_, secret) = GetConfig();
        var result = JwtHelper.VerifyWithHmacSha256(token, secret);
        if (!result.IsVerified) throw new Exception("token is invalid");
        var payload = result.Payload?.DeSerialize<JwtPayload>() ?? throw new Exception("token is invalid");
        var expTime = DateTimeOffset.FromUnixTimeSeconds(payload.Exp);
        if (expTime < DateTimeOffset.UtcNow) throw new Exception("token is invalid");
        return payload;
    }
}

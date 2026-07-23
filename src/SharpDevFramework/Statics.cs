using SharpDevLib;

namespace SharpDevFramework;

/// <summary>
/// 框架静态路径和目录配置
/// </summary>
public static class SharpFrameworkStatics
{
    static SharpFrameworkStatics()
    {
        if (!Directory.Exists(DatabaseDirectory)) Directory.CreateDirectory(DatabaseDirectory);
        if (!Directory.Exists(LogsDirectory)) Directory.CreateDirectory(LogsDirectory);
        if (!Directory.Exists(TempDirectory)) Directory.CreateDirectory(TempDirectory);
    }

    /// <summary>
    /// 应用程序根目录
    /// </summary>
    public static readonly string RootDirectory = AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>
    /// 数据目录
    /// </summary>
    public static readonly string DataDirectory = RootDirectory.CombinePath("data");

    /// <summary>
    /// 数据库目录
    /// </summary>
    public static readonly string DatabaseDirectory = DataDirectory.CombinePath("db");

    /// <summary>
    /// 日志目录
    /// </summary>
    public static readonly string LogsDirectory = DataDirectory.CombinePath("logs");

    /// <summary>
    /// 临时文件目录
    /// </summary>
    public static readonly string TempDirectory = DataDirectory.CombinePath("temp");

    /// <summary>
    /// 数据库文件路径
    /// </summary>
    public static readonly string DatabasePath = DatabaseDirectory.CombinePath("database.db");

    /// <summary>
    /// 日志文件路径
    /// </summary>
    public static readonly string LogsPath = LogsDirectory.CombinePath("logs-.txt");

    /// <summary>
    /// JWT 密钥文件路径
    /// </summary>
    public static readonly string JwtSecretPath = DatabaseDirectory.CombinePath("jwt-secret.txt");
}
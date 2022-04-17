using Talk.Extensions;

namespace QuartUI1._0.Common
{
    public static class AppConfig
    {
        //public static string DbProviderName => ConfigurationManager.GetTryConfig("Quartz:dbProviderName");
        //public static string ConnectionString => ConfigurationManager.GetTryConfig("Quartz:connectionString");

        public static string DbProviderName => "SQLite-Microsoft";
        public static string ConnectionString => "Data Source=File/sqliteScheduler.db;";
    }
}

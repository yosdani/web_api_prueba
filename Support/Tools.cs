using Microsoft.Extensions.Options;
using CommonTypes.Exceptions;
using CommonTypes.Settings.App;
using CommonTypes.Log;
using static CommonTypes.Language.LanguageSupport;
using CommonTypes.Util.IO;
using api_prueba.Security.Domain;
using Datamodels;
using CommonTypes.Settings;
using Microsoft.EntityFrameworkCore;

namespace api_prueba.Support
{
    internal static class Tools
    {
        private static Timer _cleanupTimer, _dbConTimer;
        private static APIFirewall apiFirewall;
        private static object _locker_firewall = new object(), _locker_timer = new object(), _locker_geoserver_uri = new object(), _locker_dbcons = new object();
        private static IServiceProvider services = null;
        private static string contentRootPath, currentEnvironment;
        internal const string dataPath = "Backstage";
        internal const string dataPathReplacement = "Storage";
        private static readonly MultilingualException exception_singletonError = new MultilingualException(new LanguageObject("Can't set once a value has already been set", "Não é possível definir uma vez que um valor já foi definido"));
        private static readonly MultilingualException exception_dbConError = new MultilingualException(new LanguageObject("Cannot connect to the database", "No es posibe conectar a a base de datos"));
        private static Context[] contexts;
        private static int currentDBConIndex = -1;
        private static HashSet<int> usedIndicator = new HashSet<int>();

        internal static string ContentRootPath
        {
            get => contentRootPath;
            set
            {
                if (contentRootPath != null)
                    throw exception_singletonError;
                contentRootPath = value;
                DataPath = Path.Combine(contentRootPath, dataPath);
                ResourcesPath = Path.Combine(contentRootPath, "Res");
                TemplatesPath = Path.Combine(ResourcesPath, "Templates");
                TempPath = Path.Combine(DataPath, "_Temp_");
                DatasourcePath = Path.Combine(TempPath, "_Datasources_");
                ImportPath = Path.Combine(TempPath, "_Import_");
                Task.WaitAll(Task.Run(() => Resource_RegistrationFooter = File.ReadAllBytes(Path.Combine(TemplatesPath, "registration_footer.png"))), Task.Run(() => Resource_Registration_En = File.ReadAllText(Path.Combine(TemplatesPath, "registration_en.htm"))), Task.Run(() => Resource_Registration_Pt = File.ReadAllText(Path.Combine(TemplatesPath, "registration_pt.htm"))), Task.Run(() => Resource_Recovery_En = File.ReadAllText(Path.Combine(TemplatesPath, "recovery_en.htm"))), Task.Run(() => Resource_Recovery_Pt = File.ReadAllText(Path.Combine(TemplatesPath, "recovery_pt.htm"))), Task.Run(() => Resource_Contact = File.ReadAllText(Path.Combine(TemplatesPath, "contact_neutral.htm"))));
            }
        }

        internal static string CurrentEnvironment
        {
            get => currentEnvironment;
            set
            {
                if (currentEnvironment != null)
                    throw exception_singletonError;
                currentEnvironment = value;
            }
        }

        internal static string DataPath { get; private set; }

        internal static string TempPath { get; private set; }

        internal static string DatasourcePath { get; private set; }

        internal static string ImportPath { get; private set; }

        internal static string ResourcesPath { get; private set; }

        internal static string TemplatesPath { get; private set; }

        #region Resources
        internal static byte[] Resource_RegistrationFooter { get; private set; }

        internal static string Resource_Registration_En { get; private set; }

        internal static string Resource_Registration_Pt { get; private set; }

        internal static string Resource_Recovery_En { get; private set; }

        internal static string Resource_Recovery_Pt { get; private set; }

        internal static string Resource_Contact { get; private set; }
        #endregion


        /// <summary>
        /// Provides static access to the framework's services provider
        /// </summary>
        internal static IServiceProvider Services
        {
            get => services;
            set
            {
                if (services != null)
                    throw exception_singletonError;
                services = value;
            }
        }

        /// <summary>
        /// Provides static access to the current HttpContext
        /// </summary>
        internal static HttpContext HttpContext_Current
        {
            get
            {
                IHttpContextAccessor httpContextAccessor = services.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
                return httpContextAccessor?.HttpContext;
            }
        }

        internal static AppSettings Settings
        {
            get
            {
                //This works to get file changes.
                IOptionsMonitor<AppSettings> item = services.GetService(typeof(IOptionsMonitor<AppSettings>)) as IOptionsMonitor<AppSettings>;
                AppSettings value = item.CurrentValue;
                return value;
            }
        }

        internal static DBKeysSettings DBKeys
        {
            get
            {
                //This works to get file changes.
                IOptionsMonitor<DBKeysSettings> item = services.GetService(typeof(IOptionsMonitor<DBKeysSettings>)) as IOptionsMonitor<DBKeysSettings>;
                DBKeysSettings value = item.CurrentValue;
                return value;
            }
        }

        internal static LogWriter LogWriter => services.GetService(typeof(LogWriter)) as LogWriter;


        internal static APIFirewall ApiFirewall
        {
            get
            {
                lock (_locker_firewall)
                    if (apiFirewall == null)
                    {
                        string whiteListPath = null, blackListPath = null;
                        if (!string.IsNullOrWhiteSpace(contentRootPath))
                        {
                            if (!string.IsNullOrWhiteSpace(Settings.APISecurity.Whitelist))
                                whiteListPath = Path.Combine(contentRootPath, Settings.APISecurity.Whitelist);
                            if (!string.IsNullOrWhiteSpace(Settings.APISecurity.Blacklist))
                                blackListPath = Path.Combine(contentRootPath, Settings.APISecurity.Blacklist);
                        }
                        apiFirewall = new APIFirewall(whiteListPath, blackListPath, Settings.Timers.WriteWaitSeconds);
                    }
                return apiFirewall;
            }
        }

        internal static async Task<string> ConnectionString()
        {
            int attemps = 0, i, count;
            Context c;
            AppSettings settings = null;
            do
            {
                i = currentDBConIndex < 0 ? 0 : currentDBConIndex;
                count = 0;
                while (count++ < contexts.Length)
                    try
                    {
                        i %= contexts.Length;
                        if (await (c = contexts[i]).Database.CanConnectAsync())
                        {
                            if (currentDBConIndex != i)
                                currentDBConIndex = i;
                            return c.Database.GetConnectionString();
                        }
                        i++;
                    }
                    catch { }
                if (currentDBConIndex > -1)
                    currentDBConIndex = -1;
                if (settings == null)
                    settings = Settings;
            }
            while (++attemps < settings.DataAccess.ConnectionAttempts);
            throw exception_dbConError;
        }

        internal static void ConfigureDBConnections()
        {
            string[] dbCons;
            lock (_locker_dbcons)
            {
                AppSettings settings;
                if (contexts == null && (dbCons = (settings = Settings).DataAccess.DBConnections).Any() == true)
                {
                    contexts = dbCons.Select(con => new Context(con)).ToArray();
                    if (settings.DataAccess.ResetConnectionMinutes > 0)
                    {
                        _dbConTimer = new Timer(ResetDBConnections);
                        _dbConTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(settings.DataAccess.ResetConnectionMinutes));
                    }
                }
            }
        }

        private static void ResetDBConnections(object state)
        {
            if (currentDBConIndex > 0)
                currentDBConIndex = -1;
        }

        internal static void StartCleanupTimer()
        {
            double interval = Settings.Timers.CleanupIntervalHours;
            if (interval > 0 && _cleanupTimer == null)
                lock (_locker_timer)
                    if (_cleanupTimer == null)
                    {
                        _cleanupTimer = new Timer(TimerProc);
                        _cleanupTimer.Change(TimeSpan.Zero, TimeSpan.FromHours(interval));
                    }
        }

        private static void TimerProc(object state)
        {
            LogWriter logger = LogWriter;
            AppSettings settings = Settings;

        }



        internal static void CleanTemps() => IO.CleanFolder(TempPath);

        internal static bool ReserveIndicator(int indicatorId)
        {
            lock (usedIndicator)
                return usedIndicator.Add(indicatorId);
        }

        internal static bool ReleaseIndicator(int indicatorId)
        {
            lock (usedIndicator)
                return usedIndicator.Remove(indicatorId);
        }
    }
}

using CommonTypes.Settings.App.AppSettingsItems;
using CommonTypes.Util.IO;
using CommonTypes.Util.Text;
using NetTools;
using System.Text.Json;

namespace api_prueba.Security.Domain
{
    public class SecurityIPBlocker
    {
        private readonly string path;
        private readonly IPConfigs config;
        private readonly List<IPAddressRange> range;
        private readonly double writeWaitSeconds;
        private const string noPathError = "No path defined for current instance";

        public SecurityIPBlocker(IPConfigs config = null, double writeWaitSeconds = 0)
        {
            this.config = config ?? new IPConfigs();
            range = this.config.IPs.Select(IPAddressRange.Parse).ToList();
            this.writeWaitSeconds = writeWaitSeconds;
        }

        public SecurityIPBlocker(string path, IPConfigs config = null, double writeWaitSeconds = 0) : this(config, writeWaitSeconds) => this.path = path;

        public void AddIpRange(string ipRange)
        {
            range.Add(IPAddressRange.Parse(ipRange));
            config.IPs.Add(ipRange);
        }

        public void RemoveIpRange(string ipRange)
        {
            ipRange = ipRange.Trim();
            for (int i = 0; i < config.IPs.Count; i++)
                if (config.IPs[i] == ipRange)
                    RemoveIpRange(i--);
        }

        public void RemoveIpRange(int pos)
        {
            range.RemoveAt(pos);
            config.IPs.RemoveAt(pos);
        }

        public static SecurityIPBlocker Load(string path, double writeWaitSeconds = 0)
        {
            if (!File.Exists(path))
                File.Create(path).Dispose();
            return new SecurityIPBlocker(path, JsonSerializer.Deserialize<IPConfigs>(File.ReadAllText(path)), writeWaitSeconds);
        }

        public void Save()
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException(noPathError);
            Task.Run(() => IO.SaveFile(Path.GetDirectoryName(path), JsonSerializer.Serialize(config, JSONSupport.jsonSerializerOptions_Write), Path.GetFileName(path), writeWaitSeconds));
        }

        public IEnumerable<IPAddressRange> Range() => range.AsEnumerable();
    }
}

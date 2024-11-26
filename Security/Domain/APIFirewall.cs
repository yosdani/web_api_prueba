using NetTools;
namespace api_prueba.Security.Domain
{
    public class APIFirewall
    {
        private static SecurityIPBlocker whiteList, blackList;

        public APIFirewall(string whiteListPath = null, string blackListPath = null, double writeWaitSeconds = 0)
        {
            if (!string.IsNullOrWhiteSpace(whiteListPath))
                whiteList = SecurityIPBlocker.Load(whiteListPath, writeWaitSeconds);
            if (!string.IsNullOrWhiteSpace(blackListPath))
                blackList = SecurityIPBlocker.Load(blackListPath, writeWaitSeconds);
            if (whiteList == null)
                whiteList = new SecurityIPBlocker();
            if (blackList == null)
                blackList = new SecurityIPBlocker();
        }

        public IEnumerable<IPAddressRange> WhiteList() => whiteList.Range();

        public IEnumerable<IPAddressRange> BlackList() => blackList.Range();
    }
}

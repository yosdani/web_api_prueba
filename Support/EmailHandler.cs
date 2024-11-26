using CommonTypes.Settings.App.AppSettingsItems;
using System.Net.Mail;
using static CommonTypes.Util.MailService.MailService;

namespace api_prueba.Support
{
    internal static class EmailHandler
    {
        internal static void Send(Mail sender, Dictionary<string, bool> receivers, string topic, string message, MessageType messageType, Dictionary<string, Tuple<string, byte[]>> images = null)
        {
            foreach (KeyValuePair<string, bool> receiver in receivers)
                try
                {
                    SendEmail(receiver.Key, topic, message, sender.Host, sender.Port, sender.DefaultCredentials, sender.Ssl, sender.User, sender.Password, messageType, images);
                }
                catch (SmtpFailedRecipientException)
                {
                    if (receiver.Value)
                        throw;
                }
        }
    }
}

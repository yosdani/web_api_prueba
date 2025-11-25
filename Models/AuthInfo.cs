namespace api_prueba.Models
{
    public class AuthInfo
    {
        public string Username { get; set; }
        public string Password { get; set; }
       
        public bool? fullAuthentication { get; set; }
    }
}

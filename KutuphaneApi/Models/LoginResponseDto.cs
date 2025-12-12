namespace KutuphaneApi.Models
{
    public class LoginResponseDto
    {
        public int LibrarianId { get; set; }
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
    }
}

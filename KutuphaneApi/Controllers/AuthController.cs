using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using KutuphaneApi.Models;

namespace KutuphaneApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        public AuthController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Email ve şifre zorunlu.");

            var connStr = _config.GetConnectionString("KutuphaneDB");
            if (string.IsNullOrWhiteSpace(connStr))
                return StatusCode(500, "Connection string bulunamadı (KutuphaneDB).");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // ⚠️ Şimdilik düz şifre kontrolü (DB'de password düz tutulduğu için)
            var cmd = new SqlCommand(@"
                SELECT TOP 1 librarian_id, username, email, role
                FROM dbo.Librarians
                WHERE email = @email AND password = @password
            ", conn);

            cmd.Parameters.AddWithValue("@email", dto.Email);
            cmd.Parameters.AddWithValue("@password", dto.Password);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return Unauthorized("Email veya şifre yanlış.");

            var response = new LoginResponseDto
            {
                LibrarianId = reader.GetInt32(reader.GetOrdinal("librarian_id")),
                Username = reader.GetString(reader.GetOrdinal("username")),
                Email = reader.GetString(reader.GetOrdinal("email")),
                Role = reader.GetString(reader.GetOrdinal("role"))
            };

            return Ok(response);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using KutuphaneApi.Models;

namespace KutuphaneApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly IConfiguration _config;

        public BooksController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BookDto>>> GetBooks()
        {
            var list = new List<BookDto>();
            var connStr = _config.GetConnectionString("KutuphaneDB");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT book_id, title, author, category, created_at, available_copies 
                FROM Books
                ORDER BY created_at DESC;", conn);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                int copies = reader.GetInt32(reader.GetOrdinal("available_copies"));

                list.Add(new BookDto
                {
                    Id = reader.GetInt32(reader.GetOrdinal("book_id")),
                    Ad = reader.GetString(reader.GetOrdinal("title")),
                    Yazar = reader.GetString(reader.GetOrdinal("author")),
                    Kategori = reader.GetString(reader.GetOrdinal("category")),
                    Tarih = reader.GetDateTime(reader.GetOrdinal("created_at"))
                                 .ToString("yyyy-MM-dd"),
                    Durum = copies > 0 ? "Mevcut" : "Ödünçte"
                });
            }

            return Ok(list);
        }
    }
}

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

        // GET: /api/Books
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BookDto>>> GetBooks()
        {
            var list = new List<BookDto>();
            var connStr = _config.GetConnectionString("KutuphaneDB");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT 
                    book_id, title, author, category, created_at, available_copies,
                    image_url, location
                FROM dbo.Books
                ORDER BY created_at DESC;", conn);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                int copies = reader.GetInt32(reader.GetOrdinal("available_copies"));

                list.Add(new BookDto
                {
                    Id = reader.GetInt32(reader.GetOrdinal("book_id")),
                    Ad = reader.IsDBNull(reader.GetOrdinal("title")) ? "" : reader.GetString(reader.GetOrdinal("title")),
                    Yazar = reader.IsDBNull(reader.GetOrdinal("author")) ? "" : reader.GetString(reader.GetOrdinal("author")),
                    Kategori = reader.IsDBNull(reader.GetOrdinal("category")) ? "" : reader.GetString(reader.GetOrdinal("category")),
                    Tarih = reader.GetDateTime(reader.GetOrdinal("created_at")).ToString("yyyy-MM-dd"),
                    Durum = copies > 0 ? "Mevcut" : "Ödünçte",

                    ImageUrl = reader.IsDBNull(reader.GetOrdinal("image_url")) ? null : reader.GetString(reader.GetOrdinal("image_url")),
                    Location = reader.IsDBNull(reader.GetOrdinal("location")) ? null : reader.GetString(reader.GetOrdinal("location")),
                });
            }

            return Ok(list);
        }

        // GET: /api/Books/search?q=orwell
        // GET: /api/Books/search?q=roman&available_only=true
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<BookDto>>> SearchBooks(
            [FromQuery] string? q,
            [FromQuery(Name = "available_only")] bool availableOnly = false)
        {
            q = (q ?? "").Trim();
            if (q.Length == 0) return Ok(new List<BookDto>());

            var list = new List<BookDto>();
            var connStr = _config.GetConnectionString("KutuphaneDB");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT 
                    book_id, title, author, category, created_at, available_copies,
                    image_url, location
                FROM dbo.Books
                WHERE
                    (title LIKE '%' + @q + '%'
                     OR author LIKE '%' + @q + '%'
                     OR category LIKE '%' + @q + '%'
                     OR isbn LIKE '%' + @q + '%')
                    AND (@availableOnly = 0 OR available_copies > 0)
                ORDER BY created_at DESC;", conn);

            cmd.Parameters.AddWithValue("@q", q);
            cmd.Parameters.AddWithValue("@availableOnly", availableOnly ? 1 : 0);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                int copies = reader.GetInt32(reader.GetOrdinal("available_copies"));

                list.Add(new BookDto
                {
                    Id = reader.GetInt32(reader.GetOrdinal("book_id")),
                    Ad = reader.IsDBNull(reader.GetOrdinal("title")) ? "" : reader.GetString(reader.GetOrdinal("title")),
                    Yazar = reader.IsDBNull(reader.GetOrdinal("author")) ? "" : reader.GetString(reader.GetOrdinal("author")),
                    Kategori = reader.IsDBNull(reader.GetOrdinal("category")) ? "" : reader.GetString(reader.GetOrdinal("category")),
                    Tarih = reader.GetDateTime(reader.GetOrdinal("created_at")).ToString("yyyy-MM-dd"),
                    Durum = copies > 0 ? "Mevcut" : "Ödünçte",

                    ImageUrl = reader.IsDBNull(reader.GetOrdinal("image_url")) ? null : reader.GetString(reader.GetOrdinal("image_url")),
                    Location = reader.IsDBNull(reader.GetOrdinal("location")) ? null : reader.GetString(reader.GetOrdinal("location")),
                });
            }

            return Ok(list);
        }
    }
}

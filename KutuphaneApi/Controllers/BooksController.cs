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
                SELECT book_id, title, author, category, created_at, available_copies 
                FROM dbo.Books
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
                    Tarih = reader.GetDateTime(reader.GetOrdinal("created_at")).ToString("yyyy-MM-dd"),
                    Durum = copies > 0 ? "Mevcut" : "Ödünçte"
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
                SELECT book_id, title, author, category, created_at, available_copies
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
                    Ad = reader.GetString(reader.GetOrdinal("title")),
                    Yazar = reader.GetString(reader.GetOrdinal("author")),
                    Kategori = reader.GetString(reader.GetOrdinal("category")),
                    Tarih = reader.GetDateTime(reader.GetOrdinal("created_at")).ToString("yyyy-MM-dd"),
                    Durum = copies > 0 ? "Mevcut" : "Ödünçte"
                });
            }

            return Ok(list);
        }

        // POST: /api/Books
        [HttpPost]
        public async Task<ActionResult> AddBook([FromBody] BookAddDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var connStr = _config.GetConnectionString("KutuphaneDB");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                INSERT INTO dbo.Books (title, author, category, isbn, total_copies, available_copies, created_at)
                VALUES (@title, @author, @category, @isbn, @totalCopies, @availableCopies, GETDATE());
                SELECT SCOPE_IDENTITY();", conn);

            cmd.Parameters.AddWithValue("@title", request.Title);
            cmd.Parameters.AddWithValue("@author", (object?)request.Author ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@category", (object?)request.Category ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@isbn", (object?)request.Isbn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@totalCopies", request.TotalCopies);
            cmd.Parameters.AddWithValue("@availableCopies", request.TotalCopies);

            try
            {
                var newId = await cmd.ExecuteScalarAsync();
                return CreatedAtAction(nameof(GetBooks), new { id = Convert.ToInt32(newId) }, new { id = Convert.ToInt32(newId) });
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return BadRequest(new { error = "A book with this ISBN already exists." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = "Database error occurred.", details = ex.Message });
            }
        }

        // DELETE: /api/Books/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> RemoveBook(int id)
        {
            var connStr = _config.GetConnectionString("KutuphaneDB");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                DELETE FROM dbo.Books 
                WHERE book_id = @bookId;
                SELECT @@ROWCOUNT;", conn);

            cmd.Parameters.AddWithValue("@bookId", id);

            try
            {
                var result = await cmd.ExecuteScalarAsync();
                var rowsAffected = result != null ? Convert.ToInt32(result) : 0;

                if (rowsAffected == 0)
                {
                    return NotFound(new { error = $"Book with ID {id} not found." });
                }

                return NoContent();
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = "Database error occurred.", details = ex.Message });
            }
        }
    }
}

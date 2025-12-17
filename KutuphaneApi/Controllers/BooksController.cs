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
        public async Task<ActionResult<BookDto>> AddBook([FromBody] BookAddDto newBook)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var connStr = _config.GetConnectionString("KutuphaneDB");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            if (!string.IsNullOrWhiteSpace(newBook.Isbn))
            {
                var checkCmd = new SqlCommand(@"
                    SELECT book_id, total_copies, available_copies 
                    FROM dbo.Books 
                    WHERE isbn = @isbn;", conn);

                checkCmd.Parameters.AddWithValue("@isbn", newBook.Isbn);

                using var checkReader = await checkCmd.ExecuteReaderAsync();

                if (await checkReader.ReadAsync())
                {
                    // Book exists, update the copies
                    int existingId = checkReader.GetInt32(0);
                    int existingTotal = checkReader.GetInt32(1);
                    int existingAvailable = checkReader.GetInt32(2);
                    await checkReader.CloseAsync();

                    int newTotal = existingTotal + newBook.TotalCopy;
                    int newAvailable = existingAvailable + newBook.TotalCopy;

                    var updateCmd = new SqlCommand(@"
                        UPDATE dbo.Books 
                        SET total_copies = @totalCopies, 
                            available_copies = @availableCopies
                        WHERE book_id = @id;", conn);

                    updateCmd.Parameters.AddWithValue("@totalCopies", newTotal);
                    updateCmd.Parameters.AddWithValue("@availableCopies", newAvailable);
                    updateCmd.Parameters.AddWithValue("@id", existingId);

                    await updateCmd.ExecuteNonQueryAsync();

                    // Fetch the updated book
                    var selectCmd = new SqlCommand(@"
                        SELECT 
                            book_id, title, author, category, created_at, available_copies, total_copies,
                            image_url, location
                        FROM dbo.Books
                        WHERE book_id = @id;", conn);

                    selectCmd.Parameters.AddWithValue("@id", existingId);

                    using var reader = await selectCmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        int copies = reader.GetInt32(reader.GetOrdinal("available_copies"));

                        var bookDto = new BookDto
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("book_id")),
                            Ad = reader.IsDBNull(reader.GetOrdinal("title")) ? "" : reader.GetString(reader.GetOrdinal("title")),
                            Yazar = reader.IsDBNull(reader.GetOrdinal("author")) ? "" : reader.GetString(reader.GetOrdinal("author")),
                            Kategori = reader.IsDBNull(reader.GetOrdinal("category")) ? "" : reader.GetString(reader.GetOrdinal("category")),
                            Tarih = reader.GetDateTime(reader.GetOrdinal("created_at")).ToString("yyyy-MM-dd"),
                            Durum = copies > 0 ? "Mevcut" : "Ödünçte",
                        };

                        return Ok(new { message = "Mevcut kitaba kopya eklendi", book = bookDto });
                    }
                }
            }

            // Insert new book if ISBN doesn't exist or is empty
            var cmd = new SqlCommand(@"
                INSERT INTO dbo.Books 
                    (title, author, category, isbn, total_copies, available_copies, image_url, location)
                VALUES 
                    (@title, @author, @category, @isbn, @totalCopies, @availableCopies, @imageUrl, @location);
                
                SELECT SCOPE_IDENTITY();", conn);

            cmd.Parameters.AddWithValue("@title", newBook.Name);
            cmd.Parameters.AddWithValue("@author", (object?)newBook.Author ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@category", (object?)newBook.Category ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@isbn", (object?)newBook.Isbn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@totalCopies", newBook.TotalCopy);
            cmd.Parameters.AddWithValue("@availableCopies", newBook.TotalCopy); // available_copies = total_copies
            cmd.Parameters.AddWithValue("@imageUrl", (object?)newBook.ImageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@location", (object?)newBook.Location ?? DBNull.Value);

            try
            {
                var result = await cmd.ExecuteScalarAsync();
                int newId = Convert.ToInt32(result);

                // Fetch the newly created book
                var selectCmd = new SqlCommand(@"
                    SELECT 
                        book_id, title, author, category, created_at, available_copies, total_copies,
                        image_url, location
                    FROM dbo.Books
                    WHERE book_id = @id;", conn);

                selectCmd.Parameters.AddWithValue("@id", newId);

                using var reader = await selectCmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    int copies = reader.GetInt32(reader.GetOrdinal("available_copies"));

                    var bookDto = new BookDto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("book_id")),
                        Ad = reader.IsDBNull(reader.GetOrdinal("title")) ? "" : reader.GetString(reader.GetOrdinal("title")),
                        Yazar = reader.IsDBNull(reader.GetOrdinal("author")) ? "" : reader.GetString(reader.GetOrdinal("author")),
                        Kategori = reader.IsDBNull(reader.GetOrdinal("category")) ? "" : reader.GetString(reader.GetOrdinal("category")),
                        Tarih = reader.GetDateTime(reader.GetOrdinal("created_at")).ToString("yyyy-MM-dd"),
                        Durum = copies > 0 ? "Mevcut" : "Ödünçte",
                    };

                    return CreatedAtAction(nameof(GetBooks), new { id = newId }, bookDto);
                }

                return StatusCode(500, "Kitap oluşturuldu ancak geri alınamadı");
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601) // Unique constraint violation
            {
                return Conflict(new { message = "Bu ISBN numarası zaten kullanılıyor" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Kitap eklenirken bir hata oluştu", error = ex.Message });
            }
        }

        // DELETE: /api/Books/{id}?amount=1
        [HttpDelete("{id}")]
        public async Task<ActionResult> RemoveBook(int id, [FromQuery] int amount = 1)
        {
            if (amount <= 0)
            {
                 return BadRequest(new { message = "Silinecek miktar 1 veya daha büyük olmalıdır." });
            }

            var connStr = _config.GetConnectionString("KutuphaneDB");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // Check if book exists and get copy information
            var checkCmd = new SqlCommand(@"
                SELECT total_copies, available_copies 
                FROM dbo.Books 
                WHERE book_id = @id", conn);
            checkCmd.Parameters.AddWithValue("@id", id);

            using var reader = await checkCmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new { message = $"ID {id} ile eşleşen kitap bulunamadı" });
            }

            int totalCopies = reader.GetInt32(0);
            int availableCopies = reader.GetInt32(1);
            await reader.CloseAsync();

            if (availableCopies < amount)
            {
                return BadRequest(new { 
                    message = $"Rafta yeterli kopya yok. Mevcut: {availableCopies}, Silinmek istenen: {amount}", 
                    totalCopies, 
                    availableCopies 
                });
            }

            try
            {
                // If we are deleting all copies (and implicitly all are available), delete the row
                if (totalCopies == amount)
                {
                    var deleteCmd = new SqlCommand("DELETE FROM dbo.Books WHERE book_id = @id", conn);
                    deleteCmd.Parameters.AddWithValue("@id", id);
                    
                    await deleteCmd.ExecuteNonQueryAsync();
                    return Ok(new { message = "Kitabın tüm kopyaları ve kaydı başarıyla silindi" });
                }
                else
                {
                    // Reduce the number of copies by 'amount'
                    var updateCmd = new SqlCommand(@"
                        UPDATE dbo.Books 
                        SET total_copies = total_copies - @amount, 
                            available_copies = available_copies - @amount
                        WHERE book_id = @id", conn);
                    updateCmd.Parameters.AddWithValue("@id", id);
                    updateCmd.Parameters.AddWithValue("@amount", amount);
                    
                    await updateCmd.ExecuteNonQueryAsync();
                    
                    int newTotal = totalCopies - amount;
                    int newAvailable = availableCopies - amount;

                    return Ok(new { 
                        message = $"{amount} adet kitap kopyası silindi", 
                        remainingTotal = newTotal,
                        remainingAvailable = newAvailable
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Kitap silinirken bir hata oluştu", error = ex.Message });
            }
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using KutuphaneApi.Models;

namespace KutuphaneApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ReviewsController(IConfiguration config)
        {
            _config = config;
        }

        // POST: /api/Reviews
        [HttpPost]
        public async Task<ActionResult> AddReview([FromBody] BookReviewAddDto model)
        {
            model.Comment = (model.Comment ?? "").Trim();
            if (string.IsNullOrEmpty(model.Comment) || model.Comment.Length < 3)
            {
                return BadRequest(new { message = "Lütfen en az 3 karakterlik geçerli bir yorum yazınız." });
            }

            if (model.Rating < 1 || model.Rating > 5)
                return BadRequest(new { message = "Puan 1 ile 5 arasında olmalıdır." });

            var connStr = _config.GetConnectionString("KutuphaneDB");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var checkCmd = new SqlCommand("SELECT review_id FROM BookReviews WHERE book_id = @bId AND student_id = @sId", conn);
            checkCmd.Parameters.AddWithValue("@bId", model.BookId);
            checkCmd.Parameters.AddWithValue("@sId", model.StudentId);

            var existingIdObj = await checkCmd.ExecuteScalarAsync();

            if (existingIdObj != null)
            {
                var updateCmd = new SqlCommand(@"
                    UPDATE BookReviews 
                    SET comment = @comment, 
                        rating = @rating, 
                        updated_at = GETDATE() 
                    WHERE review_id = @id", conn);

                updateCmd.Parameters.AddWithValue("@comment", model.Comment);
                updateCmd.Parameters.AddWithValue("@rating", model.Rating);
                updateCmd.Parameters.AddWithValue("@id", (int)existingIdObj);
                
                await updateCmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Değerlendirmeniz güncellendi." });
            }
            else
            {
                var insertCmd = new SqlCommand(@"
                    INSERT INTO BookReviews (book_id, student_id, comment, rating) 
                    VALUES (@bId, @sId, @comment, @rating)", conn);

                insertCmd.Parameters.AddWithValue("@bId", model.BookId);
                insertCmd.Parameters.AddWithValue("@sId", model.StudentId);
                insertCmd.Parameters.AddWithValue("@comment", model.Comment);
                insertCmd.Parameters.AddWithValue("@rating", model.Rating);

                await insertCmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Değerlendirmeniz kaydedildi." });
            }
        }

        // GET: /api/Reviews/book/{bookId}
        [HttpGet("book/{bookId}")]
        public async Task<ActionResult<BookReviewResponseDto>> GetBookReviews(int bookId)
        {
            if (bookId <= 0)
                return BadRequest(new { message = "Geçersiz kitap ID'si girdiniz." });

            var response = new BookReviewResponseDto
            {
                BookId = bookId,
                Reviews = new List<ReviewDetailDto>()
            };

            var connStr = _config.GetConnectionString("KutuphaneDB");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var checkBookCmd = new SqlCommand("SELECT book_id FROM Books WHERE book_id = @id", conn);
            checkBookCmd.Parameters.AddWithValue("@id", bookId);
            
            var bookExists = await checkBookCmd.ExecuteScalarAsync();

            if (bookExists == null)
            {
                return NotFound(new { message = $"ID'si {bookId} olan kitap bulunamadı." });
            }

            var avgCmd = new SqlCommand(@"
                SELECT AVG(CAST(rating AS FLOAT)) 
                FROM BookReviews 
                WHERE book_id = @id AND rating IS NOT NULL", conn);
            
            avgCmd.Parameters.AddWithValue("@id", bookId);
            
            var avgResult = await avgCmd.ExecuteScalarAsync();
            if (avgResult != DBNull.Value && avgResult != null)
            {
                response.AverageRating = Math.Round(Convert.ToDouble(avgResult), 1);
            }
            else
            {
                response.AverageRating = 0;
            }

            var listCmd = new SqlCommand(@"
                SELECT 
                    s.name, 
                    s.surname, 
                    r.rating, 
                    r.comment, 
                    r.created_at
                FROM BookReviews r
                INNER JOIN Students s ON r.student_id = s.student_id
                WHERE r.book_id = @id
                ORDER BY r.created_at DESC", conn);

            listCmd.Parameters.AddWithValue("@id", bookId);

            using var reader = await listCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                response.Reviews.Add(new ReviewDetailDto
                {
                    StudentName = reader.GetString(0) + " " + reader.GetString(1),
                    Rating = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    Comment = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Date = reader.GetDateTime(4).ToString("yyyy-MM-dd")
                });
            }

            return Ok(response);
        }

        // GET: /api/Reviews/top-rated
        [HttpGet("top-rated")]
        public async Task<ActionResult<IEnumerable<TopBookDto>>> GetTopBooks()
        {
            var list = new List<TopBookDto>();
            var connStr = _config.GetConnectionString("KutuphaneDB");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var sql = @"
                SELECT TOP 10 
                    b.book_id, b.title, b.author, b.category, b.created_at, b.available_copies,
                    AVG(CAST(r.rating AS FLOAT)) as AvgRating,
                    COUNT(r.comment) as CommentCount
                FROM dbo.Books b
                INNER JOIN dbo.BookReviews r ON b.book_id = r.book_id
                WHERE r.rating IS NOT NULL
                GROUP BY b.book_id, b.title, b.author, b.category, b.created_at, b.available_copies
                ORDER BY AvgRating DESC";

            var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                int copies = reader.GetInt32(reader.GetOrdinal("available_copies"));
                
                list.Add(new TopBookDto
                {
                    Id = reader.GetInt32(reader.GetOrdinal("book_id")),
                    Ad = reader.GetString(reader.GetOrdinal("title")),
                    Yazar = reader.GetString(reader.GetOrdinal("author")),
                    Kategori = reader.IsDBNull(reader.GetOrdinal("category")) ? "" : reader.GetString(reader.GetOrdinal("category")),
                    Tarih = reader.GetDateTime(reader.GetOrdinal("created_at")).ToString("yyyy-MM-dd"),
                    Durum = copies > 0 ? "Mevcut" : "Ödünçte",
                    OrtalamaPuan = Math.Round(reader.GetDouble(reader.GetOrdinal("AvgRating")), 1),
                    YorumSayisi = reader.GetInt32(reader.GetOrdinal("CommentCount"))
                });
            }

            return Ok(list);
        }
    }
}
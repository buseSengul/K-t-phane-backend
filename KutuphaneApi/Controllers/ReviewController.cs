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
    }
}
namespace KutuphaneApi.Models
{
    public class BookReviewResponseDto
    {
        public int BookId { get; set; }
        public double AverageRating { get; set; }
        public List<ReviewDetailDto> Reviews { get; set; } = new List<ReviewDetailDto>();
    }

    public class ReviewDetailDto
    {
        public string StudentName { get; set; } = "";
        public int Rating { get; set; }
        public string Comment { get; set; } = "";
        public string Date { get; set; } = "";
    }
}
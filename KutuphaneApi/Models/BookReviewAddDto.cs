namespace KutuphaneApi.Models
{
    public class BookReviewAddDto
    {
        public int StudentId { get; set; }
        public int BookId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = "";
    }
}
namespace KutuphaneApi.Models
{
    public class BookAddDto
    {
        public string Title { get; set; } = "";
        public string? Author { get; set; }
        public string? Category { get; set; }
        public string? Isbn { get; set; }
        public int TotalCopies { get; set; } = 1;
    }
}

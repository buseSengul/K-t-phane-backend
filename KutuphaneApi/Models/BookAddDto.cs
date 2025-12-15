namespace KutuphaneApi.Models
{
    public class BookAddDto
    {
        public string Name { get; set; } = "";
        public string? Author { get; set; }
        public string? Category { get; set; }
        public string? Isbn { get; set; }
        public int TotalCopy { get; set; } = 1;
        public string? ImageUrl { get; set; }
        public string? Location { get; set; }
    }
}

namespace KutuphaneApi.Models
{
    public class BookDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = "";
        public string Yazar { get; set; } = "";
        public string Kategori { get; set; } = "";
        public string Tarih { get; set; } = "";
        public string Durum { get; set; } = "";

        public string? ImageUrl { get; set; }
        public string? Location { get; set; }
    }
}

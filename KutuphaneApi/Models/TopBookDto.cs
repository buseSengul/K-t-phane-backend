namespace KutuphaneApi.Models
{
    public class TopBookDto : BookDto
    {
        public double OrtalamaPuan { get; set; }
        public int YorumSayisi { get; set; }
    }
}
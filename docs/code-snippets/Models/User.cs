namespace ServerService.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
    }
}
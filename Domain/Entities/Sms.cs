namespace Domain.Entities
{
    public class Sms
    {
        public int Id { get; set; }
        public string Meesage { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime SendTime { get; set; }

    }
}

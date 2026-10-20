namespace Domain.Entities.Post
{
    public class PostComment
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public bool IsPublish { get; set; }
        public DateTime CreateDate { get; set; }
        public int PostId { get; set; }
        public Post Post { get; set; } = null!;

    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.Post
{
    public class Post
    {
        [Key]
        public int PostId { get; set; }

        [Display(Name = "Title")]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        [Required(ErrorMessage = "Please enter {0}!")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [MaxLength(500, ErrorMessage = "{0} cannot be more than {1} characters")]
        [Required(ErrorMessage = "Please enter {0}!")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Meta Description")]
        [MaxLength(160, ErrorMessage = "{0} cannot be more than {1} characters")]
        [Required(ErrorMessage = "Please enter {0}!")]
        public string MetaDescription { get; set; } = string.Empty;

        [Display(Name = "Image")]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        [Required(ErrorMessage = "Please enter {0}!")]
        public string ImageName { get; set; } = string.Empty;

        [Display(Name = "Post Content")]
        [Required(ErrorMessage = "Please enter {0}!")]
        public string Body { get; set; } = string.Empty;

        [Display(Name = "Tags")]
        [Required(ErrorMessage = "Please enter {0}!")]
        public string Tags { get; set; } = string.Empty;

        [Display(Name = "URL Slug")]
        [MaxLength(20, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string? ShortKey { get; set; }

        [Display(Name = "Views")]
        public int Visit { get; set; }

        [Display(Name = "Is Published")]
        public bool IsPublish { get; set; }

        [Display(Name = "Creation Date")]
        public DateTime CreateDate { get; set; }

        [Display(Name = "Author ID")]
        public int AuthorId { get; set; }

        [ForeignKey("AuthorId")]
        public virtual User.User User { get; set; } = null!;

        public virtual List<PostComment> PostComments { get; set; } = new List<PostComment>();

    }
}

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Domain.Entities.User
{
    public class User
    {

        [Key]
        public int UserId { get; set; }

        // Username is always the Email
        [Display(Name = "ایمیل (نام کاربری)")]
        [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
        [MaxLength(200, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد .")]
        [EmailAddress(ErrorMessage = "ایمیل وارد شده معتبر نمی باشد")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "نام")]
        [MaxLength(200, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد .")]
        public string? FirstName { get; set; }

        [Display(Name = "نام خانوادگی")]
        [MaxLength(200, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد .")]
        public string? LastName { get; set; }

        [Display(Name = "ایمیل")]
        [MaxLength(200, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد .")]
        [EmailAddress(ErrorMessage = "ایمیل وارد شده معتبر نمی باشد")]
        public string? Email { get; set; }

        [Display(Name = "کلمه عبور")]
        [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
        [MaxLength(200, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد .")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "ورود با گوگل")]
        public bool LoginWithGoogle { get; set; }

        [Display(Name = "تاریخ آخرین ورود")]
        public DateTime? LastLoginDate { get; set; }
        [Display(Name = "دفعات تلاش")]
        public int TryCount { get; set; }

        [Display(Name = "تاریخ آخرین تلاش")]
        public DateTime? LastTry { get; set; }

        [Display(Name = "پایان قفل موقت ورود")]
        public DateTime? LockoutEnd { get; set; }

        [Display(Name = "اعمال محدودیت")]
        public bool IsBan { get; set; }

        [Display(Name = "قفل شده به دلیل رفتار مشکوک")]
        public bool IsLockedForSuspiciousActivity { get; set; }

        [Display(Name = "تاریخ قفل شدن")]
        public DateTime? LockedAt { get; set; }

        [Display(Name = "دلیل قفل شدن")]
        [MaxLength(1000)]
        public string? LockReason { get; set; }

        [Display(Name = "تعداد رفتارهای مشکوک")]
        public int SuspiciousActivityCount { get; set; }


        [Display(Name = "کد فعال سازی")]
        [MaxLength(50, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد .")]
        public string ActiveCode { get; set; } = string.Empty;

        [Display(Name = "کد امنیتی")]
        [MaxLength(50, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد .")]
        public string SecurityCode { get; set; } = string.Empty;

        [Display(Name = "تاریخ انقضای کد امنیتی")]
        public DateTime? SecurityCodeExpiry { get; set; }

        public bool IsPhoneNumberActive { get; set; }
        public bool IsEmailActive { get; set; }

        [Display(Name = "شماره تلفن همراه")]
        [AllowNull]
        [MaxLength(13, ErrorMessage = "{0} نمیتواند بیشتر از {1} کارکتر باشد")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "اخیرین تغییرات")]
        public DateTime LastChange { get; set; }

        [Display(Name = "کد فعال سازی شماره تلفن")]
        [MaxLength(10, ErrorMessage = "{0} نمیتواند بیشتر از {1} کارکتر باشد")]
        public string ActivePhoneNumberCode { get; set; } = string.Empty;

        [Display(Name = "تاریخ ثبت نام")]
        public DateTime RegisterDate { get; set; }

        [Display(Name = "حذف شده")]
        public bool IsDelete { get; set; }

        [Display(Name = "زبان مورد علاقه")]
        [MaxLength(10)]
        public string? PreferredLanguage { get; set; }

        #region Relations

        public virtual IEnumerable<Post.Post> Posts { get; set; } = new List<Post.Post>();
        public virtual IEnumerable<Entities.UserTraker.Visitor> Visitors { get; set; } = new List<Entities.UserTraker.Visitor>();
        public virtual ICollection<Permission.UserRole> UserRoles { get; set; } = new List<Permission.UserRole>();

        #endregion

    }
}

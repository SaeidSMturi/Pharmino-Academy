namespace Domain.Entities.Email
{
    /// <summary>
    /// What the admin panel knows about a mailbox on the mail server (HestiaCP).
    /// The mailbox itself (password, quota, aliases...) lives on the server; this row only adds
    /// what Hestia can't store: who the address belongs to (Admin / Student) and who created it.
    /// </summary>
    public class Mailbox
    {
        public int Id { get; set; }

        /// <summary>Full address, lower case, e.g. sara.h@pharminoacademy.com</summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>"Admin" or "Student" (see <see cref="MailboxLabels"/>).</summary>
        public string Label { get; set; } = MailboxLabels.Student;

        public string? DisplayName { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? CreatedByUserId { get; set; }
    }

    public static class MailboxLabels
    {
        public const string Admin = "Admin";
        public const string Student = "Student";

        public static bool IsValid(string? label) => label == Admin || label == Student;
    }
}

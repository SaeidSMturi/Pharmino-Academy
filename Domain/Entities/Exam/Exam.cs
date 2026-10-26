using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.Exam
{
    public class Exam
    {
        [Key]
        public int ExamId { get; set; }

        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int? CourseId { get; set; }

        [ForeignKey(nameof(CourseId))]
        public virtual Domain.Entities.Course.Course? Course { get; set; }

        /// <summary>
        /// Duration of the exam in minutes. 0 or null means unlimited.
        /// </summary>
        public int DurationMinutes { get; set; } = 30;

        /// <summary>
        /// Minimum score percentage to pass (e.g. 70%)
        /// </summary>
        public decimal PassingScorePercent { get; set; } = 70.0m;

        public bool IsActive { get; set; } = true;

        public bool ShuffleQuestions { get; set; } = false;

        public bool ShuffleOptions { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastUpdated { get; set; }

        public virtual ICollection<ExamQuestion> Questions { get; set; } = new List<ExamQuestion>();
        public virtual ICollection<ExamAttempt> Attempts { get; set; } = new List<ExamAttempt>();
    }

    public class ExamQuestion
    {
        [Key]
        public int QuestionId { get; set; }

        public int ExamId { get; set; }

        [ForeignKey(nameof(ExamId))]
        public virtual Exam Exam { get; set; } = null!;

        public int OrderIndex { get; set; }

        [Required]
        public string QuestionText { get; set; } = string.Empty;

        public string? Explanation { get; set; }

        public decimal Points { get; set; } = 1.0m;

        public virtual ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
    }

    public class QuestionOption
    {
        [Key]
        public int OptionId { get; set; }

        public int QuestionId { get; set; }

        [ForeignKey(nameof(QuestionId))]
        public virtual ExamQuestion Question { get; set; } = null!;

        [Required]
        [MaxLength(10)]
        public string OptionLabel { get; set; } = string.Empty; // e.g. "A", "B", "C", "D"

        [Required]
        public string OptionText { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }
    }

    public class ExamAttempt
    {
        [Key]
        public int AttemptId { get; set; }

        public int ExamId { get; set; }

        [ForeignKey(nameof(ExamId))]
        public virtual Exam Exam { get; set; } = null!;

        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual Domain.Entities.User.User User { get; set; } = null!;

        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        public DateTime? EndTime { get; set; }

        public decimal Score { get; set; }

        public decimal TotalPoints { get; set; }

        public decimal ScorePercent { get; set; }

        public bool IsCompleted { get; set; }

        public bool IsPassed { get; set; }

        public virtual ICollection<AttemptAnswer> Answers { get; set; } = new List<AttemptAnswer>();
    }

    public class AttemptAnswer
    {
        [Key]
        public int AttemptAnswerId { get; set; }

        public int AttemptId { get; set; }

        [ForeignKey(nameof(AttemptId))]
        public virtual ExamAttempt Attempt { get; set; } = null!;

        public int QuestionId { get; set; }

        [ForeignKey(nameof(QuestionId))]
        public virtual ExamQuestion Question { get; set; } = null!;

        public int? SelectedOptionId { get; set; }

        [ForeignKey(nameof(SelectedOptionId))]
        public virtual QuestionOption? SelectedOption { get; set; }

        public bool IsCorrect { get; set; }
    }
}

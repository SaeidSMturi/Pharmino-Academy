using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Application.Services
{
    public class ParsedMcqDto
    {
        public int QuestionNumber { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public List<ParsedOptionDto> Options { get; set; } = new();
        public string? DetectedCorrectOption { get; set; } // e.g. "A", "B", "C", "D"
        public string? Explanation { get; set; }

        /// <summary>Topic/domain label when the document provides one (e.g. "Pharmacotherapy").</summary>
        public string? Domain { get; set; }

        /// <summary>Things the admin should double-check before saving (shown in the review screen).</summary>
        public List<string> Warnings { get; set; } = new();
    }

    public class ParsedOptionDto
    {
        public string Label { get; set; } = string.Empty; // "A", "B", "C", "D"
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }

    public class ExamParseResult
    {
        public List<ParsedMcqDto> Questions { get; set; } = new();

        /// <summary>Document-level problems, e.g. missing question numbers.</summary>
        public List<string> Warnings { get; set; } = new();
    }

    public interface IPdfExamParserService
    {
        ExamParseResult ParseExamFromPdf(Stream pdfStream);
        ExamParseResult ParseExamFromText(string rawText);
    }

    /// <summary>
    /// Turns an MCQ exam (PDF or pasted text) into questions, options, correct answers and explanations.
    ///
    /// PDFs are read with their layout (position, font size, bold), not as flat text. That matters because
    /// many exam PDFs (including the Pharmino mock exams) draw the question number and the option letters
    /// as separate small bold words on their own line, slightly above the text they belong to.
    ///
    /// The document is split into sections by headings:
    ///   questions → "Answer sheet" (ignored) → "Answer key" → "Answers &amp; explanations" → anything else (ignored)
    /// and the correct answer for each question is cross-checked between the answer key, the explanation
    /// section's letter, and the explanation's answer text. Disagreements become warnings, not guesses.
    ///
    /// Ambiguity rules:
    ///  - Question numbers must run in sequence (1, 2, 3 …), so "250 mg" in a wrapped line is never a new question.
    ///  - Options must form a run A, B, C … in the same style and column, and the run that ends last wins,
    ///    so a stem line such as "A 45-year-old man…" is not mistaken for option A.
    /// </summary>
    public class PdfExamParserService : IPdfExamParserService
    {
        #region Layout model

        /// <summary>One run of words on a visual line. A line with a wide gap becomes several segments.</summary>
        private sealed class Segment
        {
            public string Text = string.Empty;
            public double X;          // left edge
            public double Y;          // baseline (PDF space: larger = higher on the page)
            public double FontSize;
            public bool Bold;
            public int Page;
            public double PageWidth;
            public double PageHeight;
            public bool HasLayout;    // false for pasted text

            /// <summary>True when this segment sits in the right-hand margin (e.g. a "Domain" column).</summary>
            public bool InRightColumn => HasLayout && X > PageWidth * 0.62;
        }

        private enum Section { Preamble, Questions, AnswerSheet, AnswerKey, Explanations, Other }

        #endregion

        #region Patterns

        private static readonly Regex QuestionsHeading = new(@"^Questions?\s*\d+\s*[–\-—to]+\s*\d+\b", RegexOptions.IgnoreCase);
        private static readonly Regex EndOfExamHeading = new(@"^[—\-–\s]*End\s+of\s+(?:the\s+)?(?:exam|test|questions)\b", RegexOptions.IgnoreCase);
        private static readonly Regex AnswerSheetHeading = new(@"^Answer\s*sheet\b", RegexOptions.IgnoreCase);
        private static readonly Regex AnswerKeyHeading = new(@"^(?:Answer\s*key|Answers?\s*key|Key\s*to\s*answers|Correct\s*answers)\b", RegexOptions.IgnoreCase);
        private static readonly Regex ExplanationsHeading = new(@"^(?:Answers?\s*(?:&|and)\s*explanations?|Explanations?|Rationales?|Answers?\s*(?:&|and)\s*rationales?)\b", RegexOptions.IgnoreCase);
        private static readonly Regex OtherHeading = new(@"^(?:Score\s*by\s*domain|Scoring|Score\s*sheet|References)\b", RegexOptions.IgnoreCase);

        // "12"  "12."  "Q12"  "Question 12:"
        private static readonly Regex QuestionNumberAlone = new(@"^(?:Q(?:uestion)?\.?\s*)?(\d{1,3})\s*[.):\-]?$", RegexOptions.IgnoreCase);
        // "12 Which …"  "12. Which …"  "Q12) Which …"
        private static readonly Regex QuestionNumberInline = new(@"^(?:Q(?:uestion)?\.?\s*)?(\d{1,3})(?:\s*[.):\-]\s*|\s+)(\S.*)$", RegexOptions.IgnoreCase);

        // "A"  "(A)"  "A."  "a)"
        private static readonly Regex OptionAlone = new(@"^[\(\[]?([A-Ha-h])[\)\]\.:]?$");
        // "(A) text"  "[a] text"  "(1) text"
        private static readonly Regex OptionBracketed = new(@"^[\(\[]\s*([A-Ha-h1-8])\s*[\)\]]\s*(\S.*)$");
        // "A. text"  "a) text"  "A: text"  "1) text"
        private static readonly Regex OptionPunctuated = new(@"^(?:([A-Ha-h])[.):]|([1-8])\))\s*(\S.*)$");
        // "A text" (uppercase only; ambiguous with sentences starting with "A", resolved by the run logic)
        private static readonly Regex OptionSpaced = new(@"^([A-H])\s+(\S.*)$");

        private static readonly Regex InlineAnswer = new(@"^(?:Correct\s*(?:answer|option)?|Answer|Ans|Key)\s*[.:\-=]?\s*\(?([A-Ha-h])\)?(?:[.)\s]|$)", RegexOptions.IgnoreCase);
        private static readonly Regex InlineExplanation = new(@"^(?:Explanation|Rationale|Reason)\s*[.:\-]\s*(.*)$", RegexOptions.IgnoreCase);

        private static readonly Regex AnswerKeyPair = new(@"(?<![\d.])(\d{1,3})\s*[.):\-–]?\s*\(?([A-Ha-h])\)?(?![A-Za-z])");
        private static readonly Regex ExplanationTableHeader = new(@"^No\.?\s+Ans\b", RegexOptions.IgnoreCase);

        #endregion

        #region Public API

        public ExamParseResult ParseExamFromPdf(Stream pdfStream)
        {
            var segments = new List<Segment>();
            using (var document = PdfDocument.Open(pdfStream))
            {
                foreach (var page in document.GetPages())
                    segments.AddRange(ReadPageSegments(page));
            }

            segments = RemoveRepeatedHeadersAndFooters(segments);
            return Parse(segments);
        }

        public ExamParseResult ParseExamFromText(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return new ExamParseResult();

            var segments = rawText.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split('\n')
                .Select(l => Regex.Replace(l, @"\s+", " ").Trim())
                .Where(l => l.Length > 0)
                .Select(l => new Segment { Text = l, HasLayout = false, PageWidth = 1 })
                .ToList();

            return Parse(segments);
        }

        #endregion

        #region PDF layout extraction

        private static IEnumerable<Segment> ReadPageSegments(Page page)
        {
            List<Word> words;
            try
            {
                words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text) && w.Letters.Count > 0).ToList();
            }
            catch
            {
                return Array.Empty<Segment>();
            }

            // 1) Group words into visual lines by baseline.
            var lines = new List<List<Word>>();
            foreach (var word in words.OrderByDescending(Baseline).ThenBy(w => w.BoundingBox.Left))
            {
                var size = FontSize(word);
                var line = lines.FirstOrDefault(l => Math.Abs(Baseline(l[0]) - Baseline(word)) <= Math.Max(1.0, 0.3 * Math.Min(size, FontSize(l[0]))));
                if (line == null) lines.Add(new List<Word> { word });
                else line.Add(word);
            }

            // 2) Split each line into segments at wide horizontal gaps (separate columns / table cells).
            var segments = new List<Segment>();
            foreach (var line in lines)
            {
                var ordered = line.OrderBy(w => w.BoundingBox.Left).ToList();
                var current = new List<Word> { ordered[0] };
                for (int i = 1; i < ordered.Count; i++)
                {
                    var gap = ordered[i].BoundingBox.Left - ordered[i - 1].BoundingBox.Right;
                    if (gap > Math.Max(18, 2.2 * FontSize(ordered[i])))
                    {
                        segments.Add(ToSegment(current, page));
                        current = new List<Word>();
                    }
                    current.Add(ordered[i]);
                }
                segments.Add(ToSegment(current, page));
            }

            // 3) Reading order: top to bottom; for a two-column page, the whole left column first.
            if (LooksTwoColumn(segments, page.Width))
            {
                var mid = page.Width / 2;
                return segments.Where(s => s.X < mid).OrderByDescending(s => s.Y).ThenBy(s => s.X)
                    .Concat(segments.Where(s => s.X >= mid).OrderByDescending(s => s.Y).ThenBy(s => s.X))
                    .ToList();
            }

            return segments.OrderByDescending(s => Math.Round(s.Y, 1)).ThenBy(s => s.X).ToList();
        }

        private static double Baseline(Word w) => w.Letters[0].StartBaseLine.Y;

        private static double FontSize(Word w) => w.Letters[0].PointSize > 0 ? w.Letters[0].PointSize : w.BoundingBox.Height;

        private static Segment ToSegment(List<Word> words, Page page)
        {
            var first = words[0];
            var font = first.Letters[0].FontName ?? string.Empty;
            return new Segment
            {
                Text = string.Join(" ", words.Select(w => w.Text)),
                X = first.BoundingBox.Left,
                Y = Baseline(first),
                FontSize = FontSize(first),
                Bold = Regex.IsMatch(font, "Bold|Black|Heavy|Semi?bold|Demi", RegexOptions.IgnoreCase),
                Page = page.Number,
                PageWidth = page.Width,
                PageHeight = page.Height,
                HasLayout = true
            };
        }

        /// <summary>
        /// Two columns if many segments start right of the middle and almost none cross the middle.
        /// </summary>
        private static bool LooksTwoColumn(List<Segment> segments, double pageWidth)
        {
            // Only sentence-length text counts: short table cells (answer keys, answer sheets) are not columns.
            var body = segments.Where(s => s.Text.Length > 20).ToList();
            if (body.Count < 10) return false;

            var mid = pageWidth / 2;
            int right = body.Count(s => s.X >= mid);
            int crossing = body.Count(s => s.X < mid - 20 && s.X + EstimateWidth(s) > mid + 20);
            return right >= body.Count * 0.3 && crossing <= body.Count * 0.1;
        }

        private static double EstimateWidth(Segment s) => s.Text.Length * s.FontSize * 0.5;

        /// <summary>
        /// Drops text that repeats near the top or bottom of most pages (copyright lines, page numbers, running titles).
        /// </summary>
        private static List<Segment> RemoveRepeatedHeadersAndFooters(List<Segment> segments)
        {
            var pageCount = segments.Select(s => s.Page).Distinct().Count();
            if (pageCount < 3) return segments;

            static string Key(Segment s) => Regex.Replace(s.Text, @"\d+", "#");

            var repeated = segments
                .GroupBy(Key)
                .Where(g => g.Select(s => s.Page).Distinct().Count() >= Math.Max(3, pageCount / 2))
                .Select(g => g.Key)
                .ToHashSet();

            // Only treat them as running headers/footers when they sit in the page margins.
            return segments.Where(s => !repeated.Contains(Key(s)) || (s.Y > 60 && s.Y < s.PageHeight - 60)).ToList();
        }

        #endregion

        #region Parsing

        private ExamParseResult Parse(List<Segment> segments)
        {
            var result = new ExamParseResult();

            var bySection = SplitIntoSections(segments);
            var questions = ParseQuestions(bySection[Section.Questions].Count > 0
                ? bySection[Section.Questions]
                : bySection[Section.Preamble]);

            var answerKey = ParseAnswerKey(bySection[Section.AnswerKey]);
            var explanations = ParseExplanations(bySection[Section.Explanations]);

            foreach (var q in questions)
                ResolveAnswer(q, answerKey, explanations);

            result.Questions = questions;

            if (questions.Count == 0)
            {
                result.Warnings.Add("No questions were recognized. Check that questions are numbered (1, 2, 3 …) and options are labelled (A, B, C …).");
                return result;
            }

            var numbers = questions.Select(q => q.QuestionNumber).ToList();
            var missing = Enumerable.Range(numbers.Min(), numbers.Max() - numbers.Min() + 1).Except(numbers).ToList();
            if (missing.Count > 0)
                result.Warnings.Add($"Question number(s) {string.Join(", ", missing)} were not found in the document.");

            int needsReview = questions.Count(q => q.Warnings.Count > 0);
            if (needsReview > 0)
                result.Warnings.Add($"{needsReview} question(s) need a quick review — they are highlighted below.");

            return result;
        }

        private static Dictionary<Section, List<Segment>> SplitIntoSections(List<Segment> segments)
        {
            var map = Enum.GetValues<Section>().ToDictionary(s => s, _ => new List<Segment>());
            var current = Section.Preamble;

            foreach (var seg in segments)
            {
                var text = seg.Text.Trim();
                var isHeadingShaped = text.Length <= 60;

                if (isHeadingShaped && QuestionsHeading.IsMatch(text) && current == Section.Preamble)
                {
                    current = Section.Questions;
                    continue;
                }
                if (isHeadingShaped && EndOfExamHeading.IsMatch(text)) { current = Section.Other; continue; }
                if (isHeadingShaped && AnswerSheetHeading.IsMatch(text)) { current = Section.AnswerSheet; continue; }
                if (isHeadingShaped && ExplanationsHeading.IsMatch(text) && current != Section.Preamble) { current = Section.Explanations; continue; }
                if (isHeadingShaped && AnswerKeyHeading.IsMatch(text) && !InlineAnswer.IsMatch(text)) { current = Section.AnswerKey; continue; }
                if (isHeadingShaped && OtherHeading.IsMatch(text)) { current = Section.Other; continue; }

                map[current].Add(seg);
            }

            // No explicit "Questions 1–N" banner: the questions are whatever came before the first answer section.
            if (map[Section.Questions].Count == 0)
            {
                map[Section.Questions] = map[Section.Preamble];
                map[Section.Preamble] = new List<Segment>();
            }

            return map;
        }

        #region Questions

        private sealed class QuestionBlock
        {
            public int Number;
            public Segment Marker = null!;
            public List<Segment> Body = new();
        }

        private List<ParsedMcqDto> ParseQuestions(List<Segment> segments)
        {
            var blocks = SplitIntoQuestionBlocks(segments);
            return blocks.Select(BuildQuestion).Where(q => q != null).Select(q => q!).ToList();
        }

        /// <summary>
        /// Finds where each question starts. A number only starts a new question when it is the next number
        /// in sequence and it looks like a question marker (same column as earlier markers, or on its own line).
        /// </summary>
        private static List<QuestionBlock> SplitIntoQuestionBlocks(List<Segment> segments)
        {
            var blocks = new List<QuestionBlock>();
            QuestionBlock? current = null;
            var markerXs = new List<double>();

            foreach (var seg in segments)
            {
                var (number, rest, alone) = MatchQuestionNumber(seg.Text);
                if (number > 0 && IsQuestionStart(seg, number, alone, current, markerXs))
                {
                    // A second "1" while the first block never got options means the first was a false start
                    // (e.g. a numbered list on the cover page). Start over.
                    if (number == 1 && current != null && CountOptionCandidates(current.Body) < 2)
                        blocks.Clear();

                    current = new QuestionBlock { Number = number, Marker = seg };
                    if (!string.IsNullOrEmpty(rest))
                        current.Body.Add(Clone(seg, rest, offsetX: 0));
                    blocks.Add(current);
                    if (seg.HasLayout) markerXs.Add(seg.X);
                    continue;
                }

                current?.Body.Add(seg);
            }

            return blocks;
        }

        private static (int Number, string? Remainder, bool Alone) MatchQuestionNumber(string text)
        {
            var alone = QuestionNumberAlone.Match(text);
            if (alone.Success) return (int.Parse(alone.Groups[1].Value), null, true);

            var inline = QuestionNumberInline.Match(text);
            if (inline.Success) return (int.Parse(inline.Groups[1].Value), inline.Groups[2].Value.Trim(), false);

            return (0, null, false);
        }

        private static bool IsQuestionStart(Segment seg, int number, bool alone, QuestionBlock? current, List<double> markerXs)
        {
            if (current == null)
                return number == 1 || alone || seg.Bold;

            if (number == 1 && CountOptionCandidates(current.Body) < 2)
                return true;

            if (number != current.Number + 1)
                return false;

            // In a laid-out PDF, question numbers line up in one column.
            if (seg.HasLayout && markerXs.Count > 0)
            {
                var typicalX = markerXs.OrderBy(x => x).ElementAt(markerXs.Count / 2);
                if (Math.Abs(seg.X - typicalX) > 15) return false;
            }

            // "12 hours later…" wrapping inside a stem: an inline number before any options is not a new question.
            if (!alone && !seg.Bold && CountOptionCandidates(current.Body) < 2)
                return false;

            return true;
        }

        private enum OptionStyle { Alone, Bracketed, Punctuated, Spaced }

        private sealed class OptionCandidate
        {
            public int Index;            // position in the block body
            public string Label = "";    // normalized "A".."H"
            public string Text = "";     // text on the same segment (may be empty)
            public OptionStyle Style;
            public double X;
        }

        private static OptionCandidate? MatchOption(Segment seg, int index)
        {
            var text = seg.Text.Trim();
            Match m;

            if ((m = OptionAlone.Match(text)).Success)
                return new OptionCandidate { Index = index, Label = NormalizeOptionLabel(m.Groups[1].Value), Style = OptionStyle.Alone, X = seg.X };

            if ((m = OptionBracketed.Match(text)).Success)
                return new OptionCandidate { Index = index, Label = NormalizeOptionLabel(m.Groups[1].Value), Text = m.Groups[2].Value, Style = OptionStyle.Bracketed, X = seg.X };

            if ((m = OptionPunctuated.Match(text)).Success)
            {
                var raw = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                return new OptionCandidate { Index = index, Label = NormalizeOptionLabel(raw), Text = m.Groups[3].Value, Style = OptionStyle.Punctuated, X = seg.X };
            }

            if ((m = OptionSpaced.Match(text)).Success)
                return new OptionCandidate { Index = index, Label = m.Groups[1].Value, Text = m.Groups[2].Value, Style = OptionStyle.Spaced, X = seg.X };

            return null;
        }

        private static int CountOptionCandidates(List<Segment> body) =>
            body.Select((s, i) => MatchOption(s, i)).Count(c => c != null);

        /// <summary>
        /// Picks the option labels: the longest run A, B, C … (same style, same column) and, on a tie, the one
        /// that starts last — so stem sentences beginning with "A …" stay in the stem.
        /// </summary>
        private static List<OptionCandidate> FindOptionRun(List<Segment> body)
        {
            var candidates = body.Select((s, i) => MatchOption(s, i)).Where(c => c != null).Select(c => c!).ToList();
            List<OptionCandidate> best = new();

            foreach (var start in candidates.Where(c => c.Label == "A"))
            {
                var run = new List<OptionCandidate> { start };
                foreach (var c in candidates.Where(c => c.Index > start.Index))
                {
                    var expected = ((char)(run[^1].Label[0] + 1)).ToString();
                    if (c.Label == expected && c.Style == start.Style && (!body[c.Index].HasLayout || Math.Abs(c.X - start.X) <= 8))
                        run.Add(c);
                }

                if (run.Count > best.Count || (run.Count == best.Count && run.Count > 0 && start.Index > best[0].Index))
                    best = run;
            }

            return best.Count >= 2 ? best : new List<OptionCandidate>();
        }

        private static ParsedMcqDto? BuildQuestion(QuestionBlock block)
        {
            var q = new ParsedMcqDto { QuestionNumber = block.Number };

            // Pull out inline "Answer: B" / "Explanation: …" lines first.
            var body = new List<Segment>();
            var explanation = new StringBuilder();
            bool inExplanation = false;
            foreach (var seg in block.Body)
            {
                var ans = InlineAnswer.Match(seg.Text);
                if (ans.Success && seg.Text.Length <= 40)
                {
                    q.DetectedCorrectOption = NormalizeOptionLabel(ans.Groups[1].Value);
                    var tail = seg.Text[ans.Length..].Trim();
                    var tailExp = InlineExplanation.Match(tail);
                    if (tailExp.Success) { inExplanation = true; AppendLine(explanation, tailExp.Groups[1].Value); }
                    continue;
                }

                var exp = InlineExplanation.Match(seg.Text);
                if (exp.Success) { inExplanation = true; AppendLine(explanation, exp.Groups[1].Value); continue; }

                if (inExplanation) { AppendLine(explanation, seg.Text); continue; }

                body.Add(seg);
            }

            var run = FindOptionRun(body);
            if (run.Count == 0)
            {
                // Keep it so the admin can fix it by hand, but flag it.
                q.QuestionText = JoinLines(body.Select(s => s.Text));
                q.Warnings.Add("No answer options (A, B, C …) were recognized for this question.");
                return string.IsNullOrWhiteSpace(q.QuestionText) ? null : q;
            }

            q.QuestionText = JoinLines(body.Take(run[0].Index).Select(s => s.Text));

            for (int i = 0; i < run.Count; i++)
            {
                var from = run[i].Index + 1;
                var to = i + 1 < run.Count ? run[i + 1].Index : body.Count;
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(run[i].Text)) parts.Add(run[i].Text);
                parts.AddRange(body.Skip(from).Take(to - from).Select(s => s.Text));

                q.Options.Add(new ParsedOptionDto { Label = run[i].Label, Text = JoinLines(parts) });
            }

            if (explanation.Length > 0)
                q.Explanation = explanation.ToString().Trim();

            if (string.IsNullOrWhiteSpace(q.QuestionText))
                q.Warnings.Add("The question text is empty.");
            if (q.Options.Any(o => string.IsNullOrWhiteSpace(o.Text)))
                q.Warnings.Add("One or more options have no text.");

            // Some PDFs draw symbols such as "(", "–", "×" or "→" with fonts that have no text mapping,
            // so they vanish on extraction. An unmatched bracket is the visible sign of that.
            var allText = q.QuestionText + " " + string.Join(" ", q.Options.Select(o => o.Text));
            if (allText.Count(c => c == '(') != allText.Count(c => c == ')'))
                q.Warnings.Add("Some symbols (e.g. brackets, dashes, ×) may be missing from the PDF's text — compare with the PDF.");

            return q;
        }

        #endregion

        #region Answer key & explanations

        private static Dictionary<int, string> ParseAnswerKey(List<Segment> segments)
        {
            var map = new Dictionary<int, string>();
            var text = string.Join(" ", segments.Select(s => s.Text));
            foreach (Match m in AnswerKeyPair.Matches(text))
            {
                if (int.TryParse(m.Groups[1].Value, out var n) && !map.ContainsKey(n))
                    map[n] = NormalizeOptionLabel(m.Groups[2].Value);
            }
            return map;
        }

        private sealed class ExplanationItem
        {
            public int Number;
            public string? Letter;
            public string? Domain;
            public string AnswerText = "";
            public StringBuilder Explanation = new();
            public Segment Marker = null!;
        }

        /// <summary>
        /// Reads "Answers &amp; explanations". Each item starts with its question number (in sequence, in the
        /// left column) followed by the correct answer's text; following lines are the explanation. A lone
        /// bold letter and a right-margin domain label on (or just above) the number's row belong to that item.
        /// </summary>
        private static Dictionary<int, ExplanationItem> ParseExplanations(List<Segment> segments)
        {
            var items = new Dictionary<int, ExplanationItem>();
            ExplanationItem? current = null;
            string? pendingLetter = null, pendingDomain = null;
            double? numberX = null;

            // Same visual row as the current item's number (letters/badges are often a point or two off the baseline).
            bool OnCurrentRow(Segment s) =>
                current != null && s.HasLayout && current.Marker.HasLayout && s.Page == current.Marker.Page &&
                Math.Abs(s.Y - current.Marker.Y) <= Math.Max(4, 0.6 * s.FontSize);

            foreach (var seg in segments)
            {
                var text = seg.Text.Trim();
                if (ExplanationTableHeader.IsMatch(text)) continue;

                var lone = OptionAlone.Match(text);
                if (lone.Success && text.Length <= 3)
                {
                    var letter = NormalizeOptionLabel(lone.Groups[1].Value);
                    if (OnCurrentRow(seg) && current!.Letter == null) current.Letter = letter;
                    else pendingLetter = letter;
                    continue;
                }

                if (seg.InRightColumn && text.Length <= 40 && !Regex.IsMatch(text, @"^\d"))
                {
                    if (OnCurrentRow(seg) && current!.Domain == null) current.Domain = text;
                    else pendingDomain = text;
                    continue;
                }

                // Number in its own cell with the answer text in the next cell of the same row.
                if (current != null && current.AnswerText.Length == 0 && current.Explanation.Length == 0 && OnCurrentRow(seg))
                {
                    current.AnswerText = text;
                    continue;
                }

                var (number, rest, _) = MatchQuestionNumber(text);
                var expected = current == null ? (int?)null : current.Number + 1;
                bool inNumberColumn = !seg.HasLayout || numberX == null || Math.Abs(seg.X - numberX.Value) <= 15;
                if (number > 0 && (expected == null || number == expected) && inNumberColumn && !items.ContainsKey(number))
                {
                    current = new ExplanationItem
                    {
                        Number = number,
                        Letter = pendingLetter,
                        Domain = pendingDomain,
                        AnswerText = rest ?? string.Empty,
                        Marker = seg
                    };

                    // Pasted text often reads "12 B Some answer…" or "12. B) …" with the letter inline.
                    if (current.Letter == null)
                    {
                        var lead = Regex.Match(current.AnswerText, @"^\(?([A-H])[\).:\-—]\s*(.*)$");
                        if (lead.Success) { current.Letter = lead.Groups[1].Value; current.AnswerText = lead.Groups[2].Value; }
                    }

                    items[number] = current;
                    if (seg.HasLayout && numberX == null) numberX = seg.X;
                    pendingLetter = pendingDomain = null;
                    continue;
                }

                if (current == null) continue;

                // The answer text can wrap onto the next line; ResolveAnswer moves such lines back to the answer.
                AppendLine(current.Explanation, text);
            }

            return items;
        }

        /// <summary>
        /// Chooses the correct option from (in order of trust): an inline "Answer:" line, the answer key,
        /// the explanation's letter, then matching the explanation's answer text to an option's text.
        /// </summary>
        private static void ResolveAnswer(ParsedMcqDto q, Dictionary<int, string> answerKey, Dictionary<int, ExplanationItem> explanations)
        {
            explanations.TryGetValue(q.QuestionNumber, out var exp);
            answerKey.TryGetValue(q.QuestionNumber, out var keyLetter);

            string? textMatch = null;
            if (exp != null && !string.IsNullOrWhiteSpace(exp.AnswerText))
                textMatch = MatchOptionByText(q, exp.AnswerText);

            var sources = new List<(string Name, string? Letter)>
            {
                ("the question's \"Answer:\" line", q.DetectedCorrectOption),
                ("the answer key", keyLetter),
                ("the explanation section", exp?.Letter),
                ("the explanation's answer text", textMatch)
            };

            var chosen = sources.FirstOrDefault(s => !string.IsNullOrEmpty(s.Letter) && q.Options.Any(o => o.Label == s.Letter));
            q.DetectedCorrectOption = chosen.Letter;

            var disagreeing = sources.Where(s => !string.IsNullOrEmpty(s.Letter) && s.Letter != chosen.Letter).ToList();
            foreach (var d in disagreeing)
                q.Warnings.Add($"Used {chosen.Letter} from {chosen.Name}, but {d.Name} says {d.Letter}.");

            foreach (var o in q.Options)
                o.IsCorrect = o.Label == q.DetectedCorrectOption;

            if (q.DetectedCorrectOption == null && q.Options.Count > 0)
                q.Warnings.Add("No correct answer was found — select it before saving.");

            if (exp != null)
            {
                q.Domain ??= exp.Domain;

                // The answer text may wrap onto the first explanation line(s); those belong to the answer, not the explanation.
                var lines = exp.Explanation.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                var correct = q.Options.FirstOrDefault(o => o.IsCorrect);
                if (correct != null)
                {
                    var optionText = Normalize(correct.Text);
                    var consumed = Normalize(exp.AnswerText);
                    while (lines.Count > 0 && consumed.Length > 0 && optionText.StartsWith(consumed + Normalize(lines[0])) && Normalize(lines[0]).Length > 0)
                    {
                        consumed += Normalize(lines[0]);
                        lines.RemoveAt(0);
                    }
                }

                if (string.IsNullOrEmpty(q.Explanation) && lines.Count > 0)
                    q.Explanation = JoinLines(lines);
            }
        }

        private static string? MatchOptionByText(ParsedMcqDto q, string answerText)
        {
            var target = Normalize(answerText);
            if (target.Length == 0) return null;

            // Exact match first, then "one starts with the other" (the explanation may be cut or extended).
            var exact = q.Options.Where(o => Normalize(o.Text) == target).ToList();
            if (exact.Count == 1) return exact[0].Label;

            var prefix = q.Options.Where(o =>
            {
                var t = Normalize(o.Text);
                return t.Length >= 3 && (target.StartsWith(t) || t.StartsWith(target));
            }).ToList();
            return prefix.Count == 1 ? prefix[0].Label : null;
        }

        private static string Normalize(string s) =>
            Regex.Replace(s.ToLowerInvariant(), @"[^\p{L}\p{N}]+", "");

        #endregion

        #region Helpers

        private static string NormalizeOptionLabel(string label) => label switch
        {
            "1" => "A",
            "2" => "B",
            "3" => "C",
            "4" => "D",
            "5" => "E",
            "6" => "F",
            "7" => "G",
            "8" => "H",
            _ => label.ToUpperInvariant()
        };

        private static Segment Clone(Segment s, string text, double offsetX) => new()
        {
            Text = text,
            X = s.X + offsetX,
            Y = s.Y,
            FontSize = s.FontSize,
            Bold = false,
            Page = s.Page,
            PageWidth = s.PageWidth,
            PageHeight = s.PageHeight,
            HasLayout = s.HasLayout
        };

        private static void AppendLine(StringBuilder sb, string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            sb.Append(line.Trim()).Append('\n');
        }

        /// <summary>Joins wrapped lines into one string, re-joining words hyphenated across a line break.</summary>
        private static string JoinLines(IEnumerable<string> lines)
        {
            var sb = new StringBuilder();
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (sb.Length == 0) { sb.Append(line); continue; }

                if (sb[^1] == '-' && sb.Length > 1 && char.IsLetter(sb[^2]) && char.IsLower(line[0]) &&
                    !Regex.IsMatch(line, @"^(?:and|or|to|nor)\b"))
                    sb.Append(line);           // "smoke-" + "free" → "smoke-free", but "long-" + "and short-term" stays apart
                else
                    sb.Append(' ').Append(line);
            }
            return Regex.Replace(sb.ToString(), @"\s{2,}", " ").Trim();
        }

        #endregion

        #endregion
    }
}

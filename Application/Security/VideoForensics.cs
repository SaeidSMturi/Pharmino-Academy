namespace Application.Security
{
    /// <summary>
    /// پروتکل واترمارک بیتی فاز ۲ (A/B Watermarking)
    /// تعبیه‌سازی شناسه کاربر در استریم ویدیو از طریق تغییر چانک‌های انتخابی (Variant A / Variant B).
    /// کاملاً قطعی (Deterministic) و مستقل از پایگاه داده با قابلیت بازیابی آفلاین هویت کاربر فاش‌کننده.
    /// </summary>
    public static class VideoForensics
    {
        /// <summary>
        /// بایت همگام‌سازی (Barker code 0xB3: 10110011) برای یافتن نقطه شروع در استریم ویدیو.
        /// </summary>
        public const byte SyncHeader = 0xB3;

        public const int SyncHeaderBitsCount = 8;
        public const int UserIdBitsCount = 24;
        public const int ChecksumBitsCount = 8;
        public const int TotalFrameBits = 40; // 8 + 24 + 8

        /// <summary>
        /// حداکثر تعداد سگمنت‌های کدگذاری‌شده در هر ویدیو (~۴۰ سگمنت = ۶.۵ دقیقه پخش، سربار ذخیره‌سازی بسیار پایین).
        /// </summary>
        public const int MaxCodeSegments = 40;

        /// <summary>
        /// بررسی می‌کند که آیا این سگمنت یک «سگمنت کد» دارای واریانت B است یا خیر.
        /// سگمنت‌های فرد ۱، ۳، ۵، ... تا سگمنت ۷۹ به عنوان سگمنت‌های کد انتخاب می‌شوند.
        /// </summary>
        public static bool IsCodeSegment(int segIndex)
        {
            return segIndex >= 1 && segIndex <= (MaxCodeSegments * 2 - 1) && (segIndex % 2 == 1);
        }

        /// <summary>
        /// شماره بیت متناظر با شماره سگمنت در فریم ۴۰ بیتی.
        /// </summary>
        public static int GetBitIndex(int segIndex)
        {
            if (segIndex < 1) return 0;
            return ((segIndex - 1) / 2) % TotalFrameBits;
        }

        /// <summary>
        /// محاسبه چک‌سام ۸ بیتی برای اعتبارسنجی یکپارچگی شناسه کاربر.
        /// </summary>
        public static byte ComputeChecksum(int userId)
        {
            byte b0 = (byte)(userId & 0xFF);
            byte b1 = (byte)((userId >> 8) & 0xFF);
            byte b2 = (byte)((userId >> 16) & 0xFF);
            return (byte)(b0 ^ b1 ^ b2 ^ 0x5A);
        }

        /// <summary>
        /// تولید فریم کامل ۴۰ بیتی برای شناسه کاربری مشخص:
        /// - ۸ بیت هدر سینک (0xB3)
        /// - ۲۴ بیت شناسه کاربر (از پرارزش به کم‌ارزش)
        /// - ۸ بیت چک‌سام
        /// </summary>
        public static bool[] GenerateUserBitstream(int userId)
        {
            var bits = new bool[TotalFrameBits];
            int bitPos = 0;

            // 1. Sync Header (8 bits)
            for (int i = 7; i >= 0; i--)
            {
                bits[bitPos++] = ((SyncHeader >> i) & 1) == 1;
            }

            // 2. UserId (24 bits)
            for (int i = 23; i >= 0; i--)
            {
                bits[bitPos++] = ((userId >> i) & 1) == 1;
            }

            // 3. Checksum (8 bits)
            byte checksum = ComputeChecksum(userId);
            for (int i = 7; i >= 0; i--)
            {
                bits[bitPos++] = ((checksum >> i) & 1) == 1;
            }

            return bits;
        }

        /// <summary>
        /// مشخص می‌کند که برای این کاربر و این سگمنت مشخص، آیا باید واریانت B (بیت ۱) ارسال شود یا واریانت A (بیت ۰).
        /// </summary>
        public static bool IsVariantBForUser(int userId, int segIndex)
        {
            if (!IsCodeSegment(segIndex)) return false;
            if (userId <= 0) return false;

            int bitIndex = GetBitIndex(segIndex);
            var bitstream = GenerateUserBitstream(userId);
            return bitstream[bitIndex];
        }

        /// <summary>
        /// رمزگشایی و اعتبارسنجی آرایه بیت‌های استخراج‌شده برای دستیابی به شناسه کاربر.
        /// </summary>
        public static (bool IsValid, int UserId) TryDecodeUserId(bool[] bits)
        {
            if (bits == null || bits.Length < TotalFrameBits)
            {
                return (false, 0);
            }

            // جستجو برای یافتن موقعیت هدر سینک
            for (int offset = 0; offset <= bits.Length - TotalFrameBits; offset++)
            {
                byte header = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (bits[offset + i]) header |= (byte)(1 << (7 - i));
                }

                if (header == SyncHeader)
                {
                    // استخراج ۲۴ بیت UserId
                    int extractedUserId = 0;
                    for (int i = 0; i < 24; i++)
                    {
                        if (bits[offset + 8 + i]) extractedUserId |= (1 << (23 - i));
                    }

                    // استخراج ۸ بیت چک‌سام
                    byte extractedChecksum = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        if (bits[offset + 32 + i]) extractedChecksum |= (byte)(1 << (7 - i));
                    }

                    if (ComputeChecksum(extractedUserId) == extractedChecksum)
                    {
                        return (true, extractedUserId);
                    }
                }
            }

            return (false, 0);
        }
    }
}

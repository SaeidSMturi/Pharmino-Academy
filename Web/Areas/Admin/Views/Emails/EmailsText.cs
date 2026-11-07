using System.Globalization;

namespace Web.Areas.Admin.Views.Emails
{
    /// <summary>
    /// Wording for the Admin → Emails pages in English, Arabic and Farsi. Used by the Razor views and handed to
    /// admin-emails.js as one JSON object, so the page and its script always say the same thing.
    /// </summary>
    public static class EmailsText
    {
        private static readonly Dictionary<string, (string En, string Ar, string Fa)> T = new()
        {
            ["Emails"] = ("Emails", "البريد الإلكتروني", "ایمیل‌ها"),
            ["CreateEmail"] = ("Create Email", "إنشاء بريد", "ساخت ایمیل"),
            ["AllEmails"] = ("All Emails", "كل الحسابات البريدية", "همه ایمیل‌ها"),
            ["CreateSub"] = ("Add a mailbox on the mail server and tag who it belongs to.", "أضف صندوق بريد على خادم البريد وحدّد لمن يتبع.", "یک صندوق پستی روی سرور ایمیل بسازید و مشخص کنید متعلق به کیست."),
            ["ListSub"] = ("Every mailbox on the mail server, with its label.", "كل صناديق البريد على الخادم مع تصنيفها.", "همه صندوق‌های پستی سرور به همراه برچسب آن‌ها."),
            ["Account"] = ("Account", "الحساب", "حساب"),
            ["AccountPh"] = ("e.g. sara.hosseini", "مثال: sara.hosseini", "مثلاً sara.hosseini"),
            ["AccountHint"] = ("Letters, numbers, dots, dashes and underscores.", "حروف وأرقام ونقاط وشرطات.", "حروف، اعداد، نقطه، خط تیره و زیرخط."),
            ["Label"] = ("Label", "التصنيف", "برچسب"),
            ["Admin"] = ("Admin", "مسؤول", "مدیر"),
            ["Student"] = ("Student", "طالب", "دانشجو"),
            ["Unlabeled"] = ("Unlabeled", "بدون تصنيف", "بدون برچسب"),
            ["DisplayName"] = ("Display name", "الاسم الظاهر", "نام نمایشی"),
            ["DisplayNamePh"] = ("e.g. Sara Hosseini", "مثال: سارة حسيني", "مثلاً سارا حسینی"),
            ["Optional"] = ("optional", "اختياري", "اختیاری"),
            ["Password"] = ("Password", "كلمة المرور", "رمز عبور"),
            ["Generate"] = ("Generate", "توليد", "ساخت"),
            ["Show"] = ("Show password", "إظهار كلمة المرور", "نمایش رمز"),
            ["Copy"] = ("Copy", "نسخ", "کپی"),
            ["Copied"] = ("Copied", "تم النسخ", "کپی شد"),
            ["Rule10"] = ("At least 10 characters", "10 أحرف على الأقل", "حداقل ۱۰ کاراکتر"),
            ["RuleMix"] = ("Letters plus numbers or symbols", "حروف مع أرقام أو رموز", "حروف به همراه عدد یا نماد"),
            ["RuleSymbol"] = ("A symbol (stronger)", "رمز خاص (أقوى)", "یک نماد (قوی‌تر)"),
            ["RuleNoName"] = ("Doesn't contain the account name", "لا يحتوي اسم الحساب", "شامل نام حساب نباشد"),
            ["Weak"] = ("Weak", "ضعيفة", "ضعیف"),
            ["Fair"] = ("Fair", "مقبولة", "متوسط"),
            ["Good"] = ("Good", "جيدة", "خوب"),
            ["Strong"] = ("Strong", "قوية", "قوی"),
            ["Quota"] = ("Quota (MB)", "الحصة (ميغابايت)", "سهمیه (مگابایت)"),
            ["Unlimited"] = ("Unlimited", "غير محدود", "نامحدود"),
            ["Advanced"] = ("Advanced options", "خيارات متقدمة", "گزینه‌های پیشرفته"),
            ["Aliases"] = ("Aliases", "الأسماء البديلة", "نام‌های مستعار"),
            ["AliasesPh"] = ("e.g. info, contact (comma separated)", "مثال: info, contact", "مثلاً info, contact (با کاما)"),
            ["ForwardTo"] = ("Forward to", "إعادة التوجيه إلى", "ارسال به"),
            ["ForwardPh"] = ("another@address.com", "another@address.com", "another@address.com"),
            ["SendDetails"] = ("Send login details to", "إرسال بيانات الدخول إلى", "ارسال اطلاعات ورود به"),
            ["SendDetailsHint"] = ("Their personal address, so they can sign in. Leave empty to skip.", "عنوانهم الشخصي ليتمكنوا من الدخول. اتركه فارغًا للتخطي.", "آدرس شخصی کاربر برای ورود. خالی بگذارید تا ارسال نشود."),
            ["CreateMailbox"] = ("Create mailbox", "إنشاء صندوق البريد", "ساخت صندوق پستی"),
            ["Creating"] = ("Creating…", "جارٍ الإنشاء…", "در حال ساخت…"),
            ["Created"] = ("Mailbox created", "تم إنشاء صندوق البريد", "صندوق پستی ساخته شد"),
            ["CreatedSub"] = ("Save the password now: it won't be shown again.", "احفظ كلمة المرور الآن: لن تظهر مرة أخرى.", "رمز را همین حالا ذخیره کنید؛ دوباره نمایش داده نمی‌شود."),
            ["CreateAnother"] = ("Create another", "إنشاء آخر", "ساخت یکی دیگر"),
            ["ViewAll"] = ("View all emails", "عرض كل الحسابات", "دیدن همه ایمیل‌ها"),
            ["Connection"] = ("Connection settings", "إعدادات الاتصال", "تنظیمات اتصال"),
            ["ConnectionSub"] = ("For mail apps (Outlook, Gmail app, iPhone Mail).", "لتطبيقات البريد (Outlook و Gmail و iPhone).", "برای برنامه‌های ایمیل (Outlook، Gmail، iPhone)."),
            ["Incoming"] = ("Incoming (IMAP)", "الوارد (IMAP)", "دریافت (IMAP)"),
            ["Outgoing"] = ("Outgoing (SMTP)", "الصادر (SMTP)", "ارسال (SMTP)"),
            ["Username"] = ("Username", "اسم المستخدم", "نام کاربری"),
            ["UsernameIs"] = ("the full email address", "عنوان البريد كاملًا", "آدرس کامل ایمیل"),
            ["Webmail"] = ("Webmail", "البريد عبر الويب", "وب‌میل"),
            ["OpenWebmail"] = ("Open webmail", "فتح البريد", "باز کردن وب‌میل"),
            ["NotConfigured"] = ("The mail server isn't connected yet. Add the HestiaCP access key to the \"Hestia\" section of appsettings.json on the server, then restart the site.", "لم يتم ربط خادم البريد بعد. أضف مفتاح HestiaCP في قسم \"Hestia\" في appsettings.json على الخادم ثم أعد تشغيل الموقع.", "سرور ایمیل هنوز وصل نشده است. کلید دسترسی HestiaCP را در بخش \"Hestia\" فایل appsettings.json روی سرور وارد کنید و سایت را دوباره راه‌اندازی کنید."),
            ["DemoMode"] = ("Demo mode: no mail server is connected on this computer, so changes are kept in memory only.", "وضع تجريبي: لا يوجد خادم بريد متصل على هذا الجهاز، لذلك تُحفظ التغييرات في الذاكرة فقط.", "حالت نمایشی: روی این رایانه سرور ایمیل وصل نیست و تغییرات فقط در حافظه می‌ماند."),
            ["Search"] = ("Search address or name…", "ابحث بالعنوان أو الاسم…", "جستجوی آدرس یا نام…"),
            ["All"] = ("All", "الكل", "همه"),
            ["Admins"] = ("Admins", "المسؤولون", "مدیران"),
            ["Students"] = ("Students", "الطلاب", "دانشجویان"),
            ["Status"] = ("Status", "الحالة", "وضعیت"),
            ["AnyStatus"] = ("Any status", "أي حالة", "همه وضعیت‌ها"),
            ["Active"] = ("Active", "نشط", "فعال"),
            ["Suspended"] = ("Suspended", "موقوف", "معلق"),
            ["From"] = ("From", "من", "از"),
            ["To"] = ("To", "إلى", "تا"),
            ["CreatedRange"] = ("Created between", "تاريخ الإنشاء بين", "تاریخ ساخت بین"),
            ["Clear"] = ("Clear filters", "مسح الفلاتر", "پاک کردن فیلترها"),
            ["Address"] = ("Email address", "عنوان البريد", "آدرس ایمیل"),
            ["CreatedAt"] = ("Created", "تاريخ الإنشاء", "تاریخ ساخت"),
            ["Disk"] = ("Disk usage", "استخدام المساحة", "فضای مصرفی"),
            ["Loading"] = ("Loading mailboxes…", "جارٍ تحميل الصناديق…", "در حال بارگذاری…"),
            ["Empty"] = ("No mailboxes match these filters.", "لا توجد صناديق تطابق الفلاتر.", "صندوقی با این فیلترها پیدا نشد."),
            ["Retry"] = ("Try again", "إعادة المحاولة", "تلاش دوباره"),
            ["ShowingOf"] = ("{0}–{1} of {2}", "{0}–{1} من {2}", "{0}–{1} از {2}"),
            ["Close"] = ("Close", "إغلاق", "بستن"),
            ["Save"] = ("Save", "حفظ", "ذخیره"),
            ["Saved"] = ("Saved", "تم الحفظ", "ذخیره شد"),
            ["Details"] = ("Details", "التفاصيل", "جزئیات"),
            ["QuotaUsage"] = ("Quota usage", "استخدام الحصة", "مصرف سهمیه"),
            ["ChangePassword"] = ("Change password", "تغيير كلمة المرور", "تغییر رمز عبور"),
            ["NewPassword"] = ("New password", "كلمة مرور جديدة", "رمز جدید"),
            ["PasswordChanged"] = ("Password changed", "تم تغيير كلمة المرور", "رمز تغییر کرد"),
            ["ChangeQuota"] = ("Quota", "الحصة", "سهمیه"),
            ["Add"] = ("Add", "إضافة", "افزودن"),
            ["Remove"] = ("Remove", "إزالة", "حذف"),
            ["NoAliases"] = ("No aliases", "لا توجد أسماء بديلة", "نام مستعاری ندارد"),
            ["NoForwards"] = ("Not forwarded", "لا يوجد توجيه", "ارسال نمی‌شود"),
            ["Forwarding"] = ("Forwarding", "إعادة التوجيه", "ارسال خودکار"),
            ["Suspend"] = ("Suspend mailbox", "إيقاف صندوق البريد", "تعلیق صندوق"),
            ["SuspendHint"] = ("A suspended mailbox can't sign in or receive mail.", "الصندوق الموقوف لا يمكنه الدخول أو استقبال البريد.", "صندوق معلق نمی‌تواند وارد شود یا ایمیل دریافت کند."),
            ["DangerZone"] = ("Danger zone", "منطقة الخطر", "بخش خطرناک"),
            ["Delete"] = ("Delete mailbox", "حذف صندوق البريد", "حذف صندوق پستی"),
            ["DeleteHint"] = ("Deletes the mailbox and every message in it. This can't be undone. Type the full address to confirm.", "يحذف الصندوق وكل رسائله نهائيًا. لا يمكن التراجع. اكتب العنوان كاملًا للتأكيد.", "صندوق و همه پیام‌های آن برای همیشه حذف می‌شود. برای تأیید آدرس کامل را بنویسید."),
            ["Deleted"] = ("Mailbox deleted", "تم حذف صندوق البريد", "صندوق حذف شد"),
            ["Error"] = ("Something went wrong. Try again.", "حدث خطأ. حاول مرة أخرى.", "مشکلی پیش آمد. دوباره تلاش کنید."),
            ["UnlabeledHint"] = ("Made outside this panel. Pick a label to start tracking it.", "أُنشئ خارج هذه اللوحة. اختر تصنيفًا لتتبعه.", "خارج از این پنل ساخته شده؛ یک برچسب انتخاب کنید."),
            ["NewEmail"] = ("New email", "بريد جديد", "ایمیل جدید"),
            ["Of"] = ("of", "من", "از"),
            ["Refresh"] = ("Refresh", "تحديث", "تازه‌سازی"),
        };

        private static int Lang()
        {
            var c = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return c == "ar" ? 1 : c == "fa" ? 2 : 0;
        }

        public static string Get(string key)
        {
            if (!T.TryGetValue(key, out var v)) return key;
            return Lang() switch { 1 => v.Ar, 2 => v.Fa, _ => v.En };
        }

        /// <summary>All strings for the current language, for the page script.</summary>
        public static Dictionary<string, string> All() => T.Keys.ToDictionary(k => k, Get);
    }
}

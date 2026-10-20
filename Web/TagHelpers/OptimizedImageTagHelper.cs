using Microsoft.AspNetCore.Razor.Runtime.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Web.TagHelpers
{
    [HtmlTargetElement("optimized-img")]
    public class OptimizedImageTagHelper : TagHelper
    {
        [HtmlAttributeName("src")] public string Src { get; set; }

        [HtmlAttributeName("width")] public int Width { get; set; }

        [HtmlAttributeName("height")] public int Height { get; set; }

        [HtmlAttributeName("alt")] public string Alt { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "img";
            output.TagMode = TagMode.SelfClosing;

            if (string.IsNullOrWhiteSpace(Src))
            {
                output.SuppressOutput();
                return;
            }

            // تمیز کردن مسیر: حذف علامت ~ برای جلوگیری از خطای مسیر در کنترلر
            string cleanPath = Src.StartsWith("~/") ? Src.Substring(1) : Src;

            // ساخت URL برای فراخوانی کنترلر پردازش عکس
            string generatedUrl =
                $"/resize?imagePath={Uri.EscapeDataString(cleanPath)}&width={Width}&height={Height}";

            // تنظیم ویژگی‌ها
            output.Attributes.SetAttribute("src", generatedUrl);

            if (Width > 0) output.Attributes.SetAttribute("width", Width);
            if (Height > 0) output.Attributes.SetAttribute("height", Height);
            if (!string.IsNullOrEmpty(Alt)) output.Attributes.SetAttribute("alt", Alt);

            output.Attributes.SetAttribute("loading", "lazy");

            // کلاس‌های tailwind شما (w-full h-full و...) به صورت خودکار توسط فریم‌ورک به تگ img منتقل می‌شوند
        }
    }
}

import { Bot, InlineKeyboard, Keyboard } from '@tgcloud/bot';

// مقداردهی اولیه بات (توکن به صورت خودکار توسط پلتفرم مدیریت می‌شود)
const bot = new Bot();

// ۱. ساخت کیبورد اصلی (دکمه‌های پایین صفحه: سلام و خداحافظ)
const mainKeyboard = new Keyboard()
  .text('سلام')
  .text('خداحافظ')
  .resized();

// هندلر دستور /start
bot.command('start', async (ctx) => {
  // پاک کردن حالت‌های قبلی کاربر در دیتابیس (در صورت وجود)
  await ctx.db.delete(`state:${ctx.from.id}`);
  
  await ctx.reply('خوش آمدید! یکی از گزینه‌های زیر را انتخاب کنید:', {
    reply_markup: mainKeyboard,
  });
});

// هندلر دکمه اصلی "سلام"
bot.hears('سلام', async (ctx) => {
  // ساخت دکمه شیشه‌ای "خوبی؟"
  const inlineKeyboard = new InlineKeyboard()
    .text('خوبی؟', 'how_are_you');

  await ctx.reply('سلام!', {
    reply_markup: inlineKeyboard,
  });
});

// هندلر دکمه اصلی "خداحافظ"
bot.hears('خداحافظ', async (ctx) => {
  // پاک کردن وضعیت کاربر در صورت خداحافظی
  await ctx.db.delete(`state:${ctx.from.id}`);
  await ctx.reply('خداحافظ!', {
    reply_markup: mainKeyboard,
  });
});

// هندلر کلیک روی دکمه شیشه‌ای "خوبی؟"
bot.callbackQuery('how_are_you', async (ctx) => {
  // پاسخ به تلگرام برای برداشتن حالت لودینگ دکمه شیشه‌ای
  await ctx.answerCallbackQuery();

  // ساخت دو دکمه شیشه‌ای "خوبم" و "بدم"
  const inlineKeyboard = new InlineKeyboard()
    .text('خوبم 😊', 'feel_good')
    .text('بدم 🥺', 'feel_bad');

  // ویرایش پیام قبلی و نمایش سوال جدید همراه با دکمه‌ها
  await ctx.editMessageText('خوبم تو چطوری؟', {
    reply_markup: inlineKeyboard,
  });
});

// هندلر کلیک روی دکمه شیشه‌ای "خوبم"
bot.callbackQuery('feel_good', async (ctx) => {
  await ctx.answerCallbackQuery();
  await ctx.editMessageText('خداروشکر که میشنوم من باید برم خداحافظ');
});

// هندلر کلیک روی دکمه شیشه‌ای "بدم"
bot.callbackQuery('feel_bad', async (ctx) => {
  await ctx.answerCallbackQuery();
  await ctx.editMessageText('ایشالا که بهتر بشی من باید برم خداحافظ');
});

// خروجی گرفتن برای پلتفرم سرورلس تلگرام
export default bot;
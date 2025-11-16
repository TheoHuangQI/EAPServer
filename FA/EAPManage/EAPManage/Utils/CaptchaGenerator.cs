using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;
using System.Drawing;
using System.Text;

namespace EAPManage.Utils
{
    public class CaptchaGenerator
    {
        private readonly IMemoryCache _cache;
        private readonly Random _random = new Random();

        public CaptchaGenerator(IMemoryCache cache)
        {
            _cache = cache;
        }

        // 生成验证码字符串
        public string GenerateCaptchaText(int length = 4)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[_random.Next(s.Length)]).ToArray());
        }

        // 生成验证码图片并返回 Base64 编码
        public (string captchaBase64, string captchaId) GenerateCaptchaImage()
        {
            string captchaText = GenerateCaptchaText();
            string captchaId = _random.Next(10000000).ToString(); // 唯一标识符

            // 缓存验证码文本
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5)); // 验证码有效期5分钟
            _cache.Set(captchaId, captchaText, cacheEntryOptions);

            // 生成验证码图片
            using var surface = SKSurface.Create(200, 80, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = surface.Canvas;

            // 绘制背景
            canvas.Clear(SKColors.White);

            // 绘制验证码文本
            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 48,
                IsAntialias = true,
                FakeBoldText = true,
                TextAlign = SKTextAlign.Center
            };

            canvas.DrawText(captchaText, 100, 50, paint);

            // 绘制干扰线和点
            using var linePaint = new SKPaint
            {
                Color = new SKColor(220, 220, 220),
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke
            };

            for (int i = 0; i < 5; i++)
            {
                canvas.DrawLine(new SKPoint(_random.Next(200), _random.Next(80)),
                                new SKPoint(_random.Next(200), _random.Next(80)), linePaint);
            }

            using var pointPaint = new SKPaint
            {
                Color = new SKColor(220, 220, 220)
            };

            for (int i = 0; i < 100; i++)
            {
                canvas.DrawCircle(_random.Next(200), _random.Next(80), _random.Next(2), pointPaint);
            }

            // 将图片转换为 PNG 格式
            using var image = surface.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = png.ToArray();

            // 转换为 Base64 编码
            string base64 = Convert.ToBase64String(bytes);

            return (base64, captchaId);
        }

        // 验证验证码
        public bool ValidateCaptcha(string captchaId, string userInput)
        {
            if (_cache.TryGetValue(captchaId, out string cachedValue))
            {
                _cache.Remove(captchaId); // 验证成功后删除缓存
                return cachedValue.Equals(userInput, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
}

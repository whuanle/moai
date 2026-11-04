using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoAI.Auth.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Auth.Services;

/// <summary>
/// 图形验证码服务实现：4 位随机字符（排除易混字符）自绘 SVG（字符随机旋转偏移 + 干扰弧线 + 噪点 + 贯穿线，零外部依赖），
/// 答案存 Redis（<c>login:captcha:{id}</c>）5 分钟有效，连续错 3 次作废；校验不区分大小写.
/// </summary>
public class CaptchaService : ICaptchaService
{
    private const int TtlMinutes = 5;

    private const int MaxVerifyAttempts = 3;

    private const int CodeLength = 4;

    /// <summary>
    /// 验证码字符集：排除 0/O、1/I/L 等易混字符.
    /// </summary>
    private const string CodeChars = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    private readonly IRedisDatabase _redisDatabase;

    private sealed record CaptchaState(string Code, int Attempts);

    /// <summary>
    /// Initializes a new instance of the <see cref="CaptchaService"/> class.
    /// </summary>
    public CaptchaService(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task<CaptchaInfo> GenerateAsync(CancellationToken cancellationToken)
    {
        var code = new string(Enumerable
            .Range(0, CodeLength)
            .Select(_ => CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)])
            .ToArray());

        var captchaId = Guid.CreateVersion7().ToString("N");
        await _redisDatabase.Database.StringSetAsync(
            CaptchaKey(captchaId),
            JsonSerializer.Serialize(new CaptchaState(code, 0)),
            TimeSpan.FromMinutes(TtlMinutes));

        var svg = RenderSvg(code);
        return new CaptchaInfo
        {
            CaptchaId = captchaId,
            Image = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg)),
            ImageMimeType = "image/svg+xml",
            ExpiresInSeconds = (int)TimeSpan.FromMinutes(TtlMinutes).TotalSeconds
        };
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyAsync(string captchaId, string code, bool consumeOnSuccess, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(captchaId) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        captchaId = captchaId.Trim();
        code = code.Trim();

        var key = CaptchaKey(captchaId);
        var json = await _redisDatabase.Database.StringGetAsync(key);
        if (json.IsNull || json.IsNullOrEmpty)
        {
            return false;
        }

        var state = JsonSerializer.Deserialize<CaptchaState>(json.ToString())!;
        if (!string.Equals(state.Code, code, StringComparison.OrdinalIgnoreCase))
        {
            var attempts = state.Attempts + 1;
            if (attempts >= MaxVerifyAttempts)
            {
                await _redisDatabase.Database.KeyDeleteAsync(key);
            }
            else
            {
                // 错误累计但保留原 TTL，防止反复续期
                var ttl = await _redisDatabase.Database.KeyTimeToLiveAsync(key);
                await _redisDatabase.Database.StringSetAsync(
                    key,
                    JsonSerializer.Serialize(state with { Attempts = attempts }),
                    ttl ?? TimeSpan.FromMinutes(TtlMinutes));
            }

            return false;
        }

        if (consumeOnSuccess)
        {
            await _redisDatabase.Database.KeyDeleteAsync(key);
        }

        return true;
    }

    private static string CaptchaKey(string captchaId) => $"login:captcha:{captchaId}";

    /// <summary>
    /// 自绘 SVG 验证码图片：底色 + 3 条干扰弧线 + 噪点 + 逐字符随机旋转/偏移/字号/配色 + 1 条贯穿线.
    /// </summary>
    private static string RenderSvg(string code)
    {
        const int width = 120;
        const int height = 40;

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        sb.Append(CultureInfo.InvariantCulture, $"<rect width=\"{width}\" height=\"{height}\" fill=\"#f5f6f8\"/>");

        string[] curveColors = ["#8ca6c8", "#c8a2b8", "#a2c8b0"];
        for (var i = 0; i < curveColors.Length; i++)
        {
            var x1 = RandomNumberGenerator.GetInt32(0, width / 2);
            var y1 = RandomNumberGenerator.GetInt32(4, height - 4);
            var cx = RandomNumberGenerator.GetInt32(width / 3, width * 2 / 3);
            var cy = RandomNumberGenerator.GetInt32(4, height - 4);
            var x2 = RandomNumberGenerator.GetInt32(width / 2, width);
            var y2 = RandomNumberGenerator.GetInt32(4, height - 4);
            sb.Append(CultureInfo.InvariantCulture, $"<path d=\"M {x1} {y1} Q {cx} {cy} {x2} {y2}\" fill=\"none\" stroke=\"{curveColors[i]}\" stroke-width=\"1.2\" opacity=\"0.55\"/>");
        }

        for (var i = 0; i < 36; i++)
        {
            var cx = RandomNumberGenerator.GetInt32(0, width);
            var cy = RandomNumberGenerator.GetInt32(0, height);
            sb.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"1\" fill=\"#9aa3ad\" opacity=\"0.5\"/>");
        }

        string[] fontColors = ["#2f54eb", "#c41d7f", "#08979c", "#531dab", "#d46b08", "#237804"];
        var step = (width - 28) / code.Length;
        for (var i = 0; i < code.Length; i++)
        {
            var x = 14 + (i * step) + RandomNumberGenerator.GetInt32(-3, 4);
            var y = 27 + RandomNumberGenerator.GetInt32(-3, 4);
            var rotate = RandomNumberGenerator.GetInt32(-28, 29);
            var fontSize = RandomNumberGenerator.GetInt32(22, 27);
            var color = fontColors[RandomNumberGenerator.GetInt32(fontColors.Length)];
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{x}\" y=\"{y}\" font-family=\"Georgia, serif\" font-size=\"{fontSize}\" font-weight=\"bold\" font-style=\"italic\" fill=\"{color}\" transform=\"rotate({rotate} {x} {y})\">{code[i]}</text>");
        }

        var ly = RandomNumberGenerator.GetInt32(12, height - 12);
        sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"0\" y1=\"{ly}\" x2=\"{width}\" y2=\"{height - ly}\" stroke=\"#666666\" stroke-width=\"1\" opacity=\"0.45\"/>");
        sb.Append("</svg>");
        return sb.ToString();
    }
}

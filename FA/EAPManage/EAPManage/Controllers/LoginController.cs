using EAPManage.DataBase;
using EAPManage.Models.DB;
using EAPManage.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {

        private readonly IConfiguration _configuration;

        public LoginController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 获取验证码
        [HttpGet("getCaptcha")]
        public IActionResult GetCaptcha()
        {
            IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
            CaptchaGenerator captcha = new CaptchaGenerator(cache);
            var (captchaBase64, captchaId) = captcha.GenerateCaptchaImage();
            var captchaImg = $"data:image/png;base64,{captchaBase64}";
            return Ok(new
            {
                captchaId,
                captchaImg
            });
        }

        [HttpPost]
        public IActionResult Login([FromBody] User user)
        {
            var res = DataFactory.Current.User.GetList(u => u.Name ==user.Name && u.Password ==user.Password);
            if (res == null || res.Count !=1)
            {
                return Unauthorized(new { Return="NG",MESSAGE= "用户名或密码错误" });
            }
            var token = GenerateJwtToken(res[0]);
            return Ok(new { Return="OK", MESSAGE = "SUCCESS", Token = token });
        }




        private string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpireMinutes"])),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

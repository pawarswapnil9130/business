using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;
using ApparelERP.Api.Data;
using ApparelERP.Api.Models;

namespace ApparelERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly ApparelDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SettingsController(ApparelDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("{key}")]
        public async Task<IActionResult> GetSetting(string key)
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting == null) return Content("{}", "application/json");
            return Content(setting.Value, "application/json");
        }

        [HttpPost("{key}")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
        public async Task<IActionResult> SaveSetting(string key, [FromBody] object value)
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            var jsonValue = System.Text.Json.JsonSerializer.Serialize(value);

            if (setting == null)
            {
                setting = new SystemSetting { Key = key, Value = jsonValue };
                _context.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = jsonValue;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Setting saved successfully" });
        }

        [HttpPost("upload-qr")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
        public async Task<IActionResult> UploadQrCode([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "qr");
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            var fileName = $"qr_{System.DateTime.UtcNow.Ticks}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new { url = $"/uploads/qr/{fileName}" });
        }
    }
}

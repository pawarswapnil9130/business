using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApparelERP.Api.Data;
using ApparelERP.Api.Models;

namespace ApparelERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN,EMPLOYEE,CA")]
    public class ProductsController : ControllerBase
    {
        private readonly ApparelDbContext _context;

        public ProductsController(ApparelDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            return await _context.Products.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            return product;
        }

        [HttpPost]
        public async Task<ActionResult<Product>> CreateProduct(Product product)
        {
            product.CreatedAt = System.DateTime.UtcNow;
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, Product product)
        {
            if (id != product.Id)
            {
                return BadRequest();
            }

            _context.Entry(product).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Delete related rows sequentially to ensure Postgres ON DELETE RESTRICT constraints are not violated
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM stock_ledger WHERE product_id = {0}", id);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM sales_order_items WHERE product_id = {0}", id);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM trading_purchases WHERE product_id = {0}", id);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM production_batches WHERE product_id = {0}", id);

            // Finally, remove the product
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id}/upload-image")]
        public async Task<IActionResult> UploadProductImage(int id, [FromForm] IFormFile file)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = "Product not found." });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No image file provided." });
            }

            var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var ext = System.IO.Path.GetExtension(file.FileName).ToLower();
            if (!allowedExts.Contains(ext))
            {
                return BadRequest(new { message = "Only JPG, PNG, WEBP, or GIF image formats are supported." });
            }

            var webRoot = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsDir = System.IO.Path.Combine(webRoot, "uploads", "products");
            if (!System.IO.Directory.Exists(uploadsDir))
            {
                System.IO.Directory.CreateDirectory(uploadsDir);
            }

            var fileName = $"prod_{id}_{System.DateTime.UtcNow.Ticks}{ext}";
            var filePath = System.IO.Path.Combine(uploadsDir, fileName);

            using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativeUrl = $"/uploads/products/{fileName}";
            product.ImageUrl = relativeUrl;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Product image uploaded successfully!", imageUrl = relativeUrl });
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}

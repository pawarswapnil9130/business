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
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public ProductsController(ApparelDbContext context, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            var products = await _context.Products.ToListAsync();
            
            var stockTotals = await _context.StockLedgerEntries
                .GroupBy(sl => sl.ProductId)
                .Select(g => new { ProductId = g.Key, Total = g.Sum(sl => sl.QuantityChange) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Total);
                
            foreach(var p in products)
            {
                p.CurrentStock = stockTotals.ContainsKey(p.Id) ? stockTotals[p.Id] : 0;
            }
            return products;
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

            if (product.CurrentStock > 0)
            {
                _context.StockLedgerEntries.Add(new StockLedger
                {
                    ProductId = product.Id,
                    QuantityChange = product.CurrentStock,
                    TransactionType = "ADJUSTMENT"
                });
                await _context.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, Product product)
        {
            if (product.Id == 0)
            {
                product.Id = id;
            }

            if (id != product.Id)
            {
                return BadRequest(new { message = "ID mismatch in payload." });
            }

            _context.Entry(product).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();

                var currentActualStock = await _context.StockLedgerEntries
                    .Where(sl => sl.ProductId == id)
                    .SumAsync(sl => sl.QuantityChange);

                var diff = product.CurrentStock - currentActualStock;
                if (diff != 0)
                {
                    _context.StockLedgerEntries.Add(new StockLedger
                    {
                        ProductId = id,
                        QuantityChange = diff,
                        TransactionType = "ADJUSTMENT"
                    });
                    await _context.SaveChangesAsync();
                }
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

            // Cloudinary Upload
            var cloudName = _configuration["Cloudinary:CloudName"];
            var apiKey = _configuration["Cloudinary:ApiKey"];
            var apiSecret = _configuration["Cloudinary:ApiSecret"];

            if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
            {
                return StatusCode(500, new { message = "Cloudinary configuration is missing on the server." });
            }

            var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
            var cloudinary = new CloudinaryDotNet.Cloudinary(account);

            var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams()
            {
                File = new CloudinaryDotNet.FileDescription(file.FileName, file.OpenReadStream()),
                Folder = "casa_apparel/products",
                PublicId = $"prod_{id}_{System.DateTime.UtcNow.Ticks}"
            };

            var uploadResult = await cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                return BadRequest(new { message = $"Image upload failed: {uploadResult.Error.Message}" });
            }

            product.ImageUrl = uploadResult.SecureUrl.ToString();
            await _context.SaveChangesAsync();

            return Ok(new { message = "Product image uploaded successfully!", imageUrl = product.ImageUrl });
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}

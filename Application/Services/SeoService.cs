using System;
using System.Net.Http;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
    public partial class SeoService : ISeoService
    {
        private readonly IApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly HttpClient _httpClient;

        public SeoService(IApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
            
            // Bypass SSL certificate issues locally
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SEO-Health-Monitor-Bot/1.0");
        }

        public async Task<bool> IsGlobalNoindexActiveAsync()
        {
            var setting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Title == "SEO.GlobalNoindex");
            
            return setting?.DataBool ?? false;
        }

        public async Task SetGlobalNoindexAsync(bool noindex)
        {
            var setting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Title == "SEO.GlobalNoindex");

            if (setting == null)
            {
                setting = new Setting
                {
                    Title = "SEO.GlobalNoindex",
                    Category = "SEO",
                    Description = "Blocks all search indexing using noindex tags globally",
                    DataBool = noindex,
                    Changeable = true,
                    DisplayOrder = 100,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Settings.Add(setting);
            }
            else
            {
                setting.DataBool = noindex;
                setting.UpdatedAt = DateTime.UtcNow;
                _context.Settings.Update(setting);
            }

            await _context.SaveChangesAsync();
        }
    }
}

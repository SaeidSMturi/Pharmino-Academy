using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Domain.Entities.Seo;

namespace Application.Services
{
    public partial class SeoService
    {
        public async Task<List<SeoRedirect>> GetRedirectsAsync()
        {
            return await _context.SeoRedirects
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<SeoRedirect?> GetRedirectByIdAsync(int id)
        {
            return await _context.SeoRedirects.FindAsync(id);
        }

        public async Task<SeoRedirect?> GetRedirectByOldUrlAsync(string oldUrl)
        {
            var cleanUrl = NormalizeUrlPath(oldUrl);
            return await _context.SeoRedirects
                .FirstOrDefaultAsync(r => r.IsActive && r.OldUrl == cleanUrl);
        }

        public async Task AddRedirectAsync(SeoRedirect redirect)
        {
            redirect.OldUrl = NormalizeUrlPath(redirect.OldUrl);
            redirect.NewUrl = NormalizeUrlPath(redirect.NewUrl);
            
            _context.SeoRedirects.Add(redirect);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRedirectAsync(SeoRedirect redirect)
        {
            redirect.OldUrl = NormalizeUrlPath(redirect.OldUrl);
            redirect.NewUrl = NormalizeUrlPath(redirect.NewUrl);

            _context.SeoRedirects.Update(redirect);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteRedirectAsync(int id)
        {
            var redirect = await _context.SeoRedirects.FindAsync(id);
            if (redirect != null)
            {
                _context.SeoRedirects.Remove(redirect);
                await _context.SaveChangesAsync();
            }
        }

        private string NormalizeUrlPath(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "/";
            
            url = url.Trim().ToLower();
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                try
                {
                    var uri = new Uri(url);
                    url = uri.PathAndQuery;
                }
                catch { }
            }

            if (!url.StartsWith("/"))
            {
                url = "/" + url;
            }

            return url;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities.Seo;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
    public partial class SeoService : ISeoService
    {
        public async Task<List<PageSeo>> GetPageSeosAsync()
        {
            return await _context.PageSeos
                .OrderBy(p => p.PagePath)
                .ToListAsync();
        }

        public async Task<PageSeo?> GetPageSeoByPathAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var normalizedPath = NormalizePagePath(path);

            return await _context.PageSeos
                .FirstOrDefaultAsync(p => p.IsActive && p.PagePath.ToLower() == normalizedPath.ToLower());
        }

        public async Task<PageSeo?> GetPageSeoByIdAsync(int id)
        {
            return await _context.PageSeos.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task AddOrUpdatePageSeoAsync(PageSeo pageSeo)
        {
            if (pageSeo == null) throw new ArgumentNullException(nameof(pageSeo));

            pageSeo.PagePath = NormalizePagePath(pageSeo.PagePath);
            pageSeo.UpdatedAt = DateTime.UtcNow;

            var existing = await _context.PageSeos
                .FirstOrDefaultAsync(p => p.Id == pageSeo.Id || p.PagePath.ToLower() == pageSeo.PagePath.ToLower());

            if (existing != null)
            {
                existing.PagePath = pageSeo.PagePath;
                existing.Title = pageSeo.Title;
                existing.MetaDescription = pageSeo.MetaDescription;
                existing.MetaKeywords = pageSeo.MetaKeywords;
                existing.MetaRobots = pageSeo.MetaRobots;
                existing.CanonicalUrl = pageSeo.CanonicalUrl;
                existing.OgTitle = pageSeo.OgTitle;
                existing.OgDescription = pageSeo.OgDescription;
                existing.OgImage = pageSeo.OgImage;
                existing.OgType = pageSeo.OgType;
                existing.JsonLd = pageSeo.JsonLd;
                existing.IsActive = pageSeo.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;

                _context.PageSeos.Update(existing);
            }
            else
            {
                _context.PageSeos.Add(pageSeo);
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeletePageSeoAsync(int id)
        {
            var item = await _context.PageSeos.FirstOrDefaultAsync(p => p.Id == id);
            if (item != null)
            {
                _context.PageSeos.Remove(item);
                await _context.SaveChangesAsync();
            }
        }

        private static string NormalizePagePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return "/";

            var trimmed = rawPath.Trim();
            if (!trimmed.StartsWith("/")) trimmed = "/" + trimmed;
            if (trimmed.Length > 1 && trimmed.EndsWith("/")) trimmed = trimmed.TrimEnd('/');

            return trimmed;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using MediaTracker.Data;
using MediaTracker.Models;

namespace MediaTracker.Services;

public class MediaService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public MediaService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<MediaItem>> GetAllAsync(
        MediaType? typeFilter = null,
        MediaStatus? statusFilter = null,
        string? searchQuery = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.MediaItems
            .AsNoTracking()
            .Include(m => m.Episodes)
            .Include(m => m.GameProgress)
            .AsSplitQuery()
            .AsQueryable();

        if (typeFilter.HasValue)
            query = query.Where(m => m.MediaType == typeFilter.Value);

        if (statusFilter.HasValue)
            query = query.Where(m => m.Status == statusFilter.Value);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var search = $"%{searchQuery.Trim()}%";
            query = query.Where(m =>
                EF.Functions.Like(m.Title, search) ||
                (m.OriginalTitle != null && EF.Functions.Like(m.OriginalTitle, search)));
        }

        return await query.OrderByDescending(m => m.UpdatedAt).ToListAsync();
    }

    public async Task<MediaItem?> GetByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.MediaItems
            .Include(m => m.Episodes.OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber))
            .Include(m => m.GameProgress)
            .Include(m => m.ProviderMappings)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<MediaItem> CreateAsync(MediaItem item)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        item.CreatedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        db.MediaItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task UpdateAsync(MediaItem item)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        item.UpdatedAt = DateTime.UtcNow;
        db.MediaItems.Update(item);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var item = await db.MediaItems.FindAsync(id);
        if (item is not null)
        {
            db.MediaItems.Remove(item);
            await db.SaveChangesAsync();
        }
    }

    public async Task<int> GetCountAsync(MediaType? typeFilter = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.MediaItems.AsQueryable();
        if (typeFilter.HasValue)
            query = query.Where(m => m.MediaType == typeFilter.Value);
        return await query.CountAsync();
    }

    public async Task AddProviderMappingAsync(int mediaItemId, ProviderMapping mapping)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        mapping.MediaItemId = mediaItemId;
        db.ProviderMappings.Add(mapping);
        await db.SaveChangesAsync();
    }

    // Episode management

    public async Task<List<Episode>> GetEpisodesAsync(int mediaItemId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Episodes
            .Where(e => e.MediaItemId == mediaItemId)
            .OrderBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .ToListAsync();
    }

    public async Task<int> AddEpisodesAsync(List<Episode> episodes)
    {
        if (episodes.Count == 0)
            return 0;

        await using var db = await _dbFactory.CreateDbContextAsync();

        int mediaItemId = episodes[0].MediaItemId;
        var existingKeys = await db.Episodes
            .Where(e => e.MediaItemId == mediaItemId)
            .Select(e => new { e.SeasonNumber, e.EpisodeNumber })
            .ToListAsync();

        var knownKeys = existingKeys
            .Select(key => $"{key.SeasonNumber}:{key.EpisodeNumber}")
            .ToHashSet(StringComparer.Ordinal);

        var newEpisodes = episodes
            .Where(ep => knownKeys.Add($"{ep.SeasonNumber}:{ep.EpisodeNumber}"))
            .ToList();

        if (newEpisodes.Count == 0)
            return 0;

        db.Episodes.AddRange(newEpisodes);

        var item = await db.MediaItems.FindAsync(mediaItemId);
        if (item is not null)
            item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return newEpisodes.Count;
    }

    public async Task ToggleEpisodeWatchedAsync(int episodeId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var ep = await db.Episodes.FindAsync(episodeId);
        if (ep is null) return;

        ep.IsWatched = !ep.IsWatched;
        ep.WatchedAt = ep.IsWatched ? DateTime.UtcNow : null;

        var item = await db.MediaItems.FindAsync(ep.MediaItemId);
        if (item is not null)
            item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    public async Task MarkSeasonWatchedAsync(int mediaItemId, int seasonNumber, bool watched)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var episodes = await db.Episodes
            .Where(e => e.MediaItemId == mediaItemId && e.SeasonNumber == seasonNumber)
            .ToListAsync();

        foreach (var ep in episodes)
        {
            ep.IsWatched = watched;
            ep.WatchedAt = watched ? DateTime.UtcNow : null;
        }

        var item = await db.MediaItems.FindAsync(mediaItemId);
        if (item is not null)
            item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    public async Task SetEpisodeScoreAsync(int episodeId, int? score)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var ep = await db.Episodes.FindAsync(episodeId);
        if (ep is null) return;

        ep.UserScore = score;

        var item = await db.MediaItems.FindAsync(ep.MediaItemId);
        if (item is not null)
            item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    public async Task UpdateGameProgressAsync(GameProgress progress)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.GameProgresses
            .FirstOrDefaultAsync(g => g.MediaItemId == progress.MediaItemId);

        if (existing is null)
        {
            db.GameProgresses.Add(progress);
        }
        else
        {
            existing.HoursPlayed = progress.HoursPlayed;
            existing.CurrentStage = progress.CurrentStage;
            existing.Platform = progress.Platform;
            existing.CompletionState = progress.CompletionState;
        }

        var item = await db.MediaItems.FindAsync(progress.MediaItemId);
        if (item is not null)
            item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }
}

﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;

namespace HocrEditor.Core;

public class SKBitmapManager(uint cacheMemoryLimitMegabytes = 1024)
{
    private ulong cacheMemoryUsageBytes;

    private readonly IDictionary<string, SKBitmapCacheItem> cache =
        new ConcurrentDictionary<string, SKBitmapCacheItem>(StringComparer.Ordinal);

    public ulong CacheMemoryLimitMegabytes { get; set; } = cacheMemoryLimitMegabytes;
    private ulong CacheMemoryLimitBytes => CacheMemoryLimitMegabytes * 1024 * 1024;

    public SKBitmapReference Get(string path, Func<Task<SKBitmap>> loadAction) => new(this, path, loadAction);

    public SKBitmapReference Get(string path, Func<SKBitmap> loadAction) =>
        new(this, path, () => Task.FromResult(loadAction()));

    private async Task<SKBitmap> GetBitmap(string key, Func<Task<SKBitmap>> loadAction)
    {
        if (cache.TryGetValue(key, out var cacheItem))
        {
            cacheItem.LastUsage = DateTime.Now;

            return cacheItem.Bitmap;
        }

        var bitmap = await Task.Run(loadAction)
            .ConfigureAwait(false);

        bitmap.SetImmutable();

        Set(key, bitmap);

        return bitmap;
    }

    private void Set(string key, SKBitmap bitmap)
    {
        if (CacheMemoryLimitBytes > 0 && bitmap.ByteCount > (int)CacheMemoryLimitBytes)
        {
            throw new InvalidOperationException(
                $"Bitmap requires {bitmap.ByteCount} which is more than the allotted {CacheMemoryLimitBytes}."
            );
        }

        while (CacheMemoryLimitBytes > 0 && cacheMemoryUsageBytes > CacheMemoryLimitBytes)
        {
            EvictLeastRecentlyUsed();
        }

        if (cache.TryAdd(key, new SKBitmapCacheItem(bitmap)))
        {
            cacheMemoryUsageBytes += (ulong)bitmap.ByteCount;
        }
    }

    private void EvictLeastRecentlyUsed()
    {
        var leastRecentlyUsed = cache
            .OrderBy(pair => pair.Value.LastUsage)
            .Select(pair => pair.Key)
            .FirstOrDefault();

        if (leastRecentlyUsed != null && cache.Remove(leastRecentlyUsed, out var item))
        {
            cacheMemoryUsageBytes -= (ulong)item.Bitmap.ByteCount;

            item.Bitmap.Dispose();
        }
    }

    private sealed class SKBitmapCacheItem(SKBitmap bitmap)
    {
        public SKBitmap Bitmap { get; } = bitmap;
        public DateTime LastUsage { get; set; } = DateTime.Now;
    }

    public sealed class SKBitmapReference
    {
        private readonly SKBitmapManager bitmapManager;
        private readonly string key;
        private readonly Func<Task<SKBitmap>> loadAction;

        internal SKBitmapReference(SKBitmapManager bitmapManager, string key, Func<Task<SKBitmap>> loadAction)
        {
            this.bitmapManager = bitmapManager;
            this.key = key;
            this.loadAction = loadAction;
        }

        public Task<SKBitmap> GetBitmap() => bitmapManager.GetBitmap(key, loadAction);
    }
}

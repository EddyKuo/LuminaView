using SQLite;
using System;
using System.IO;

namespace PhotoViewer.Core.Services;

/// <summary>
/// 圖片評分持久化服務（使用 SQLite）
/// </summary>
public class RatingService : IDisposable
{
    private readonly SQLiteConnection _db;
    private bool _disposed = false;

    public RatingService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbPath = Path.Combine(appData, "LuminaView", "ratings.db");
        
        // 確保目錄存在
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _db = new SQLiteConnection(dbPath);
        _db.CreateTable<ImageRating>();
    }

    /// <summary>
    /// 取得圖片評分
    /// </summary>
    public int GetRating(string filePath)
    {
        var rating = _db.Table<ImageRating>().FirstOrDefault(x => x.FilePath == filePath);
        return rating?.Rating ?? 0;
    }

    /// <summary>
    /// 設定圖片評分
    /// </summary>
    public void SetRating(string filePath, int rating)
    {
        // 確保評分在有效範圍內
        rating = Math.Clamp(rating, 0, 5);

        var existing = _db.Table<ImageRating>().FirstOrDefault(x => x.FilePath == filePath);
        if (existing != null)
        {
            if (rating == 0)
            {
                // 刪除評分記錄
                _db.Delete(existing);
            }
            else
            {
                existing.Rating = rating;
                existing.UpdatedAt = DateTime.Now;
                _db.Update(existing);
            }
        }
        else if (rating > 0)
        {
            _db.Insert(new ImageRating
            {
                FilePath = filePath,
                Rating = rating,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });
        }
    }

    /// <summary>
    /// 批次取得多個檔案的評分
    /// </summary>
    public Dictionary<string, int> GetRatings(IEnumerable<string> filePaths)
    {
        var result = new Dictionary<string, int>();
        var pathList = filePaths.ToList();
        
        // SQLite 對於 IN 查詢有限制，分批處理
        foreach (var rating in _db.Table<ImageRating>().Where(x => pathList.Contains(x.FilePath)))
        {
            result[rating.FilePath] = rating.Rating;
        }
        
        return result;
    }

    /// <summary>
    /// 取得所有評分記錄
    /// </summary>
    public Dictionary<string, int> GetAllRatings()
    {
        return _db.Table<ImageRating>().ToDictionary(x => x.FilePath, x => x.Rating);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _db?.Close();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

/// <summary>
/// 圖片評分資料模型
/// </summary>
public class ImageRating
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public string FilePath { get; set; } = string.Empty;
    
    public int Rating { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

using System.IO;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using PhotoViewer.Core.Models;

namespace PhotoViewer.Core.Services;

/// <summary>
/// EXIF 資訊讀取服務
/// 負責從圖片檔案中提取拍攝參數和元數據
/// </summary>
public class ExifService
{
    #region Public Methods

    /// <summary>
    /// 讀取結構化的 EXIF 資訊（常用拍攝參數）
    /// </summary>
    /// <param name="filePath">圖片檔案路徑</param>
    /// <returns>包含相機、鏡頭、曝光參數等資訊的 ExifInfo 物件</returns>
    public ExifInfo GetExifInfo(string filePath)
    {
        var info = new ExifInfo();

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            // 讀取相機資訊
            ReadCameraInfo(directories, info);

            // 讀取拍攝參數
            ReadExposureInfo(directories, info);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EXIF] 讀取結構化 EXIF 失敗: {ex.Message}");
        }

        return info;
    }

    /// <summary>
    /// 讀取完整 EXIF 資訊（所有 metadata 標籤）
    /// </summary>
    /// <param name="filePath">圖片檔案路徑</param>
    /// <returns>包含所有 metadata 的字典</returns>
    public Dictionary<string, string> GetExifData(string filePath)
    {
        var exifData = new Dictionary<string, string>();

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            // 提取所有 metadata 標籤
            ExtractAllMetadata(directories, exifData);

            // 補充檔案資訊
            AddFileInfo(filePath, exifData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EXIF] 讀取完整 EXIF 失敗: {ex.Message}");
            AddBasicFileInfo(filePath, exifData);
        }

        return exifData;
    }

    #endregion

    #region Private Methods - Camera Info

    /// <summary>
    /// 讀取相機資訊（製造商、型號）
    /// </summary>
    private void ReadCameraInfo(IEnumerable<MetadataExtractor.Directory> directories, ExifInfo info)
    {
        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        if (ifd0 == null) return;

        info.Make = ifd0.GetString(ExifIfd0Directory.TagMake) ?? "";
        info.Model = ifd0.GetString(ExifIfd0Directory.TagModel) ?? "";
    }

    #endregion

    #region Private Methods - Exposure Info

    /// <summary>
    /// 讀取拍攝參數（光圈、快門、ISO、焦距等）
    /// </summary>
    private void ReadExposureInfo(IEnumerable<MetadataExtractor.Directory> directories, ExifInfo info)
    {
        var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        if (subIfd == null) return;

        // 鏡頭型號
        info.Lens = subIfd.GetString(ExifSubIfdDirectory.TagLensModel) ?? "";

        // 光圈
        ReadFNumber(subIfd, info);

        // 快門速度
        ReadExposureTime(subIfd, info);

        // ISO
        ReadIso(subIfd, info);

        // 焦距
        ReadFocalLength(subIfd, info);

        // 拍攝時間
        ReadDateTaken(subIfd, info);
    }

    private void ReadFNumber(ExifSubIfdDirectory subIfd, ExifInfo info)
    {
        if (!subIfd.ContainsTag(ExifSubIfdDirectory.TagFNumber)) return;

        double fNumber = subIfd.GetDouble(ExifSubIfdDirectory.TagFNumber);
        info.FNumber = $"f/{fNumber:0.0}";
    }

    private void ReadExposureTime(ExifSubIfdDirectory subIfd, ExifInfo info)
    {
        if (!subIfd.ContainsTag(ExifSubIfdDirectory.TagExposureTime)) return;

        double exposureTime = subIfd.GetDouble(ExifSubIfdDirectory.TagExposureTime);
        info.ExposureTime = exposureTime < 1.0
            ? $"1/{Math.Round(1.0 / exposureTime)}"
            : $"{exposureTime}s";
    }

    private void ReadIso(ExifSubIfdDirectory subIfd, ExifInfo info)
    {
        string? iso = subIfd.GetString(ExifSubIfdDirectory.TagIsoEquivalent);

        // 備用 ISO 標籤 (某些相機使用不同標籤)
        if (string.IsNullOrEmpty(iso) && subIfd.ContainsTag(0x8833))
        {
            iso = subIfd.GetString(0x8833);
        }

        if (!string.IsNullOrEmpty(iso))
        {
            info.Iso = $"ISO {iso}";
        }
    }

    private void ReadFocalLength(ExifSubIfdDirectory subIfd, ExifInfo info)
    {
        if (!subIfd.ContainsTag(ExifSubIfdDirectory.TagFocalLength)) return;

        double focalLength = subIfd.GetDouble(ExifSubIfdDirectory.TagFocalLength);
        info.FocalLength = $"{focalLength}mm";
    }

    private void ReadDateTaken(ExifSubIfdDirectory subIfd, ExifInfo info)
    {
        if (!subIfd.ContainsTag(ExifSubIfdDirectory.TagDateTimeOriginal)) return;

        try
        {
            info.DateTaken = subIfd.GetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal);
        }
        catch
        {
            // 日期格式無效時忽略
        }
    }

    #endregion

    #region Private Methods - Metadata Extraction

    /// <summary>
    /// 提取所有 metadata 標籤
    /// </summary>
    private void ExtractAllMetadata(IEnumerable<MetadataExtractor.Directory> directories, Dictionary<string, string> exifData)
    {
        foreach (var directory in directories)
        {
            foreach (var tag in directory.Tags)
            {
                var key = $"{directory.Name} - {tag.Name}";

                if (!exifData.ContainsKey(key))
                {
                    exifData[key] = tag.Description ?? "";
                }
            }
        }
    }

    /// <summary>
    /// 補充檔案資訊
    /// </summary>
    private void AddFileInfo(string filePath, Dictionary<string, string> exifData)
    {
        var fileInfo = new FileInfo(filePath);

        TryAddValue(exifData, "File - Name", fileInfo.Name);
        TryAddValue(exifData, "File - Size", $"{fileInfo.Length / 1024.0:F2} KB");
        TryAddValue(exifData, "File - Created", fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
        TryAddValue(exifData, "File - Modified", fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    /// <summary>
    /// 僅補充基本檔案資訊（錯誤回退時使用）
    /// </summary>
    private void AddBasicFileInfo(string filePath, Dictionary<string, string> exifData)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            exifData["File - Name"] = fileInfo.Name;
            exifData["File - Size"] = $"{fileInfo.Length / 1024.0:F2} KB";
        }
        catch
        {
            // 無法讀取檔案資訊時忽略
        }
    }

    private void TryAddValue(Dictionary<string, string> dict, string key, string value)
    {
        if (!dict.ContainsKey(key))
        {
            dict[key] = value;
        }
    }

    #endregion
}

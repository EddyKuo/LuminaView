namespace PhotoViewer.Core.Models;

/// <summary>
/// 結構化的 EXIF 資訊
/// </summary>
public class ExifInfo
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Lens { get; set; } = string.Empty;
    public string FNumber { get; set; } = string.Empty;      // e.g. "f/1.8"
    public string ExposureTime { get; set; } = string.Empty; // e.g. "1/100 s"
    public string Iso { get; set; } = string.Empty;          // e.g. "ISO 100"
    public string FocalLength { get; set; } = string.Empty;  // e.g. "50 mm"
    public DateTime? DateTaken { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

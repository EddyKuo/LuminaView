namespace PhotoViewer.Core.Models;

/// <summary>
/// 檔案夾樹結構節點
/// </summary>
public class FolderNode
{
    /// <summary>
    /// 檔案夾完整路徑
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 檔案夾名稱（不含路徑）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 子檔案夾列表
    /// </summary>
    public List<FolderNode> SubFolders { get; set; } = new();

    /// <summary>
    /// 檔案夾內的圖片列表
    /// </summary>
    public List<ImageItem> Images { get; set; } = new();

    /// <summary>
    /// 最後掃描時間
    /// </summary>
    public DateTime LastScanned { get; set; }

    /// <summary>
    /// 圖片總數（包含子檔案夾）
    /// </summary>
    public int TotalImageCount { get; set; }

    /// <summary>
    /// 是否已展開（用於 UI 樹狀顯示）
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// 父節點引用
    /// </summary>
    public FolderNode? Parent { get; set; }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace PhotoViewer.Core.Models;

public class FolderNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;

    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public ObservableCollection<FolderNode> Children { get; set; } = new();

    // 用於延遲加載的標記項目
    private static readonly FolderNode DummyNode = new FolderNode { Name = "Loading..." };

    public bool HasDummyNode => Children.Count == 1 && Children[0] == DummyNode;

    public FolderNode()
    {
    }

    public FolderNode(string path)
    {
        FullPath = path;
        Name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(Name)) // 處理磁碟機根目錄 (e.g., "C:\")
        {
            Name = path;
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                
                if (_isExpanded && HasDummyNode)
                {
                    LoadChildren();
                }
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public void AddDummyNode()
    {
        Children.Clear();
        Children.Add(DummyNode);
    }

    private void LoadChildren()
    {
        Children.Clear();
        try
        {
            var directories = Directory.GetDirectories(FullPath);
            foreach (var dir in directories)
            {
                var node = new FolderNode(dir);
                // 檢查是否有子目錄，若有則添加 dummy node 以支援延遲加載
                try 
                {
                    if (Directory.EnumerateDirectories(dir).Any())
                    {
                        node.AddDummyNode();
                    }
                }
                catch (UnauthorizedAccessException) { } // 忽略無權限的目錄
                
                Children.Add(node);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // 忽略無權限的目錄
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading children for {FullPath}: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

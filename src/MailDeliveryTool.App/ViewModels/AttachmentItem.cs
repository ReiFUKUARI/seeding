using System.IO;

namespace MailDeliveryTool.App.ViewModels;

/// <summary>
/// メール作成画面（要件定義書7章）で選択した添付ファイル1件。
/// 実際のメール組み立て（フェーズ5⑥：送信エンジン）でこのFilePathを使ってMimeKitへ添付する。
/// </summary>
public sealed class AttachmentItem
{
    public string FilePath { get; }
    public string FileName { get; }
    public long SizeBytes { get; }

    public string SizeText => $"{SizeBytes / 1024.0 / 1024.0:0.0}MB";

    public AttachmentItem(string filePath, long sizeBytes)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        SizeBytes = sizeBytes;
    }
}

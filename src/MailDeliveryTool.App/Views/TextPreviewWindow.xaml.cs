using System.Windows;

namespace MailDeliveryTool.App.Views;

/// <summary>テキストを拡大表示するだけの汎用モーダル（メール作成画面のプレビュー拡大等で使う）。</summary>
public partial class TextPreviewWindow : Window
{
    public TextPreviewWindow(string title, string body)
    {
        InitializeComponent();
        Title = title;
        TitleTextBlock.Text = title;
        BodyTextBlock.Text = body;
    }
}

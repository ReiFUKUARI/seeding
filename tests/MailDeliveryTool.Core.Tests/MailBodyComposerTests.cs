using MailDeliveryTool.Core.Mail;
using MailDeliveryTool.Core.Models;
using Xunit;

namespace MailDeliveryTool.Core.Tests;

public sealed class MailBodyComposerTests
{
    [Fact]
    public void Substitute_3種類のタグを宛先の値に置換する()
    {
        var contact = new Contact { CompanyName = "株式会社A", ContactName = "山田 太郎", Email = "yamada@a.co.jp" };

        var result = MailBodyComposer.Substitute("#会社名#\n#担当者名# 様\n連絡先: #メールアドレス#", contact);

        Assert.Equal("株式会社A\n山田 太郎 様\n連絡先: yamada@a.co.jp", result);
    }

    [Fact]
    public void Substitute_タグが複数回出現しても全て置換される()
    {
        var contact = new Contact { CompanyName = "A社", ContactName = "B", Email = "b@a.jp" };

        var result = MailBodyComposer.Substitute("#会社名#と#会社名#", contact);

        Assert.Equal("A社とA社", result);
    }

    [Fact]
    public void Substitute_未知のタグはそのまま残る()
    {
        var contact = new Contact { CompanyName = "A社", ContactName = "B", Email = "b@a.jp" };

        var result = MailBodyComposer.Substitute("#電話番号#にご連絡ください", contact);

        Assert.Equal("#電話番号#にご連絡ください", result);
    }

    [Fact]
    public void AppendSignature_署名がnullなら本文はそのまま()
    {
        var result = MailBodyComposer.AppendSignature("本文", null);

        Assert.Equal("本文", result);
    }

    [Fact]
    public void AppendSignature_署名が空文字なら本文はそのまま()
    {
        var result = MailBodyComposer.AppendSignature("本文", string.Empty);

        Assert.Equal("本文", result);
    }

    [Fact]
    public void AppendSignature_署名がある場合は区切り線付きで末尾に追加する()
    {
        var result = MailBodyComposer.AppendSignature("本文", "山田 太郎\n株式会社A");

        Assert.Equal("本文\n\n-- \n山田 太郎\n株式会社A", result);
    }
}

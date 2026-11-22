using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Ganss.Xss;
using PreMailer.Net;

namespace Eden_Relics_BE.Services;

/// <summary>Company details printed in every email footer. Read from the site's footer content.</summary>
public record EmailFooterInfo(string CompanyName, string Address, string CompanyLine, string Tagline);

public record RenderedEmail(string Html, string Text);

/// <summary>
/// The Eden Relics email wrapper (header, body, footer) plus the clean-up every admin-written
/// email goes through: scripts and event handlers stripped, CSS inlined (email apps ignore most
/// stylesheets), and a plain-text version made from the same content.
/// </summary>
public static partial class EmailTemplate
{
    public const string DefaultCompanyName = "Eden Relics Ltd";

    private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

    private static HtmlSanitizer BuildSanitizer()
    {
        HtmlSanitizer s = new();
        s.AllowedTags.Add("style");
        s.AllowedTags.Add("center");
        s.AllowedAttributes.Add("class");
        s.AllowedAttributes.Add("align");
        s.AllowedAttributes.Add("valign");
        s.AllowedAttributes.Add("bgcolor");
        s.AllowedAttributes.Add("width");
        s.AllowedAttributes.Add("height");
        s.AllowedAttributes.Add("cellpadding");
        s.AllowedAttributes.Add("cellspacing");
        s.AllowedAttributes.Add("border");
        s.AllowedAttributes.Add("role");
        s.AllowedAttributes.Add("target");
        s.AllowedSchemes.Add("mailto");
        s.AllowedSchemes.Add("tel");
        s.AllowedAtRules.Add(AngleSharp.Css.Dom.CssRuleType.Media);
        return s;
    }

    /// <summary>
    /// Makes pasted or typed HTML safe to store and to show in the admin editor. A whole pasted
    /// document is reduced to its body, keeping any &lt;style&gt; blocks from the head.
    /// </summary>
    public static string SanitizeBody(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        string fragment = html;
        if (html.Contains("<body", StringComparison.OrdinalIgnoreCase) || html.Contains("<head", StringComparison.OrdinalIgnoreCase))
        {
            IDocument doc = new HtmlParser().ParseDocument(html);
            string styles = string.Concat(doc.QuerySelectorAll("head style").Select(e => e.OuterHtml));
            fragment = styles + (doc.Body?.InnerHtml ?? string.Empty);
        }

        return Sanitizer.Sanitize(fragment).Trim();
    }

    /// <summary>
    /// Wraps the body in the house template and inlines the CSS. Pass an unsubscribe URL for
    /// marketing email (it goes in the footer); transactional email passes null.
    /// </summary>
    public static RenderedEmail Render(
        string subject,
        string? previewText,
        string bodyHtml,
        EmailFooterInfo footer,
        string siteUrl,
        string? unsubscribeUrl)
    {
        string enc(string? v) => WebUtility.HtmlEncode(v ?? string.Empty);

        string preheader = string.IsNullOrWhiteSpace(previewText)
            ? string.Empty
            // The run of invisible characters stops inboxes pulling body text into the preview line.
            : $"""<div style="display:none;max-height:0;overflow:hidden;mso-hide:all;font-size:1px;line-height:1px;color:#F5F0E6;opacity:0;">{enc(previewText)}{string.Concat(Enumerable.Repeat("&#847;&zwnj;&nbsp;", 60))}</div>""";

        string addressLine = enc(footer.Address);
        string companyLine = string.IsNullOrWhiteSpace(footer.CompanyLine) ? string.Empty : $"{enc(footer.CompanyLine)}<br>";
        string unsubscribe = unsubscribeUrl is null
            ? string.Empty
            : $"""You're receiving this because you signed up to the Eden Relics newsletter.<br><a href="{enc(unsubscribeUrl)}" style="color:#5C3D1E;text-decoration:underline;">Unsubscribe</a> &middot; """;

        string html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="x-apple-disable-message-reformatting">
            <title>{{enc(subject)}}</title>
            <style>
              .er-body p { margin: 0 0 16px; line-height: 1.6; }
              .er-body a { color: #9B4A1E; }
              .er-body img { max-width: 100%; height: auto; border: 0; }
              .er-body h1, .er-body h2, .er-body h3 { font-family: Georgia, 'Times New Roman', serif; font-weight: normal; color: #2E1A0E; margin: 0 0 12px; line-height: 1.3; }
              .er-body h1 { font-size: 28px; }
              .er-body h2 { font-size: 22px; }
              .er-body h3 { font-size: 18px; }
              .er-body ul, .er-body ol { margin: 0 0 16px; padding-left: 22px; line-height: 1.6; }
              @media (max-width: 620px) {
                .er-container { width: 100% !important; }
                .er-pad { padding: 24px 18px !important; }
                .er-card { display: block !important; width: 100% !important; box-sizing: border-box; }
              }
            </style>
            </head>
            <body style="margin:0;padding:0;background:#F5F0E6;">
            {{preheader}}
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#F5F0E6;">
              <tr>
                <td align="center" style="padding:24px 12px;">
                  <table role="presentation" class="er-container" width="600" cellpadding="0" cellspacing="0" border="0" style="width:600px;max-width:600px;background:#FBF8F1;border:1px solid #EAE0CC;">
                    <tr>
                      <td align="center" style="padding:28px 24px 20px;border-bottom:1px solid #EAE0CC;">
                        <a href="{{enc(siteUrl)}}" style="font-family:Georgia,'Times New Roman',serif;font-size:28px;letter-spacing:4px;color:#2E1A0E;text-decoration:none;">EDEN RELICS</a>
                        <div style="font-family:Georgia,'Times New Roman',serif;font-size:14px;font-style:italic;color:#5C3D1E;margin-top:6px;">{{enc(footer.Tagline)}}</div>
                      </td>
                    </tr>
                    <tr>
                      <td class="er-pad er-body" style="padding:32px 36px;font-family:Georgia,'Times New Roman',serif;font-size:16px;line-height:1.6;color:#2E1A0E;">
            {{bodyHtml}}
                      </td>
                    </tr>
                    <tr>
                      <td align="center" style="padding:22px 36px 26px;border-top:1px solid #EAE0CC;font-family:Georgia,'Times New Roman',serif;font-size:12px;line-height:1.7;color:#5C3D1E;">
                        <strong>{{enc(footer.CompanyName)}}</strong><br>
                        {{addressLine}}<br>
                        {{companyLine}}
                        {{unsubscribe}}<a href="{{enc(siteUrl)}}" style="color:#5C3D1E;text-decoration:underline;">edenrelics.co.uk</a>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            </body>
            </html>
            """;

        string inlined = PreMailer.Net.PreMailer.MoveCssInline(html, removeStyleElements: false, stripIdAndClassAttributes: false).Html;

        StringBuilder text = new();
        text.AppendLine(ToPlainText(bodyHtml));
        text.AppendLine();
        text.AppendLine("--");
        text.AppendLine(footer.CompanyName);
        text.AppendLine(footer.Address);
        if (!string.IsNullOrWhiteSpace(footer.CompanyLine))
        {
            text.AppendLine(footer.CompanyLine);
        }
        if (unsubscribeUrl is not null)
        {
            text.AppendLine($"Unsubscribe: {unsubscribeUrl}");
        }
        text.Append(siteUrl);

        return new RenderedEmail(inlined, text.ToString());
    }

    /// <summary>Builds the footer details from the site content (footer address and company number).</summary>
    public static EmailFooterInfo FooterFrom(IReadOnlyDictionary<string, string> content)
    {
        string get(string key) => content.TryGetValue(key, out string? v) ? v ?? string.Empty : string.Empty;

        // The footer address starts with the company name in capitals; print the name once.
        List<string> lines = get("footer.contact.address")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !l.Equals(DefaultCompanyName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        string tagline = get("footer.tagline");
        return new EmailFooterInfo(
            DefaultCompanyName,
            string.Join(", ", lines),
            get("footer.company.line1"),
            string.IsNullOrWhiteSpace(tagline) ? "Carefully sourced & lovingly preserved vintage clothing." : tagline);
    }

    /// <summary>A readable plain-text version of an HTML body: paragraphs and line breaks kept, links as "text (url)".</summary>
    public static string ToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        IDocument doc = new HtmlParser().ParseDocument($"<body>{html}</body>");
        StringBuilder sb = new();
        Walk(doc.Body!, sb);
        string text = sb.ToString().Replace("\r", string.Empty);
        text = SpacesRegex().Replace(text, " ");
        text = string.Join('\n', text.Split('\n').Select(l => l.Trim()));
        text = BlankLinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    private static readonly HashSet<string> BlockTags =
        ["P", "DIV", "H1", "H2", "H3", "H4", "H5", "H6", "TR", "TABLE", "UL", "OL", "BLOCKQUOTE", "SECTION", "HEADER", "FOOTER", "CENTER"];

    private static void Walk(INode node, StringBuilder sb)
    {
        foreach (INode child in node.ChildNodes)
        {
            switch (child)
            {
                case IText t:
                    sb.Append(t.Data);
                    break;
                case IElement e when e.TagName is "STYLE" or "SCRIPT" or "HEAD" or "TITLE":
                    break;
                case IElement e when e.TagName == "BR":
                    sb.Append('\n');
                    break;
                case IElement e when e.TagName == "IMG":
                    string? alt = e.GetAttribute("alt");
                    if (!string.IsNullOrWhiteSpace(alt))
                    {
                        sb.Append(alt);
                    }
                    break;
                case IElement e when e.TagName == "LI":
                    sb.Append("\n- ");
                    Walk(e, sb);
                    break;
                case IElement e when e.TagName == "A":
                    int start = sb.Length;
                    Walk(e, sb);
                    string linkText = sb.ToString(start, sb.Length - start).Trim();
                    string? href = e.GetAttribute("href");
                    if (!string.IsNullOrWhiteSpace(href) && !href.StartsWith('#') && !linkText.Equals(href, StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append($" ({href})");
                    }
                    break;
                case IElement e when e.TagName == "TD" || e.TagName == "TH":
                    Walk(e, sb);
                    sb.Append('\n');
                    break;
                case IElement e when BlockTags.Contains(e.TagName):
                    sb.Append("\n\n");
                    Walk(e, sb);
                    sb.Append("\n\n");
                    break;
                case IElement e:
                    Walk(e, sb);
                    break;
            }
        }
    }

    [GeneratedRegex("[ \t ]+")]
    private static partial Regex SpacesRegex();

    [GeneratedRegex("\n{3,}")]
    private static partial Regex BlankLinesRegex();
}

using Microsoft.UI.Xaml;
using System;

namespace SupportBot.UI.ChatWindowKit.Models;
internal sealed record Message
{
    internal HorizontalAlignment MsgAlignment { get; set; } = HorizontalAlignment.Left;

    internal string? MsgText { get; set; }

    internal string MsgDateTime { get; set; } = DateTime.Now.ToString("hh:mm tt");
}

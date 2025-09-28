using Microsoft.Web.WebView2.Wpf;
using System;

namespace NativeBrowser
{
    public class TabItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public WebView2 WebView { get; set; }
        public System.Windows.Controls.Button TabButton { get; set; }
        public bool IsActive { get; set; }
        public string Id { get; set; }

        public TabItem()
        {
            Id = Guid.NewGuid().ToString();
            Title = "New Tab";
            Url = "about:blank";
            IsActive = false;
        }
    }
}
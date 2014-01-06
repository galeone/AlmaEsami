using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System.IO.IsolatedStorage;
using System.IO;
using System.Text.RegularExpressions;

namespace AlmaEsami
{
    public partial class InfoPage : PhoneApplicationPage
    {
        private string data;
        private LoadingHandler loadingHandler;

        public InfoPage()
        {
            InitializeComponent();
            loadingHandler = new LoadingHandler(this, new ProgressIndicator(), new System.Windows.Threading.DispatcherTimer());
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            loadingHandler.BeginLoading();
            IsolatedStorageFile file = IsolatedStorageFile.GetUserStoreForApplication();
            StreamReader sr = new StreamReader(new IsolatedStorageFileStream(plusPage.infoDataFile, FileMode.Open, file));
            data = sr.ReadToEnd();
            sr.Close();

            parseAndPopulate();
            loadingHandler.EndLoading();
        }

        private void parseAndPopulate()
        {
            foreach (Match m in Regex.Matches(data, @"<td[^>]+>(.+?)<\/td>\s*<td[^>]+>(.+?)<\/td>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                TextBlock label = new TextBlock();
                label.Text = HttpUtility.HtmlDecode(m.Groups[1].Value.Trim()).ToLower();
                label.FontWeight = FontWeights.Bold;

                TextBlock value = new TextBlock();
                value.Text = '\t' + HttpUtility.HtmlDecode(m.Groups[2].Value.Trim()).ToLower();
                value.TextWrapping = TextWrapping.Wrap;

                stack.Children.Add(label);
                stack.Children.Add(value);
            }
        }

    }
}
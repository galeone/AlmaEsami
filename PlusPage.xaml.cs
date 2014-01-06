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
using System.Text.RegularExpressions;
using System.IO;

namespace AlmaEsami
{
    public partial class plusPage : PhoneApplicationPage
    {
        private IsolatedStorageSettings userSettings = IsolatedStorageSettings.ApplicationSettings;
        private enum Type { Avaiable, Booked };
        private string baseUrl = "https://almaesami.unibo.it/";
        private LoadingHandler loadingHandler;
        private string plusData;
        private byte[] postContent;

        public const string infoDataFile = "infoData.html";

        public plusPage()
        {
            InitializeComponent();
            loadingHandler = new LoadingHandler(this, new ProgressIndicator(), new System.Windows.Threading.DispatcherTimer(), "Errore in Almaesami:\n");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            loadingHandler.BeginLoading();

            IsolatedStorageFile file = IsolatedStorageFile.GetUserStoreForApplication();

            StreamReader sr = new StreamReader(new IsolatedStorageFileStream(PanoramaAfterLogin.dataFileName, FileMode.Open, file));
            plusData = sr.ReadToEnd();
            sr.Close();

            sr = new StreamReader(new IsolatedStorageFileStream(PanoramaAfterLogin.tileFileName, FileMode.Open, file));
            txtTitle.Text = ((string)sr.ReadToEnd()).ToLower();
            sr.Close();

            parseAndPopulate();

            loadingHandler.EndLoading();
        }

        private void parseAndPopulate()
        {
            //match 1 prenotate, 2 disponibili - Matches parte da 1
            MatchCollection righe = Regex.Matches(plusData, @"<table>(.+?)<\/table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Regex regex = new Regex(@"<td[^>]*>(.*?)<\/td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            switch (righe.Count)
            {
                case 1: //determino se è la tabella delle disponibili o delle prenotate
                    MatchCollection matches = regex.Matches(righe[0].Value);
                    populate(matches, matches[0].Groups[1].Value.Contains("dispon") ? Type.Avaiable : Type.Booked);
                    break;

                case 2:
                    populate(regex.Matches(righe[0].Value), Type.Booked);
                    populate(regex.Matches(righe[1].Value), Type.Avaiable);
                    break;
            }
        }

        private void populate(MatchCollection matches, Type type)
        {
            StackPanel stack = type == Type.Booked ? stackPrenotate : stackDisponibili;
            //Matches has always at least 1 element (the section title, that I can skip starting count from 1)

            for (int i = 1; i < matches.Count; ++i)
                if (0 != (i % 2)) //odd row are for information
                    appendTextTo(stack, matches[i].Groups[1].Value);
                else
                    //Getting button (if present) and adding event handler
                    foreach (Match m in Regex.Matches(matches[i].Groups[1].Value, @"<a[^h]+href=""(.+?)""[^>]*>(.+?)<\/a>"))
                        appendButtonTo(stack, m.Groups);
        }

        private void appendTextTo(StackPanel stack, string content)
        {
            string[] values = content.Split(new string[] { "<br />" }, System.StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();

            foreach (string val in values)
            {
                TextBlock txt = new TextBlock();
                txt.Text = '\t' + val.Replace("<em>", "").Replace("</em>", "");
                txt.FontSize = 22;

                stack.Children.Add(txt);
            }
            stack.Children.Add(new TextBlock());
        }

        private void appendButtonTo(StackPanel stack, GroupCollection g)
        {
            string value = g[2].Value.ToLower().Trim();
            bool isStampa = value == "stampa";

            Button txt = new Button();
            txt.Content = isStampa ? "info" : value;
            txt.Tag = g[1].Value; //url
            txt.Click += isStampa ? info_Click : value == "ritira" ? (RoutedEventHandler)ritira_Click : prenota_Click;
            stack.Children.Add(txt);
        }

        private void ritira_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBoxResult.OK == MessageBox.Show("Vuoi davvero ritirarti dalla prova?", "info", MessageBoxButton.OKCancel))
            {
                loadingHandler.BeginLoading((Button)sender);
                Dispatcher.BeginInvoke(() =>
                {
                    HttpWebRequest request = initRequest(sender);
                    request.BeginGetResponse(new AsyncCallback((result) =>
                    {
                        Dispatcher.BeginInvoke(() =>
                            {
                                Button _sender = (Button)sender;
                                loadingHandler.BeginLoading(_sender);
                                HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(result);
                                loadingHandler.EndLoading(_sender);
                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                    loadingHandler.EndLoading(_sender);
                                    MessageBox.Show("Ritirato!");
                                    NavigationService.Navigate(new Uri("/PanoramaAfterLogin.xaml", UriKind.Relative));
                                }
                                else
                                {
                                    loadingHandler.EndLoading(_sender);
                                    MessageBox.Show(loadingHandler.ErrorHandler("Prova più tardi", _sender));
                                }
                            });
                    }), null);
                });
            }
        }

        private void prenota_Click(object sender, RoutedEventArgs e)
        {
            HttpWebRequest wizardRequest = initRequest(sender); //GET
            loadingHandler.BeginLoading((Button)sender);
            wizardRequest.BeginGetResponse(new AsyncCallback((result) =>
                    {
                        Dispatcher.BeginInvoke(() =>
                            {
                                Button _sender = (Button)sender;
                                loadingHandler.BeginLoading(_sender);
                                HttpWebResponse response = (HttpWebResponse)wizardRequest.EndGetResponse(result);
                                System.IO.StreamReader responseStream = new System.IO.StreamReader(response.GetResponseStream());
                                string wizard = responseStream.ReadToEnd();
                                responseStream.Close();

                                Regex pattern = new Regex(@"(?i)<\s?input.+?>");
                                Dictionary<string, string> postData = new Dictionary<string, string>();
                                foreach (Match m in pattern.Matches(wizard))
                                {
                                    Regex name = new Regex(@"name=""([^""]+)""", RegexOptions.IgnoreCase),
                                        value = new Regex(@"value=""([^""]+|)?""", RegexOptions.IgnoreCase);

                                    MatchCollection nameCollection = name.Matches(m.Groups[0].Value),
                                        valueCollection = value.Matches(m.Groups[0].Value);

                                    if (valueCollection.Count == 1 && nameCollection.Count == 1 && valueCollection[0].Groups[1].Value.Trim().ToLower() != "indietro") //altrimenti indietro manda indietro
                                        postData.Add(nameCollection[0].Groups[1].Value, valueCollection[0].Groups[1].Value);
                                }

                                pattern = new Regex(@"<\s?form.+?action=""(.+?)""", RegexOptions.IgnoreCase);
                                Match match = pattern.Match(wizard);
                                string postURL = match.Groups[1].Value;

                                //POST request
                                _sender.Tag = postURL;
                                HttpWebRequest bookRequest = initRequest(_sender, postData);
                                bookRequest.Headers["Referer"] = baseUrl + postURL; //il referrer in realtà son me stesso
                                bookRequest.AllowAutoRedirect = false;
                                bookRequest.BeginGetRequestStream(new AsyncCallback((result2) =>
                                    {
                                        Dispatcher.BeginInvoke(() =>
                                        {
                                            System.IO.Stream stream = bookRequest.EndGetRequestStream(result2);

                                            stream.Write(postContent, 0, (int)bookRequest.ContentLength);
                                            stream.Flush();
                                            stream.Close();
                                            bookRequest.BeginGetResponse((endResponse) =>
                                            {
                                                Dispatcher.BeginInvoke(() =>
                                                {
                                                    HttpWebResponse res = (HttpWebResponse)bookRequest.EndGetResponse(endResponse);
                                                    loadingHandler.EndLoading(_sender);
                                                    if (res.StatusCode == HttpStatusCode.Redirect)
                                                    {
                                                        MessageBox.Show("Prenotato con successo!");
                                                    }
                                                    else
                                                    {
                                                        loadingHandler.ErrorHandler("Riprova più tardi",_sender);
                                                    }
                                                });
                                            }, null);
                                            NavigationService.Navigate(new Uri("/PanoramaAfterLogin.xaml", UriKind.Relative));

                                        });
                                    }), null);
                            });
                    }), null);
        }

        private void info_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
                {
                    HttpWebRequest request = initRequest(sender);

                    request.BeginGetResponse(new AsyncCallback((result) =>
                    {
                        Dispatcher.BeginInvoke(() =>
                            {
                                Button _sender = (Button)sender;
                                loadingHandler.BeginLoading(_sender);
                                HttpWebResponse response = null;
                                try
                                {
                                    response = (HttpWebResponse)request.EndGetResponse(result);
                                    System.IO.StreamReader responseStream = new System.IO.StreamReader(response.GetResponseStream());
                                    string page = responseStream.ReadToEnd();
                                    responseStream.Close();

                                    IsolatedStorageFile file = IsolatedStorageFile.GetUserStoreForApplication();

                                    StreamWriter sw = new StreamWriter(file.OpenFile(plusPage.infoDataFile, FileMode.Create));
                                    sw.Write(Regex.Match(page, @"<div\s+?class=""dati"">\s*<table>(.+?)<\/table>", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value);
                                    sw.Close();

                                    loadingHandler.EndLoading(_sender);
                                    NavigationService.Navigate(new Uri("/InfoPage.xaml", UriKind.Relative));
                                }
                                catch (WebException)
                                {
                                    MessageBox.Show(loadingHandler.ErrorHandler("Controlla la tua connessione internet"));
                                    loadingHandler.EndLoading(_sender);
                                }

                            });
                    }), null);
                });
        }

        private HttpWebRequest initRequest(Object sender, Dictionary<string, string> postData = null)
        {
            //making request
            string destinationUrl = (string)((Button)sender).Tag;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(baseUrl + destinationUrl);

            if (postData != null)
            {
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";

                postContent = System.Text.Encoding.UTF8.GetBytes(string.Join("&", postData.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeUriString(x.Value))));
                request.ContentLength = postContent.Length;
            }

            request.Headers["Cookie"] = (string)userSettings["cookie"];
            request.Headers["Connection"] = "keep-alive";
            request.Headers["Host"] = "almaesami.unibo.it";
            request.Headers["Origin"] = baseUrl;

            return request;
        }
    }
}
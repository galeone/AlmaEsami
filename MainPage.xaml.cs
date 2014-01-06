using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Text.RegularExpressions;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using AlmaEsami.Resources;
using System.Windows.Threading;
using System.IO.IsolatedStorage;

namespace AlmaEsami
{
    public partial class MainPage : PhoneApplicationPage
    {
        private HttpWebRequest mainRequest, loginRequest;
        private String loginFormURL, loginPageURL;
        private IDictionary<string, string> postData;
        private byte[] postContent;
        private Cookie JSESSIONID = null;
        private int loginStep;
        private IsolatedStorageSettings userSettings = IsolatedStorageSettings.ApplicationSettings;
        private LoadingHandler loadingHandler;

        private const string baseURL = "https://cas.unibo.it/";

        // Costruttore
        public MainPage()
        {
            InitializeComponent();

            //progress bar handler
            loadingHandler = new LoadingHandler(this, new ProgressIndicator(), new DispatcherTimer(), "Errore in Almaesami\n");
            loadingHandler.BeginLoading(btnLogin);

            //getting userData if present
            try
            {
                boxUser.Text = (string)userSettings["username"];
                boxPass.Password = (string)userSettings["password"];
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                //nothing
            }

            //postData -> url and session
            postData = new Dictionary<string, string>();
            loginFormURL = baseURL + "cas/login?service=" + Uri.EscapeUriString("https://almaesami.unibo.it/almaesami/studenti/j_acegi_cas_security_check"); //crea l'url corretta, fai la richiesta, ottieni la pagina
            try
            {
                mainRequest = (HttpWebRequest)WebRequest.Create(loginFormURL);
            }
            catch (WebException exp)
            {
                if (exp.Status == WebExceptionStatus.RequestCanceled) //resuming
                    mainRequest = (HttpWebRequest)WebRequest.Create(loginFormURL);
                else
                {
                    MessageBox.Show(loadingHandler.ErrorHandler("Controlla la tua connessione internet", btnLogin));
                    return;
                }
            }

            mainRequest.CookieContainer = null; //NO COOKIE Container
            mainRequest.AllowAutoRedirect = false;

            try
            {
                mainRequest.BeginGetResponse(new AsyncCallback(GetPostData), null);
            }
            catch (Exception)
            {
                MessageBox.Show(loadingHandler.ErrorHandler("Controlla la tua connessione internet", btnLogin));
                return;
            }
        }

        //BEGIN FORM REQUESTS
        private void InitPostRequest(ref HttpWebRequest req)
        {
            req.CookieContainer = null;
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.Headers["Referer"] = loginFormURL;

            postData["username"] = boxUser.Text.ToString();
            postData["password"] = boxPass.Password.ToString();

            postContent = System.Text.Encoding.UTF8.GetBytes(string.Join("&", postData.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeUriString(x.Value))));
            req.ContentLength = postContent.Length;
        }

        private void GetPostData(IAsyncResult result)
        {
            this.Dispatcher.BeginInvoke(() =>
            {
                HttpWebResponse response = null;
                try
                {
                    response = (HttpWebResponse)mainRequest.EndGetResponse(result);
                }
                catch (WebException)
                {
                    MessageBox.Show(loadingHandler.ErrorHandler("Controlla la tua connessione internet", btnLogin));
                    return;
                }

                string html = (new System.IO.StreamReader(response.GetResponseStream())).ReadToEnd();
                Regex pattern = new Regex(@"<\s?form.+?action=""(.+?)""", RegexOptions.IgnoreCase);

                Match match = pattern.Match(html);

                loginPageURL = baseURL + match.Groups[1].Value;
                JSESSIONID = new Cookie("JSESSIONID", Regex.Match(match.Groups[1].Value, @"jsessionid=([A-Z0-9]+\.cas\-joss[0-9])", RegexOptions.IgnoreCase).Groups[1].Value);

                pattern = new Regex(@"(?i)<\s?input.+?>");

                foreach (Match m in pattern.Matches(html))
                {
                    Regex name = new Regex(@"name=""([^""]+)""", RegexOptions.IgnoreCase),
                        value = new Regex(@"value=""([^""]+|)?""", RegexOptions.IgnoreCase);

                    MatchCollection nameCollection = name.Matches(m.Groups[0].Value),
                        valueCollection = value.Matches(m.Groups[0].Value);

                    if (valueCollection.Count == 1 && nameCollection.Count == 1)
                    {
                        postData.Add(nameCollection[0].Groups[1].Value, valueCollection[0].Groups[1].Value);
                    }
                }
                if (!string.IsNullOrEmpty(boxUser.Text) && !string.IsNullOrEmpty(boxPass.Password))
                    btnLogin_Click(btnLogin, new RoutedEventArgs());
                else
                    loadingHandler.EndLoading(btnLogin);
            });
        }

        //BEGIN LOGIN METHODS

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (postData.Count > 1 && loginPageURL != null && loginPageURL.Length > 0 && JSESSIONID != null) //got valid login url and form fields?
                {
                    if (boxPass.Password.Length == 0 || boxUser.Text.Trim().Length == 0)
                    {
                        MessageBox.Show(loadingHandler.ErrorHandler("Inserisci sia username che password", btnLogin));
                        return;
                    }

                    loadingHandler.BeginLoading(btnLogin);

                    try
                    {
                        loginRequest = (HttpWebRequest)WebRequest.Create(loginPageURL);
                    }
                    catch (WebException exp)
                    {
                        if (exp.Status == WebExceptionStatus.RequestCanceled) //resuming
                            loginRequest = (HttpWebRequest)WebRequest.Create(loginPageURL);
                        else
                        {
                            MessageBox.Show(loadingHandler.ErrorHandler("Riavvia l'applicazione.", btnLogin));
                            return;
                        }
                    }

                    //headers
                    loginRequest.Headers["Cookie"] = JSESSIONID.Name + "=" + JSESSIONID.Value;
                    loginRequest.Headers["Cache-Control"] = "max-age=0";
                    loginRequest.Headers["Connection"] = "keep-alive";
                    loginRequest.Headers["Host"] = "cas.unibo.it";
                    loginRequest.Headers["Origin"] = "https://cas.unibo.it";

                    InitPostRequest(ref loginRequest);

                    loginRequest.AllowAutoRedirect = false;
                    loginStep = 1;
                    loginRequest.BeginGetRequestStream(new AsyncCallback(Authenticate), loginStep);
                }
                else
                {
                    MessageBox.Show(loadingHandler.ErrorHandler("Errore in almaesami, riprova più tardi", btnLogin));
                    return;
                }
            });
        }

        private void Authenticate(IAsyncResult result)
        {
            Dispatcher.BeginInvoke(() =>
            {
                HttpWebResponse response = null;
                switch ((int)result.AsyncState)
                {
                    case 1:
                        //richiesta post cas.unibo.it/cas
                        BeginLogin(result);
                        break;
                    //step dal 2 in poi sono su almaesami
                    case 2:
                        //cambio di dominio, si va su almaesami. Nuovo cookie
                        try
                        {
                            response = (HttpWebResponse)loginRequest.EndGetResponse(result);
                        }
                        catch (WebException e)
                        {
                            if (e.Status == WebExceptionStatus.RequestCanceled) //resuming
                                response = (HttpWebResponse)loginRequest.EndGetResponse(result);
                            else
                            {
                                MessageBox.Show(loadingHandler.ErrorHandler("Riavvia l'applicazione.", btnLogin));
                                return;
                            }
                        }

                        if (response.StatusCode == HttpStatusCode.Redirect)
                        {
                            //Invia 2 cookie il maledetto, quindi va preso il secondo.
                            JSESSIONID = new Cookie("JSESSIONID", response.Headers["Set-Cookie"].Split(',')[1].Split(';')[0].Substring(13), "/almaesami", "unibo.it");
                            NextLoginStep(response);
                        }
                        else
                        {
                            MessageBox.Show(loadingHandler.ErrorHandler("Riavvia l'applicazione.", btnLogin));
                            return;
                        }
                        break;

                    case 3:
                    case 4:
                        try
                        {
                            response = (HttpWebResponse)loginRequest.EndGetResponse(result);
                            if (response.StatusCode == HttpStatusCode.Redirect)
                            {
                                NextLoginStep(response);
                            }
                            else
                            {
                                MessageBox.Show(loadingHandler.ErrorHandler("Riavvia l'applicazione.", btnLogin));
                                return;
                            }
                        }
                        catch (WebException)
                        {
                            MessageBox.Show(loadingHandler.ErrorHandler("Riavvia l'applicazione.", btnLogin));
                            return;
                        }
                        break;

                    case 5:
                        EndLogin(result);
                        break;
                    default:
                        MessageBox.Show(loadingHandler.ErrorHandler("Errore inaspettato\nRiavvia l'applicazione.", btnLogin));
                        break;
                }
            });
        }

        private void NextLoginStep(HttpWebResponse response) //su almaesami
        {
            Dispatcher.BeginInvoke(() =>
            {
                response.Close();
                try
                {
                    loginRequest = (HttpWebRequest)WebRequest.Create(response.Headers["Location"]);
                }
                catch (WebException e)
                {
                    if (e.Status == WebExceptionStatus.RequestCanceled) //resuming
                        loginRequest = (HttpWebRequest)WebRequest.Create(response.Headers["Location"]);
                    else
                    {
                        MessageBox.Show(loadingHandler.ErrorHandler("Riavvia l'applicazione.", btnLogin));
                        return;
                    }
                }

                loginRequest.Headers["Cache-Control"] = "max-age=0";
                loginRequest.Headers["Connection"] = "keep-alive";
                loginRequest.Headers["Host"] = "almaesami.unibo.it";

                loginRequest.Headers["Referer"] = loginPageURL;
                loginPageURL = response.Headers["Location"]; //for next step (previous line)
                loginRequest.Headers["Cookie"] = JSESSIONID.Name + "=" + JSESSIONID.Value;
                loginRequest.AllowAutoRedirect = false;

                loginRequest.BeginGetResponse(new AsyncCallback(Authenticate), ++loginStep);
            });
        }

        //step 1
        private void BeginLogin(IAsyncResult result)
        {
            Dispatcher.BeginInvoke(() =>
            {
                System.IO.Stream stream = loginRequest.EndGetRequestStream(result);

                    stream.Write(postContent, 0, (int)loginRequest.ContentLength);
                    stream.Flush();
                    stream.Close();
                    loginRequest.BeginGetResponse((endResponse) =>
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            HttpWebResponse response = (HttpWebResponse)loginRequest.EndGetResponse(endResponse);
                            if (response.StatusCode == HttpStatusCode.Redirect)
                            {
                                NextLoginStep(response);
                            }
                            else
                            {
                                loadingHandler.EndLoading(btnLogin);
                                System.IO.StreamReader responseStream = new System.IO.StreamReader(response.GetResponseStream());
                                string html = responseStream.ReadToEnd();
                                responseStream.Close();
                                response.Close();
                                MessageBox.Show(loadingHandler.ErrorHandler(Regex.Match(html, @"(?im)<div.+?class=""errors""(:?.+?)?>(.+?)<\/div>").Groups[2].Value,btnLogin));
                                //if here, probably username or password are wrong, so clear localstorage
                                userSettings.Clear();
                            }
                        });
                    }, null);
            });
        }

        //step 5
        private void EndLogin(IAsyncResult result)
        {
            Dispatcher.BeginInvoke(() =>
            {
                HttpWebResponse response = (HttpWebResponse)loginRequest.EndGetResponse(result);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    System.IO.StreamReader responseStream = new System.IO.StreamReader(response.GetResponseStream());
                    string marksPage = responseStream.ReadToEnd();
                    responseStream.Close();
                    response.Close();
                    //if here, login success and marksPage if full, so lets go set username and passowrd and than navigate over
                    userSettings["username"] = boxUser.Text.ToString();
                    userSettings["password"] = boxPass.Password.ToString();
                    //saving to isolatedstorage marksPath
                    IsolatedStorageFile store = IsolatedStorageFile.GetUserStoreForApplication();
                    try
                    {
                        store.CreateFile("marksPage.html");
                        System.IO.StreamWriter sw = new System.IO.StreamWriter("marksPage.html");
                        sw.Write(marksPage);
                        sw.Close();
                    }
                    catch
                    {
                        MessageBox.Show(loadingHandler.ErrorHandler("Controlla d'aver abbastanza spazio sul tuo Windows Phone", btnLogin));
                        return;
                    }
                    btnLogin.Content = "OK!";
                    userSettings["cookie"] = JSESSIONID.Name + "=" + JSESSIONID.Value; //for panoramaafterlogin requests
                    NavigationService.Navigate(new Uri("/PanoramaAfterLogin.xaml", UriKind.Relative));
                }
                else
                {
                    MessageBox.Show(loadingHandler.ErrorHandler("Il server ha risposto con errore " + response.StatusCode, btnLogin));
                    loadingHandler.EndLoading(btnLogin);
                }
            });
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.IO;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System.IO.IsolatedStorage;

namespace AlmaEsami
{
    public partial class PanoramaAfterLogin : PhoneApplicationPage
    {
        private string marksPage;
        private bool loaded, runningRequest = false;
        private IsolatedStorageSettings userSettings = IsolatedStorageSettings.ApplicationSettings;
        private LinkedList<Dictionary<string, string>> records;
        private Dictionary<string, int> cfu;
        private float avg;
        private int fontSize = 22; //default

        private LoadingHandler loadingHandler;

        public const string dataFileName = "plusData.html";
        public const string tileFileName = "plusTitle.txt";


        public PanoramaAfterLogin()
        {
            InitializeComponent();

            //data needs
            records = new LinkedList<Dictionary<string, string>>();

            //progress bar setup
            loadingHandler = new LoadingHandler(this, new ProgressIndicator(), new DispatcherTimer(), "Errore in Almaesami\n");

            //begin loading
            loadingHandler.BeginLoading();
            loaded = false;

            try
            {
                StreamReader sr = new StreamReader("marksPage.html");
                marksPage = sr.ReadToEnd();
                sr.Close();
            }
            catch
            {
                MessageBox.Show(loadingHandler.ErrorHandler("Errore nella lettura del database. Prova più tardi"));
                return;
            }

            //table values
            getMarksAndInfos();
            //populating view and calculating avg
            cfu = new Dictionary<string, int>();
            int cfuSum = 0, validMarks = 0, marksSum = 0, numId = 0, esFuturi = 0, 
                creditiIdoneita = 0, respintiVerbalizzati = 0, esitiNegativiDaVerbalizzare = 0, esitiPositiviDaVerbalizzare = 0,
                numeroLodi = 0;
            avg = 0;

            foreach (Dictionary<string, string> node in records)
            {
                int crediti = 0;

                try
                {
                    crediti = int.Parse(node["cfu"].Trim()); //esiste sempre

                    node["voto"] = Regex.Replace(node["stato"], @"^Verbalizzato:\s", "", RegexOptions.IgnoreCase).Trim();

                    Regex lode = new Regex(@"30\s",RegexOptions.IgnoreCase);

                    int voto = lode.IsMatch(node["voto"]) ? 30 : int.Parse(node["voto"]); // se c'è uno spazio dopo il 30 significa che c'è scritto "30 e lode"

                    if (lode.IsMatch(node["voto"]))
                    {
                        node["voto"] = "30L";
                        ++numeroLodi;
                    }

                    cfu[node["materia"]] = crediti;
                    marksSum += voto;
                    cfuSum += crediti;
                    avg += voto * crediti;
                    ++validMarks;
                }
                catch
                {
                    if (node["voto"].ToLower().Trim() == "id")
                    {
                        node["voto"] = "ID";
                        ++numId;
                        creditiIdoneita += crediti;
                    }
                    else if (node["voto"].ToLower().Trim() == "re")
                    {
                        node["voto"] = "RE";
                        ++respintiVerbalizzati;
                    }
                    else if (Regex.Replace(node["voto"], @"<|>|class|""|colonna|\/|=|tr|td", "", RegexOptions.IgnoreCase).Trim() == String.Empty) // (Like english placement test -> can't prenote and 0 cfu)
                        node["voto"] = "/";
                    else if (Regex.IsMatch(node["voto"], @"\([^0-9]+\)")) //esito ancora da verbalizzare (insuff, respinto)
                    {
                        node["voto"] = Regex.Match(node["voto"], @"\(([^0-9]+)\)").Groups[1].Value;
                        ++esitiNegativiDaVerbalizzare;
                    }
                    else if (Regex.IsMatch(node["voto"], @"\([0-9]+\)")) // ^ (voto)
                    {
                        node["voto"] = Regex.Match(node["voto"], @"\(([0-9]+)\)").Groups[1].Value;
                        ++esitiPositiviDaVerbalizzare;
                    }
                    else
                    {
                        node["voto"] = "+";
                        ++esFuturi;
                    }
                }
            }

            populateMarks();
            populateStats(cfuSum, marksSum, validMarks, numId, esFuturi, creditiIdoneita,respintiVerbalizzati,esitiNegativiDaVerbalizzare,esitiPositiviDaVerbalizzare, numeroLodi);

            loadingHandler.EndLoading();
        }

        void populateStats(int cfuSum, int marksSum, int validMarks, int numId, int futureExams, int creditiIdoneita, int respintiVerbalizzati, int esitiNegativiDaVerbalizzare, int esitiPositiviDaVerbalizzare, int numeroLodi)
        {
            txtMediaPesata.Text += Math.Round(avg /= cfuSum, 2);

            txtMediaAritmetica.Text += Math.Round((float)marksSum / validMarks, 2);

            double votoBaseLaurea = Math.Round(avg * 11 / 3, 2), votoArrotondato = Math.Round(votoBaseLaurea, 0);
            txtBaseLaurea.Text += (votoBaseLaurea == votoArrotondato ? votoArrotondato.ToString() : votoBaseLaurea + "  →  " + votoArrotondato);

            txtNumeroCrediti.Text += cfuSum + creditiIdoneita;;

            txtNumeroIdoneita.Text += numId;

            txtEsSostenuti.Text += validMarks;

            txtEsDaSostenere.Text += futureExams;

            txtNumeroLodi.Text += numeroLodi;

            txtPositiviDaVerbalizzare.Text += esitiPositiviDaVerbalizzare;

            txtNegativiDaVerbalizzare.Text += esitiNegativiDaVerbalizzare;

            txtRespintiVerbalizzati.Text += respintiVerbalizzati;
        }

        void populateMarks()
        {
            //sort by name
            records = new LinkedList<Dictionary<string, string>>(records.OrderBy(node => Regex.Replace(node["materia"].Trim(), @"^(:?[a-z]+)?[0-9]+\s*\-", "", RegexOptions.IgnoreCase)).ToList());

            int rowPosition = 0; //grid

            //create columns [2, not into foreach loop]
            ColumnDefinition action = new ColumnDefinition();
            action.Width = GridLength.Auto;

            ColumnDefinition subject = new ColumnDefinition();
            action.Width = GridLength.Auto;
            //add to grid column definition
            gridExams.ColumnDefinitions.Add(action);
            gridExams.ColumnDefinitions.Add(subject);

            foreach (Dictionary<string, string> node in records)
            {
                //create row [1 foreach subject]
                RowDefinition row = new RowDefinition();
                row.Height = new GridLength(fontSize + 40);

                //add to grid row definition
                gridExams.RowDefinitions.Add(row);
                TextBlock txtAction = new TextBlock(), txtSubject = new TextBlock();

                txtAction.Text = "  " + node["voto"] + "  ";
                txtAction.TextAlignment = TextAlignment.Center;
                txtAction.VerticalAlignment = System.Windows.VerticalAlignment.Center;


                txtSubject.Text = Regex.Replace(node["materia"].Trim(), @"^(:?[a-z]+)?[0-9]+\s*\-", "", RegexOptions.IgnoreCase);
                txtSubject.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                txtSubject.TextTrimming = TextTrimming.WordEllipsis;

                txtAction.FontSize = txtSubject.FontSize = fontSize;
                txtAction.FontWeight = FontWeights.Bold;

                //set txtAction and txtSubject as member of a Grid
                Grid.SetRow(txtAction, rowPosition);
                Grid.SetRow(txtSubject, rowPosition);
                ++rowPosition;
                Grid.SetColumn(txtAction, 0);
                Grid.SetColumn(txtSubject, 1);

                //append to grid
                gridExams.Children.Add(txtAction);
                gridExams.Children.Add(txtSubject);

                if (node["voto"] == "+" || Regex.IsMatch(node["voto"], @"\([0-9][0-9]?\)")) //add handler
                {
                    EventHandler<System.Windows.Input.GestureEventArgs> handler = new EventHandler<System.Windows.Input.GestureEventArgs>((sender, args) =>
                    {
                        this.Dispatcher.BeginInvoke(() =>
                        {
                            if (!runningRequest)
                            {
                                runningRequest = true;
                                loadingHandler.BeginLoading();
                                HttpWebRequest tapRequest = (HttpWebRequest)WebRequest.Create("https://almaesami.unibo.it/almaesami/studenti/attivitaFormativaPiano-list.htm?execution=e1s1&_eventId=toggle&idx=" + node["number"]);
                                tapRequest.Headers["Cookie"] = (string)userSettings["cookie"];
                                tapRequest.Headers["Connection"] = "keep-alive";
                                tapRequest.Headers["Host"] = "almaesami.unibo.it";
                                tapRequest.Headers["Referer"] = "https://almaesami.unibo.it/almaesami/studenti/attivitaFormativaPiano-list.htm?execution=e1s1";
                                tapRequest.BeginGetResponse((result) =>
                                {
                                    this.Dispatcher.BeginInvoke(() =>
                                    {
                                        HttpWebResponse response = null;
                                        try
                                        {
                                            response = (HttpWebResponse)tapRequest.EndGetResponse(result);
                                            System.IO.StreamReader responseStream = new System.IO.StreamReader(response.GetResponseStream());
                                            string marksPage = responseStream.ReadToEnd();
                                            responseStream.Close();
                                            response.Close();

                                            //the differences between the two pages is the new text
                                            string[] newPage = marksPage.Split('\n').ToArray<string>();
                                            string[] oldPage = this.marksPage.Split('\n').ToArray<string>();

                                            List<string> difference = new List<string>();
                                            for (int i = 0; i < oldPage.Length; ++i)
                                                if (!oldPage[i].Equals(newPage[i]))
                                                    difference.Add(newPage[i]);

                                            for (int i = oldPage.Length; i < newPage.Length; ++i)
                                                difference.Add(newPage[i]);

                                            string newText = string.Join("", difference.Select(x => x.Trim()));

                                            try
                                            {
                                                IsolatedStorageFile file = IsolatedStorageFile.GetUserStoreForApplication();
                                                StreamWriter sw = new StreamWriter(file.OpenFile(PanoramaAfterLogin.dataFileName, FileMode.Create));
                                                sw.Write(newText);
                                                sw.Close();

                                                sw = new StreamWriter(file.OpenFile(PanoramaAfterLogin.tileFileName, FileMode.Create));
                                                sw.Write(txtSubject.Text);
                                                sw.Close();
                                            }
                                            catch
                                            {
                                                loadingHandler.ErrorHandler("Controlla di avere abbastanza spazio sul telefono!");
                                                loadingHandler.EndLoading();
                                                runningRequest = false;
                                                return;
                                            }

                                            loadingHandler.EndLoading();
                                            runningRequest = false;

                                            NavigationService.Navigate(new Uri("/plusPage.xaml", UriKind.Relative));
                                        }
                                        catch (WebException)
                                        {
                                            MessageBox.Show(loadingHandler.ErrorHandler("Controlla la tua connessione internet"));
                                            loadingHandler.EndLoading();
                                            return;
                                        }

                                    });
                                }, null);
                            }
                        });
                    });

                    //handler to both element in row
                    txtAction.Tap += handler;
                    txtSubject.Tap += handler;
                }
            }
        }

        private string getFieldName(int index)
        {
            switch (index)
            {
                case 1:
                    return "+";
                case 2:
                    return "anno";
                case 3:
                    return "materia";
                case 4:
                    return "cds";
                case 5:
                    return "cfu";
                case 6:
                    return "stato";
                case 7:
                    return "prenota";
                default:
                    return "errore";
            }
        }

        private void getMarksAndInfos()
        {
            MatchCollection matches = Regex.Matches(marksPage, @"<\s?tr[^>]+class=""riga[0-9]+"">(.+?)<\/tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            //numero progressivo che identifica la lista [necessario per il menù a tendina per la prenotazione degli esami], parte da 0
            int examNumber = 0;

            //ogni match è una riga
            foreach (Match riga in matches)
            {
                MatchCollection colonne = Regex.Matches(riga.Groups[1].Value, @"<\s?td[^>]+class=""colonna""(?:[^>]+)?>(.+?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                Dictionary<string, string> row = new Dictionary<string, string>(); //per riga
                //per ogni campo
                int k = 1;
                foreach (Match campo in colonne)
                    row.Add(getFieldName(k++), campo.Groups[1].Value);

                //numero progressivo
                row.Add("number", examNumber.ToString());
                ++examNumber;

                records.AddFirst(row);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (!loaded) //first view
            {
                NavigationService.RemoveBackEntry(); //back will close app
                loaded = true;
            }
        }
    }
}
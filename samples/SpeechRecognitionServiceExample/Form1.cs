using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using System.Diagnostics;
using System.Windows.Threading;
using System.IO;
using System.Windows.Forms.DataVisualization.Charting;

using Microsoft.CognitiveServices.SpeechRecognition;

namespace SpeechToTextWPFSample
{
    public partial class Form1 : Form
    {
        #region variables
        private bool startFlg = true;

        private string subscriptionKey = ConfigurationManager.AppSettings["SpeechToTextSubscriptionKey"];
        
        private string defaultLocale = ConfigurationManager.AppSettings["defaultLocate"];

        private DataRecognitionClient dataClient;

        private MicrophoneRecognitionClient micClient;

        private Dispatcher _dispather = null;

        private int chartX = 1;

        //private TranslatorText tran = new TranslatorText();
        #endregion

        #region property
        /// <summary>
        /// サブスクリプションキー
        /// </summary>
        public string SubscriptionKey
        {
            get{ return this.subscriptionKey; }
        }

        public bool IsMicrophoneClientDictation { get; set; }

        public bool IsDataClientDictation { get; set; }

        public string DefaultLocale
        {
            get { return this.defaultLocale; }
        }

        private SpeechRecognitionMode Mode
        {
            get { return SpeechRecognitionMode.LongDictation; }
        }

        private bool UseMicrophone
        {
            get { return this.IsMicrophoneClientDictation; }
        }

        private string AuthenticationUri
        {
            get { return ConfigurationManager.AppSettings["AuthenticationUri"]; }
        }

        private string LongWaveFile
        {
            get { return ConfigurationManager.AppSettings["LongWaveFile"]; }
        }
        #endregion

        #region Constractor
        public Form1()
        {
            InitializeComponent();
            this.Initialize();
        }
        #endregion

        #region method

        [STAThread]
        static void Main()
        {
            Application.Run(new Form1());
        }

        /// <summary>
        /// 初期化
        /// </summary>
        private void Initialize()
        {
            this.IsMicrophoneClientDictation = true;　// TODO : リアルタイムをまずは実装
            this.IsDataClientDictation = false;

            _dispather = Dispatcher.CurrentDispatcher;

            // Set the default choice for the group of checkbox.
            //this.micRadioButton.IsChecked = true;
        }

        /// <summary>
        /// スタートログ画面出力
        /// </summary>
        private void LogRecognitionStart()
        {
            this.WriteLineTranslated("--- Start speech recognition" + " with " + this.Mode + " mode in " + this.DefaultLocale + " language ----\n\n");
        }

        /// <summary>
        /// ログ出力 変換途中テキスト
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The arguments.</param>
        private void WriteLineTranslating(string format, params object[] args)
        {
            var formattedStr = string.Format(format, args);
            Trace.WriteLine(formattedStr);
            _dispather.Invoke(() =>
            {
                this.txtTranslating.Text = (formattedStr + "\n");
            });
        }

        /// <summary>
        /// ログ出力 変換結果テキスト
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The arguments.</param>
        private void WriteLineTranslated(string format, params object[] args)
        {
            var formattedStr = string.Format(format, args);
            Trace.WriteLine(formattedStr);
            _dispather.Invoke(() =>
            {
                this.txtTranslated.Text += (formattedStr + "\n");
                this.txtTranslated.SelectionStart = this.txtTranslated.Text.Length;
                this.txtTranslated.ScrollToCaret();
            });
        }

        /// <summary>
        /// マイククライアント作成
        /// </summary>
        private void CreateMicrophoneRecoClient()
        {
            this.micClient = SpeechRecognitionServiceFactory.CreateMicrophoneClient(
                this.Mode,
                this.DefaultLocale,
                this.SubscriptionKey);
            this.micClient.AuthenticationUri = this.AuthenticationUri;

            // Event handlers for speech recognition results
            this.micClient.OnMicrophoneStatus += this.OnMicrophoneStatus;
            this.micClient.OnPartialResponseReceived += this.OnPartialResponseReceivedHandler;
            this.micClient.OnResponseReceived += this.OnMicDictationResponseReceivedHandler;

            this.micClient.OnConversationError += this.OnConversationErrorHandler;
        }

        /// <summary>
        /// データクライアント作成
        /// </summary>
        private void CreateDataRecoClient()
        {
            this.dataClient = SpeechRecognitionServiceFactory.CreateDataClient(
                this.Mode,
                this.DefaultLocale,
                this.SubscriptionKey);
            this.dataClient.AuthenticationUri = this.AuthenticationUri;

            // Event handlers for speech recognition results
            this.dataClient.OnResponseReceived += this.OnDataDictationResponseReceivedHandler;

            this.dataClient.OnPartialResponseReceived += this.OnPartialResponseReceivedHandler;
            this.dataClient.OnConversationError += this.OnConversationErrorHandler;
        }

        /// <summary>
        /// 変換結果出力
        /// </summary>
        /// <param name="e"></param>
        private void WriteResponseResult(SpeechResponseEventArgs e)
        {
            if (e.PhraseResponse.Results.Length == 0)
            {
                this.WriteLineTranslated("No phrase response is available.");
            }
            else
            {
                this.WriteLineTranslated("********* Final n-BEST Results *********");
                for (int i = 0; i < e.PhraseResponse.Results.Length; i++)
                {
                    this.WriteLineTranslated(
                        "[{0}] Confidence={1}, Text=\"{2}\"",
                        i,
                        e.PhraseResponse.Results[i].Confidence,
                        e.PhraseResponse.Results[i].DisplayText);
                }

                this.WriteLineTranslated("");

                // 変換結果表示
                var task = Task.Run(() => TranslatorText.Translate(e.PhraseResponse.Results[0].DisplayText));
                var res = task.Result;
                this.WriteLineTranslated(res);

                // 感情分析結果表示
                var task2 = Task.Run(() => TextAnalytics.PostSentiment(res));
                string res2 = task2.Result;
                this.WriteLineTranslated(res2);

                // グラフ書き出し
                double resParse;
                if (double.TryParse(res2, out resParse))
                    this.DrawChart(resParse * 100);
            }
        }

        /// <summary>
        /// 音声ファイル作成
        /// </summary>
        /// <param name="wavFileName"></param>
        private void SendAudioHelper(string wavFileName)
        {
            using (FileStream fileStream = new FileStream(wavFileName, FileMode.Open, FileAccess.Read))
            {
                // Note for wave files, we can just send data from the file right to the server.
                // In the case you are not an audio file in wave format, and instead you have just
                // raw data (for example audio coming over bluetooth), then before sending up any 
                // audio data, you must first send up an SpeechAudioFormat descriptor to describe 
                // the layout and format of your raw audio data via DataRecognitionClient's sendAudioFormat() method.
                int bytesRead = 0;
                byte[] buffer = new byte[1024];

                try
                {
                    do
                    {
                        // Get more Audio data to send into byte buffer.
                        bytesRead = fileStream.Read(buffer, 0, buffer.Length);

                        // Send of audio data to service. 
                        this.dataClient.SendAudio(buffer, bytesRead);
                    }
                    while (bytesRead > 0);
                }
                finally
                {
                    // We are done sending audio.  Final recognition results will arrive in OnResponseReceived event call.
                    this.dataClient.EndAudio();
                }
            }
        }

        /// <summary>
        /// グラフに値書き込み
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        private void DrawChart(double y)
        {
            Series series = this.chart1.Series[0];
            _dispather.Invoke(() =>
            {
                series.Points.AddXY(chartX, y);
            });
            chartX++;
        }
        #endregion

        #region events
        /// <summary>
        /// イベント：画面ロード
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
            ////Seriesの作成
            Series test = this.chart1.Series[0];
            
            //グラフのデータを追加
            test.Points.AddXY(0, 50);

            //X軸最小値、最大値、目盛間隔の設定
            this.chart1.ChartAreas[0].AxisX.Minimum = 0;
            //this.chart1.ChartAreas["area1"].AxisX.Maximum = 360;
            //this.chart1.ChartAreas["area1"].AxisX.Interval = 60;

            //Y軸最小値、最大値、目盛間隔の設定
            this.chart1.ChartAreas[0].AxisY.Minimum = 0;
            this.chart1.ChartAreas[0].AxisY.Maximum = 110;
            this.chart1.ChartAreas[0].AxisY.Interval = 10;

            //目盛線の消去
            this.chart1.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
            this.chart1.ChartAreas[0].AxisY.MajorGrid.Enabled = false;

            //作ったSeriesをchartコントロールに追加する
            //this.chart1.Series.Add(test);
        }
        
        /// <summary>
        /// イベント：画面クローズ
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosed(EventArgs e)
        {
            if (null != this.dataClient)
            {
                this.dataClient.Dispose();
            }

            if (null != this.micClient)
            {
                this.micClient.Dispose();
            }

            base.OnClosed(e);
        }

        /// <summary>
        /// イベント：スタート・ストップボタンクリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStartStop_Click(object sender, EventArgs e)
        {
            if (startFlg)
            {
                // ボタンのテキスト表示制御
                this.btnStartStop.Text = "Stop";
                startFlg = false;

                // スタートログ書き出し
                this.LogRecognitionStart();

                // TODO : まずはマイクを使った分析
                if (this.UseMicrophone)
                {
                    if (this.micClient == null)
                        this.CreateMicrophoneRecoClient();

                    this.micClient.StartMicAndRecognition();
                }
                else
                {
                    if (null == this.dataClient)
                        this.CreateDataRecoClient();

                    this.SendAudioHelper(this.LongWaveFile);
                }
            }
            else
            {
                // ボタンのテキスト表示制御
                this.btnStartStop.Text = "Start";
                startFlg = true;

                // TODO : 
            }
        }

        /// <summary>
        /// マイクイベント：マイクステータスセット
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMicrophoneStatus(object sender, MicrophoneEventArgs e)
        {
            _dispather.Invoke(() =>
            {
                WriteLineTranslated("--- Microphone status change received by OnMicrophoneStatus() ---");
                WriteLineTranslated("********* Microphone status: {0} *********", e.Recording);
                if (e.Recording)
                {
                    WriteLineTranslating("Please start speaking.");
                }
            });
        }

        /// <summary>
        /// 変換イベント：変換中結果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnIntentHandler(object sender, SpeechIntentEventArgs e)
        {
            this.WriteLineTranslating("--- Intent received by OnIntentHandler() ---\r\n{0}", e.Payload);
        }

        /// <summary>
        /// 変換イベント：変換中結果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPartialResponseReceivedHandler(object sender, PartialSpeechResponseEventArgs e)
        {
            this.WriteLineTranslating("--- Partial result received by OnPartialResponseReceivedHandler() ---\r\n{0}", e.PartialResult);
        }

        /// <summary>
        /// 変換イベント：変換完了結果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMicDictationResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            this.WriteLineTranslated("--- OnMicDictationResponseReceivedHandler ---");
            if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.EndOfDictation ||
                e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout)
            {
                _dispather.Invoke(
                    (Action)(() =>
                    {
                        this.micClient.EndMicAndRecognition();
                    }));
            }

            this.WriteResponseResult(e);


        }

        /// <summary>
        /// 変換イベント：
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDataDictationResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            this.WriteLineTranslated("--- OnDataDictationResponseReceivedHandler ---");
            if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.EndOfDictation ||
                e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout)
            {
                _dispather.Invoke(
                    (Action)(() =>
                    {
                    }));
            }

            this.WriteResponseResult(e);
        }

        /// <summary>
        /// 変換イベント：エラーハンドリング
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConversationErrorHandler(object sender, SpeechErrorEventArgs e)
        {
            _dispather.Invoke(() =>
            {
                //_startButton.IsEnabled = true;
                //_radioGroup.IsEnabled = true;
            });

            this.WriteLineTranslating("--- Error received by OnConversationErrorHandler() ---");
            this.WriteLineTranslated("--- Error received by OnConversationErrorHandler() ---");
            this.WriteLineTranslated("Error code: {0}", e.SpeechErrorCode.ToString());
            this.WriteLineTranslated("Error text: {0}", e.SpeechErrorText);
            this.WriteLineTranslated("");
        }


        #endregion
        
    }
}

﻿using SimplePNGTuber.Audio;
using SimplePNGTuber.Model;
using SimplePNGTuber.ModelEditor;
using SimplePNGTuber.Options;
using SimplePNGTuber.Server;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SimplePNGTuber
{
    public partial class MainForm : Form
    {
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 0x2;

        [DllImport("User32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("User32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private readonly Random random = new Random();
        private readonly AudioMonitor monitor;


        private PNGState state = PNGState.SILENT;
        private double blink = 0;

        private bool animationActive = false;
        private double animationProgress = -1;

        public PNGModel Model { get; private set; } = PNGModel.Empty;
        public string Expression
        {
            get => Model.CurrentExpression;
            set
            {
                Model.CurrentExpression = value;
                UpdateImage();
            }
        }

        public MainForm()
        {
            InitializeComponent();

            monitor = new AudioMonitor();

            monitor.VoiceStateChanged += VoiceStateChanged;
            Settings.Instance.SettingChanged += SettingChanged;
            LoadFromSettings();
            UpdateImage();
        }

        private void SettingChanged(object sender, SettingChangeEventArgs e)
        {
            Action action = () =>
            {
                switch (e.ChangeType)
                {
                    case SettingChangeType.MODEL:
                        LoadModel();
                        UpdateImage();
                        break;
                    case SettingChangeType.MIC:
                        monitor.RecordingDevice = Settings.Instance.MicDevice;
                        break;
                    case SettingChangeType.BACKGROUND:
                        this.BackColor = Settings.Instance.BackgroundColor;
                        this.TransparencyKey = Settings.Instance.BackgroundColor;
                        break;
                }
            };
            this.Invoke(action);
        }

        private void LoadFromSettings()
        {
            LoadModel();
            monitor.RecordingDevice = Settings.Instance.MicDevice;
            this.BackColor = Settings.Instance.BackgroundColor;
            this.TransparencyKey = Settings.Instance.BackgroundColor;
        }

        private void LoadModel()
        {
            Model = PNGModelRegistry.Instance.GetModel(Settings.Instance.ModelName);
            var modelSilent = Model.GetState(PNGState.SILENT);
            Icon icon = ConvertToIco(modelSilent, modelSilent.Width);
            notifyIcon.Icon = icon;
            this.Icon = icon;
            this.Size = new Size(modelSilent.Width, modelSilent.Height + Settings.Instance.AnimationHeight);
            this.pngTuberImageBox.Size = new Size(modelSilent.Width, modelSilent.Height);
            pngTuberImageBox.Location = new Point(0, Settings.Instance.AnimationHeight);
        }

        private void VoiceStateChanged(object sender, StateChangedEventArgs e)
        {
            if(e.VoiceActive)
            {
                animationActive = true;
                if(state == PNGState.BLINKING)
                {
                    state = PNGState.SPEAKING_BLINKING;
                }
                else
                {
                    state = PNGState.SPEAKING;
                }
            } else
            {
                if (state == PNGState.SPEAKING_BLINKING)
                {
                    state = PNGState.BLINKING;
                }
                else
                {
                    state = PNGState.SILENT;
                }
            }
            UpdateImage();
        }

        private void BlinkTimer_Tick(object sender, EventArgs e)
        {
            if(random.Next(2) == 1)
            {
                blink += Settings.Instance.BlinkFrequency;
            }
            if(blink > 1)
            {
                blink = 0;
                if(state == PNGState.SILENT)
                {
                    state = PNGState.BLINKING;
                }
                else if(state == PNGState.SPEAKING)
                {
                    state = PNGState.SPEAKING_BLINKING;
                }
                UpdateImage();
            }
            else if(state == PNGState.BLINKING)
            {
                state = PNGState.SILENT;
                UpdateImage();
            }
            else if(state == PNGState.SPEAKING_BLINKING)
            {
                state = PNGState.SPEAKING;
                UpdateImage();
            }
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if(animationActive)
            {
                animationProgress += Settings.Instance.AnimationSpeed;
                pngTuberImageBox.Location = new Point(0, (int) Math.Abs(Settings.Instance.AnimationHeight * Math.Sin(animationProgress)));
                if(animationProgress >= 1)
                {
                    animationActive = false;
                    animationProgress = -1;
                }
            }
        }

        private void UpdateImage()
        {
            pngTuberImageBox.BackgroundImage = Model.GetState(state);
        }

        private void PngTuberImageBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
            else if(e.Button == MouseButtons.Right)
            {
                contextMenuStrip.Show(pngTuberImageBox, e.Location);
            }
        }

        private Icon ConvertToIco(Image img, int size)
        {
            Icon icon;
            using (var msImg = new MemoryStream())
            using (var msIco = new MemoryStream())
            {
                img.Save(msImg, ImageFormat.Png);
                using (var bw = new BinaryWriter(msIco))
                {
                    bw.Write((short)0);           //0-1 reserved
                    bw.Write((short)1);           //2-3 image type, 1 = icon, 2 = cursor
                    bw.Write((short)1);           //4-5 number of images
                    bw.Write((byte)size);         //6 image width
                    bw.Write((byte)size);         //7 image height
                    bw.Write((byte)0);            //8 number of colors
                    bw.Write((byte)0);            //9 reserved
                    bw.Write((short)0);           //10-11 color planes
                    bw.Write((short)32);          //12-13 bits per pixel
                    bw.Write((int)msImg.Length);  //14-17 size of image data
                    bw.Write(22);                 //18-21 offset of image data
                    bw.Write(msImg.ToArray());    // write image data
                    bw.Flush();
                    bw.Seek(0, SeekOrigin.Begin);
                    icon = new Icon(msIco);
                }
            }
            return icon;
        }

        private void OptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new OptionsForm(monitor).ShowDialog();
        }

        private void CreateModelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new EditModelForm().ShowDialog();
        }

        private void EditCurrentModelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new EditModelForm(Model).ShowDialog();
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HttpServer.Instance.Stop();
            Application.Exit();
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Accord.Audio;
using Accord.Video.FFMPEG;
using Microsoft.Win32;
using Newtonsoft.Json;
using ScriptPlayer.Shared;
using ScriptPlayer.Shared.Classes;
using ScriptPlayer.Shared.Scripts;
using ScriptPlayer.VideoSync.Dialogs;
using Brush = System.Windows.Media.Brush;

namespace ScriptPlayer.VideoSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly DependencyProperty SpeedRatioModifierProperty = DependencyProperty.Register(
            "SpeedRatioModifier", typeof(double), typeof(MainWindow), new PropertyMetadata(1.0d, OnSpeedRatioModifierPropertyChanged));

        private static void OnSpeedRatioModifierPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MainWindow)d).SpeedRatioModifierChanged();
        }

        private void SpeedRatioModifierChanged()
        {
            videoPlayer.SpeedRatio = SpeedRatioModifier;
        }

        public double SpeedRatioModifier
        {
            get => (double)GetValue(SpeedRatioModifierProperty);
            set => SetValue(SpeedRatioModifierProperty, value);
        }

        public static readonly DependencyProperty PositionsProperty = DependencyProperty.Register(
            "Positions", typeof(PositionCollection), typeof(MainWindow), new PropertyMetadata(default(PositionCollection)));

        public PositionCollection Positions
        {
            get => (PositionCollection)GetValue(PositionsProperty);
            set => SetValue(PositionsProperty, value);
        }

        public static readonly DependencyProperty BeatBarDurationProperty = DependencyProperty.Register(
            "BeatBarDuration", typeof(TimeSpan), typeof(MainWindow), new PropertyMetadata(TimeSpan.FromSeconds(5.0)));

        public TimeSpan BeatBarDuration
        {
            get => (TimeSpan)GetValue(BeatBarDurationProperty);
            set => SetValue(BeatBarDurationProperty, value);
        }

        public static readonly DependencyProperty BeatBarCenterProperty = DependencyProperty.Register(
            "BeatBarCenter", typeof(double), typeof(MainWindow), new PropertyMetadata(0.5));

        public double BeatBarCenter
        {
            get => (double)GetValue(BeatBarCenterProperty);
            set => SetValue(BeatBarCenterProperty, value);
        }

        public static readonly DependencyProperty SampleXProperty = DependencyProperty.Register(
            "SampleX", typeof(int), typeof(MainWindow), new PropertyMetadata(680, OnSampleSizeChanged));

        private static void OnSampleSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MainWindow)d).UpdateSampleSize();
        }

        private void UpdateSampleSize()
        {
            var rect = new Int32Rect(SampleX, SampleY, SampleW, SampleH);
            if (rect == _captureRect) return;

            SetCaptureRect(rect);
        }

        public int SampleX
        {
            get => (int)GetValue(SampleXProperty);
            set => SetValue(SampleXProperty, value);
        }

        public static readonly DependencyProperty SampleYProperty = DependencyProperty.Register(
            "SampleY", typeof(int), typeof(MainWindow), new PropertyMetadata(700, OnSampleSizeChanged));

        public int SampleY
        {
            get => (int)GetValue(SampleYProperty);
            set => SetValue(SampleYProperty, value);
        }

        public static readonly DependencyProperty SampleWProperty = DependencyProperty.Register(
            "SampleW", typeof(int), typeof(MainWindow), new PropertyMetadata(4, OnSampleSizeChanged));

        public int SampleW
        {
            get => (int)GetValue(SampleWProperty);
            set => SetValue(SampleWProperty, value);
        }

        public static readonly DependencyProperty SampleHProperty = DependencyProperty.Register(
            "SampleH", typeof(int), typeof(MainWindow), new PropertyMetadata(4, OnSampleSizeChanged));

        public int SampleH
        {
            get => (int)GetValue(SampleHProperty);
            set => SetValue(SampleHProperty, value);
        }

        public static readonly DependencyProperty BookmarksProperty = DependencyProperty.Register(
            "Bookmarks", typeof(List<TimeSpan>), typeof(MainWindow), new PropertyMetadata(default(List<TimeSpan>)));

        public List<TimeSpan> Bookmarks
        {
            get => (List<TimeSpan>)GetValue(BookmarksProperty);
            set => SetValue(BookmarksProperty, value);
        }

        public static readonly DependencyProperty SamplerProperty = DependencyProperty.Register(
            "Sampler", typeof(ColorSampler), typeof(MainWindow), new PropertyMetadata(default(ColorSampler)));

        public ColorSampler Sampler
        {
            get => (ColorSampler)GetValue(SamplerProperty);
            set => SetValue(SamplerProperty, value);
        }

        public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
            "Duration", typeof(double), typeof(MainWindow), new PropertyMetadata(default(double)));

        public double Duration
        {
            get => (double)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        public static readonly DependencyProperty BeatsProperty = DependencyProperty.Register(
            "Beats", typeof(BeatCollection), typeof(MainWindow), new PropertyMetadata(default(BeatCollection)));

        public BeatCollection Beats
        {
            get => (BeatCollection)GetValue(BeatsProperty);
            set => SetValue(BeatsProperty, value);
        }


        public static readonly DependencyProperty BeatCountProperty = DependencyProperty.Register(
            "BeatCount", typeof(int), typeof(MainWindow), new PropertyMetadata(default(int)));

        public int BeatCount
        {
            get => (int)GetValue(BeatCountProperty);
            set => SetValue(BeatCountProperty, value);
        }


        public static readonly DependencyProperty PixelPreviewProperty = DependencyProperty.Register(
            "PixelPreview", typeof(Brush), typeof(MainWindow), new PropertyMetadata(default(Brush)));

        public Brush PixelPreview
        {
            get => (Brush)GetValue(PixelPreviewProperty);
            set => SetValue(PixelPreviewProperty, value);
        }

        public MainWindow()
        {
            Bookmarks = new List<TimeSpan>();
            SetAllBeats(new BeatCollection());
            Positions = new PositionCollection();
            InitializeComponent();
        }

        private void InitializeSampler()
        {
            Sampler = new ColorSampler();
            Sampler.BeatDetected += SamplerOnBeatDetected;
            Sampler.Sample = _captureRect;

            RefreshSampler();
        }

        private void RefreshSampler()
        {
            Sampler.Resolution = videoPlayer.Player.Resolution;
            Sampler.Source = videoPlayer.Player.VideoBrush;
            Sampler.TimeSource = videoPlayer.TimeSource;
            Sampler.Refresh();
        }

        private void SamplerOnBeatDetected(object sender, TimeSpan d)
        {
            if (cckRecord.IsChecked != true) return;
            AddBeat(d);
            BeatCount = Beats.Count;
        }

        private Int32Rect _captureRect = new Int32Rect(680, 700, 4, 4);
        private bool _wasplaying;

        private string _videoFile;
        private string _projectFile;

        private FrameCaptureCollection _frameSamples;
        private BeatCollection _originalBeats;

        private PixelColorSampleCondition _condition = new PixelColorSampleCondition();
        private AnalysisParameters _parameters = new AnalysisParameters();

        private TimeSpan _marker1;
        private TimeSpan _marker2;

        private void AddBeat(TimeSpan positionTotalSeconds)
        {
            Debug.WriteLine(positionTotalSeconds);
            Beats.Add(positionTotalSeconds);
            Beats.Sort();
        }


        private readonly string[] _supportedVideoExtensions = { "mp4", "mpg", "mpeg", "m4v", "avi", "mkv", "mp4v", "mov", "wmv", "asf" };
        private void mnuOpenVideo_Click(object sender, RoutedEventArgs e)
        {
            string videoFilters = String.Join(";", _supportedVideoExtensions.Select(v => $"*.{v}"));

            OpenFileDialog dialog = new OpenFileDialog { Filter = $"Videos|{videoFilters}|All Files|*.*" };
            if (dialog.ShowDialog(this) != true) return;

            OpenVideo(dialog.FileName, true, true);
        }

        private void SeekBar_OnSeek(object sender, double relative, TimeSpan absolute, int downMoveUp)
        {
            switch (downMoveUp)
            {
                case 0:
                    _wasplaying = videoPlayer.TimeSource.IsPlaying;
                    videoPlayer.TimeSource.Pause();
                    videoPlayer.SetPosition(absolute);
                    break;
                case 1:
                    videoPlayer.SetPosition(absolute);
                    break;
                case 2:
                    videoPlayer.SetPosition(absolute);
                    if (_wasplaying)
                        videoPlayer.TimeSource.Play();
                    break;
            }
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            videoPlayer.TimeSource.Pause();
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            videoPlayer.TimeSource.Play();
        }

        private void mnuClear_Click(object sender, RoutedEventArgs e)
        {
            Beats.Clear();
            Positions.Clear();
        }

        private void mnuSaveBeats_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(_videoFile) ?? Environment.CurrentDirectory,
                Filter = "Text-File|*.txt"
            };
            if (dialog.ShowDialog(this) != true) return;

            SaveAsBeatsFile(dialog.FileName);
        }

        private void SaveAsBeatsFile(string filename)
        {
            Beats.Save(filename);
        }

        private void mnuLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "Text-File|*.txt" };

            if (dialog.ShowDialog(this) != true) return;
            LoadBeatsFile(dialog.FileName);
        }

        private void LoadBeatsFile(string filename)
        {
            SetAllBeats(BeatCollection.Load(filename));
        }

        private void videoPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeSampler();
            videoPlayer.Volume = 20;
        }

        private void VideoPlayer_OnMediaOpened(object sender, EventArgs e)
        {
            RefreshSampler();
        }

        private void videoPlayer_VideoMouseDown(object sender, int x, int y)
        {
            SetCaptureRect(new Int32Rect(x, y, SampleW, SampleH));
        }

        private void SetCaptureRect(Int32Rect rect)
        {
            _captureRect = rect;

            SampleX = rect.X;
            SampleY = rect.Y;
            SampleW = rect.Width;
            SampleH = rect.Height;

            Debug.WriteLine($"Set Sample Area to: {_captureRect.X} / {_captureRect.Y} ({_captureRect.Width} x {_captureRect.Height})");
            Sampler.Sample = _captureRect;
            videoPlayer.SampleRect = new Rect(_captureRect.X, _captureRect.Y, _captureRect.Width, _captureRect.Height);
        }

        private void VideoPlayer_OnVideoMouseUp(object sender, int x, int y)
        {
            //throw new NotImplementedException();
        }

        private void mnuFrameSampler_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FrameSamplerDialog(_videoFile, _captureRect, videoPlayer.Duration);
            if (dialog.ShowDialog() != true) return;

            SetSamples(dialog.Result, dialog.Thumbnails);
        }

        private void mnuSaveSamples_Click(object sender, RoutedEventArgs e)
        {
            if (_frameSamples == null)
            {
                MessageBox.Show("No Samples loaded!");
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Frame Sample Files|*.framesamples|All Files|*.*",
                FileName = Path.GetFileNameWithoutExtension(_videoFile) ?? Environment.CurrentDirectory
            };

            if (dialog.ShowDialog(this) != true) return;

            _frameSamples.SaveToFile(dialog.FileName);
        }

        private void mnuLoadSamples_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "Frame Sample Files|*.framesamples|All Files|*.*" };

            if (dialog.ShowDialog(this) != true) return;

            SetSamples(FrameCaptureCollection.FromFile(dialog.FileName), null);

            if (_videoFile != _frameSamples.VideoFile)
            {
                if (MessageBox.Show(this, "Load '" + _frameSamples.VideoFile + "'?", "Open associated file?",
                        MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    OpenVideo(_frameSamples.VideoFile, true, false);
                }
            }
        }

        private void SetSamples(FrameCaptureCollection frames, Dictionary<long, BitmapSource> thumbnails)
        {
            _frameSamples = frames;
            SetCaptureRect(_frameSamples.CaptureRect);
            colorSampleBar.Frames = _frameSamples;

            VideoThumbnailCollection collection = new VideoThumbnailCollection();
            if (thumbnails != null)
            {
                foreach(var kvp in thumbnails)
                    collection.Add(frames.FrameIndexToTimeSpan(kvp.Key), kvp.Value);
            }

            SeekBar.Thumbnails = collection;
        }

        private void OpenVideo(string videoFile, bool play, bool resetFileNames)
        {
            _videoFile = videoFile;
            videoPlayer.Open(videoFile);

            SetTitle(videoFile);

            if (play)
                videoPlayer.TimeSource.Play();

            if (resetFileNames)
            {
                _projectFile = null;
            }
        }

        private void mnuAnalyseSamples_Click(object sender, RoutedEventArgs e)
        {
            AnalyseSamples();
        }

        private void AnalyseSamples()
        {
            if (_frameSamples == null)
            {
                MessageBox.Show(this, "No samples to analyse!");
                return;
            }

            FrameAnalyserDialog dialog = new FrameAnalyserDialog(_frameSamples, _condition, _parameters);

            if (dialog.ShowDialog() != true) return;

            SetAllBeats(dialog.Result);
        }

        private void mnuAnalyseSelection_Click(object sender, RoutedEventArgs e)
        {
            if (_frameSamples == null)
            {
                MessageBox.Show(this, "No samples to analyse!");
                return;
            }

            FrameAnalyserDialog dialog = new FrameAnalyserDialog(_frameSamples, _condition, _parameters);

            if (dialog.ShowDialog() != true) return;

            OverridesSelection(dialog.Result);
        }

        private void OverridesSelection(List<TimeSpan> beats)
        {
            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            List<TimeSpan> newBeats = Beats.Where(t => t < tBegin || t > tEnd).ToList();
            newBeats.AddRange(beats.Where(t => t >= tBegin && t <= tEnd).ToList());

            SetAllBeats(newBeats);
        }

        private void SetAllBeats(IEnumerable<TimeSpan> beats)
        {
            _originalBeats = new BeatCollection(beats);
            Beats = _originalBeats.Duplicate();

            RefreshHeatMap();
        }

        private double GetDouble(double defaultValue, string title = null)
        {
            DoubleInputDialog dialog = new DoubleInputDialog(defaultValue) { Owner = this };
            if (!String.IsNullOrWhiteSpace(title))
                dialog.Title = title;

            dialog.Owner = this;
            if (dialog.ShowDialog() != true)
                return double.NaN;

            return dialog.Result;
        }

        private void mnuScale_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBeats == null) return;
            double scale = GetDouble(1);
            if (double.IsNaN(scale)) return;

            Beats = Beats.Scale(scale);
        }

        private void mnuShift_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBeats == null) return;
            double shift = GetDouble(0);
            if (double.IsNaN(shift)) return;

            var timeSpan = TimeSpan.FromSeconds(shift);
            Beats = Beats.Shift(timeSpan);
            Positions = Positions.Shift(timeSpan);
        }

        private void mnuReset_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBeats == null) return;
            Beats = _originalBeats.Duplicate();
        }

        private void btnAddBookmark_Click(object sender, RoutedEventArgs e)
        {
            List<TimeSpan> newBookMarks = new List<TimeSpan>(Bookmarks) { videoPlayer.GetPosition() };
            newBookMarks.Sort();

            Bookmarks = newBookMarks;
        }

        private void btnResetBookmarks_Click(object sender, RoutedEventArgs e)
        {
            Bookmarks = new List<TimeSpan>();
        }

        private void Bookmark_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem item = sender as ListBoxItem;

            if (!(item?.DataContext is TimeSpan)) return;

            TimeSpan position = (TimeSpan)item.DataContext;

            videoPlayer.SetPosition(position);
        }

        private void mnuSetCondition_Click(object sender, RoutedEventArgs e)
        {
            PixelColorSampleCondition currentCondition = _condition;
            AnalysisParameters currentParameters = _parameters;
            ConditionEditorDialog dialog = new ConditionEditorDialog(currentCondition, currentParameters);
            dialog.LiveConditionUpdate += DialogOnLiveConditionUpdate;

            if (dialog.ShowDialog() != true)
            {
                SetCondition(currentCondition);
                _parameters = currentParameters;
            }
            else
            {
                SetCondition(dialog.Result);
                _parameters = dialog.Result2;

                if (dialog.Reanalyse)
                    AnalyseSamples();
            }

        }

        private void DialogOnLiveConditionUpdate(object sender, PixelColorSampleCondition condition)
        {
            SetCondition(condition);
        }

        private void SetCondition(PixelColorSampleCondition condition)
        {
            _condition = condition;
            colorSampleBar.SampleCondition = condition;
        }

        private void ShiftTime(TimeSpan timespan)
        {
            videoPlayer.SetPosition(videoPlayer.GetPosition() + timespan);
        }

        private void btnBeatBarDurationBack_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(-BeatBarDuration);
        }

        private void btnBeatbarDurationForward_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(BeatBarDuration);
        }

        private void btnPreviousBookmark_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(TimeSpan.FromMinutes(-1));
        }

        private void btnSecondBack_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(TimeSpan.FromSeconds(-1));
        }

        private void btnFrameBack_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(TimeSpan.FromMilliseconds(-10));
        }

        private void btnFrameForward_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(TimeSpan.FromMilliseconds(10));
        }

        private void btnSecondForward_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(TimeSpan.FromSeconds(1));
        }

        private void btnNextBookmark_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(TimeSpan.FromMinutes(1));
        }

        private void SetMarker(int index, TimeSpan position)
        {
            switch (index)
            {
                case 1:
                    _marker1 = position;
                    BeatBar.Marker1 = _marker1;
                    break;
                case 2:
                    _marker2 = position;
                    BeatBar.Marker2 = _marker2;
                    break;
            }

            UpdateSelection();
        }

        private void UpdateSelection()
        {
            var beats = Beats.GetBeats(_marker1, _marker2).ToList();
            if (beats.Count >= 2)
            {
                var duration = beats.Last() - beats.First();

                txtSelectStart.Text = beats.First().ToString("g");
                txtSelectEnd.Text = beats.Last().ToString("g");
                txtSelectDuration.Text = duration.ToString("g");
                txtSelectBeats.Text = beats.Count.ToString("D");
                txtSelectAvgDuration.Text = duration.Divide(beats.Count - 1).ToString("g");
            }
        }

        private void mnuJumpToFirstBeat_Click(object sender, RoutedEventArgs e)
        {
            JumpToFirst();
        }

        private void JumpToFirst()
        {
            if (Beats == null) return;
            if (Beats.Count == 0) return;
            TimeSpan beat = Beats.First();
            videoPlayer.SetPosition(beat);
        }

        private void mnuFindShortestBeat_Click(object sender, RoutedEventArgs e)
        {
            JumpToShortest();
        }

        private void JumpToShortest()
        {
            if (Beats == null) return;
            if (Beats.Count < 2) return;

            TimeSpan shortest = TimeSpan.MaxValue;
            TimeSpan position = TimeSpan.Zero;

            for (int i = 0; i < Beats.Count - 1; i++)
            {
                TimeSpan duration = Beats[i + 1] - Beats[i];
                if (duration < shortest)
                {
                    shortest = duration;
                    position = Beats[i];
                }
            }

            videoPlayer.SetPosition(position);

            Fadeout.SetText("Shortest beat: " + shortest.TotalMilliseconds + " ms", TimeSpan.FromSeconds(4));
        }

        private void mnuJumpToLastBeat_Click(object sender, RoutedEventArgs e)
        {
            JumpToLast();
        }

        private void JumpToLast()
        {
            if (Beats == null) return;
            if (Beats.Count == 0) return;
            TimeSpan beat = Beats.Last();
            videoPlayer.SetPosition(beat);
        }

        private void mnuSaveKiiroo_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Kiiroo JS|*.js|All Files|*.*",
                FileName = Path.GetFileNameWithoutExtension(_videoFile) ?? Environment.CurrentDirectory
            };

            if (dialog.ShowDialog(this) != true) return;

            using (StreamWriter writer = new StreamWriter(dialog.FileName, false))
            {
                writer.Write("var kiiroo_subtitles = {");

                CultureInfo culture = new CultureInfo("en-us");

                List<string> commands = new List<string>();

                bool up = false;
                foreach (TimeSpan timestamp in Beats)
                {
                    up ^= true;

                    commands.Add(String.Format(culture, "{0:f2}:{1}", timestamp.TotalSeconds, up ? 4 : 1));
                }

                writer.Write(String.Join(",", commands));

                writer.Write("};");
            }
        }

        private void mnuLoadFun_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "Funscript|*.funscript" };

            if (dialog.ShowDialog(this) != true)
                return;

            FunScriptLoader loader = new FunScriptLoader();
            var actions = loader.Load(dialog.FileName).Cast<FunScriptAction>();

            var pos = actions.Select(s => new TimedPosition()
            {
                TimeStamp = s.TimeStamp,
                Position = s.Position
            });

            Positions = new PositionCollection(pos);
        }

        private void mnuSaveFunscript_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Funscript|*.funscript|All Files|*.*",
                FileName = Path.GetFileNameWithoutExtension(_videoFile) ?? Environment.CurrentDirectory
            };

            if (dialog.ShowDialog(this) != true) return;

            SaveAsFunscript(dialog.FileName);
        }

        private void SaveAsFunscript(string filename)
        {
            if (Positions == null)
                return;

            FunScriptFile script = new FunScriptFile
            {
                Inverted = false,
                Range = 90,
                Version = new Version(1, 0),
                Actions = Positions.Select(p => new FunScriptAction
                {
                    Position = p.Position,
                    TimeStamp = p.TimeStamp
                }).ToList()
            };

            string content = JsonConvert.SerializeObject(script);

            File.WriteAllText(filename, content, new UTF8Encoding(false));
        }

        private void btnResetSamples_Click(object sender, RoutedEventArgs e)
        {
            if (_frameSamples == null) return;

            SetCaptureRect(_frameSamples.CaptureRect);
        }

        private void DeleteBeatsWithinMarkers()
        {
            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            SetAllBeats(Beats.Where(t => t < tBegin || t > tEnd));
        }

        private void mnuTrackBlob_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new VisualTrackerDialog(_videoFile, _marker1, _marker2, new Rectangle(SampleX, SampleY, SampleW, SampleH));
            dialog.Show();
        }

        private void mnuShowMock_Click(object sender, RoutedEventArgs e)
        {
            new MocktestDialog().Show();
        }

        private void SaveProjectAs()
        {
            if (string.IsNullOrWhiteSpace(_videoFile))
                return;

            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Beat Projects|*.bproj",
                FileName = Path.ChangeExtension(_videoFile, ".bproj")
            };

            if (dialog.ShowDialog(this) != true)
                return;

            SaveProjectAs(dialog.FileName);
        }

        private void SaveProjectAs(string filename)
        {
            BeatProject project = new BeatProject
            {
                VideoFile = _videoFile,
                SampleCondition = _condition,
                AnalysisParameters = _parameters,
                BeatBarDuration = BeatBar.TotalDisplayedDuration.TotalSeconds,
                BeatBarMidpoint = BeatBar.Midpoint,
                Beats = Beats.Select(b => b.Ticks).ToList(),
                Bookmarks = Bookmarks.Select(b => b.Ticks).ToList(),
                Positions = Positions.ToList()
            };
            project.Save(filename);
            _projectFile = filename;

            SaveAsFunscript(Path.ChangeExtension(_videoFile, "funscript"));
            SaveAsBeatsFile(Path.ChangeExtension(_videoFile, "txt"));
        }

        private void mnuLoadProject_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "Beat Projects|*.bproj" };
            if (dialog.ShowDialog(this) != true)
                return;

            BeatProject project = BeatProject.Load(dialog.FileName);

            if (File.Exists(project.VideoFile))
                OpenVideo(project.VideoFile, true, false);
            else
            {
                string otherPath = ChangePath(dialog.FileName, project.VideoFile);
                OpenVideo(otherPath, true, false);
            }

            SetCondition(project.SampleCondition);
            _parameters = project.AnalysisParameters;
            BeatBarDuration = TimeSpan.FromSeconds(project.BeatBarDuration);
            BeatBarCenter = project.BeatBarMidpoint;
            Beats = new BeatCollection(project.Beats.Select(TimeSpan.FromTicks));
            Bookmarks = project.Bookmarks.Select(TimeSpan.FromTicks).ToList();
            Positions = new PositionCollection(project.Positions);

            _projectFile = dialog.FileName;

            RefreshHeatMap();
        }

        private string ChangePath(string path, string filename)
        {
            string directory = Path.GetDirectoryName(path);
            string file = Path.GetFileName(filename);

            return Path.Combine(directory, file);
        }

        private void mnuSaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(_projectFile))
                SaveProjectAs();
            else
                SaveProjectAs(_projectFile);
        }

        private void Normalize()
        {
            bool wasPlaying = videoPlayer.TimeSource.IsPlaying;
            if (wasPlaying)
                videoPlayer.TimeSource.Pause();

            int selectedBeats = GetSelectedBeats().Count;

            int additionalBeats = GetAdditionalBeats(selectedBeats);
            if (additionalBeats == int.MinValue) return;

            bool caps = Keyboard.IsKeyToggled(Key.CapsLock);
            Normalize(additionalBeats, !caps);

            if (wasPlaying)
                videoPlayer.TimeSource.Play();
        }

        private int GetAdditionalBeats(int selectedBeats)
        {
            NormalizationDialog dialog = new NormalizationDialog(selectedBeats) { Owner = this };
            if (dialog.ShowDialog() != true) return int.MinValue;

            return dialog.AdditionalBeats;
        }

        private void Normalize(int additionalBeats, bool trimToBeats = true)
        {
            List<TimeSpan> beatsToEvenOut = GetSelectedBeats();

            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            List<TimeSpan> otherBeats = Beats.Where(t => t < tBegin || t > tEnd).ToList();

            if (beatsToEvenOut.Count < 2 && trimToBeats)
                return;

            TimeSpan first = trimToBeats ? beatsToEvenOut.Min() : tBegin;
            TimeSpan last = trimToBeats ? beatsToEvenOut.Max() : tEnd;

            int numberOfBeats = beatsToEvenOut.Count + additionalBeats;
            if (numberOfBeats < 2)
                numberOfBeats = 2;

            TimeSpan tStart = first;
            TimeSpan intervall = (last - first).Divide(numberOfBeats - 1);

            for (int i = 0; i < numberOfBeats; i++)
                otherBeats.Add(tStart + intervall.Multiply(i));

            Fadeout.SetText(intervall.TotalMilliseconds.ToString("f0") + "ms", TimeSpan.FromSeconds(4));

            SetAllBeats(otherBeats);
        }

        private void PatternFill()
        {
            PatternFillOptionsDialog dialog = new PatternFillOptionsDialog(_previousPattern) { Owner = this };
            dialog.PatternChanged += PreviewFill;
            try
            {
                if (dialog.ShowDialog() != true)
                    return;

                _previousPattern = dialog.Result;
                PatternFill(_previousPattern);
            }
            finally
            {
                ClearPreview();
                dialog.PatternChanged -= PreviewFill;
            }
        }

        private void ClearPreview()
        {
            BeatBar.PreviewBeats = null;
        }

        private void PreviewFill(object sender, PatternEventArgs e)
        {
            var pattern = e.Pattern;

            List<TimeSpan> beatsToEvenOut = GetSelectedBeats();

            if (beatsToEvenOut.Count < 2)
                return;

            TimeSpan first = beatsToEvenOut.Min();
            TimeSpan last = beatsToEvenOut.Max();

            int numberOfBeats = beatsToEvenOut.Count;
            if (numberOfBeats < 2)
                numberOfBeats = 2;

            TimeSpan tStart = first;
            TimeSpan intervall = (last - first).Divide(numberOfBeats - 1);
            TimeSpan smallIntervall = intervall.Divide(pattern.Length - 1);

            BeatCollection result = new BeatCollection();

            for (int i = 0; i < numberOfBeats; i++)
            {
                if (i + 1 < numberOfBeats)
                {
                    for (int j = 0; j < pattern.Length - 1; j++)
                    {
                        if (pattern[j])
                            result.Add(tStart + intervall.Multiply(i) + smallIntervall.Multiply(j));
                    }
                }
                else
                {
                    result.Add(last);
                }
            }

            BeatBar.PreviewBeats = result;
            BeatBar.PreviewHighlightInterval = pattern.Count(p => p) -1;
        }

        private void Fade()
        {
            List<TimeSpan> beatsToEvenOut = GetSelectedBeats();

            if (beatsToEvenOut.Count < 4)
                return;

            TimeSpan first = beatsToEvenOut[1] - beatsToEvenOut[0];
            TimeSpan last = beatsToEvenOut[beatsToEvenOut.Count - 1] - beatsToEvenOut[beatsToEvenOut.Count - 2];

            Fade(first, last);
        }

        private void FadeNormalize()
        {
            List<TimeSpan> beatsToEvenOut = GetSelectedBeats();

            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            List<TimeSpan> otherBeats = Beats.Where(t => t < tBegin || t > tEnd).ToList();

            if (beatsToEvenOut.Count < 3)
                return;

            TimeSpan first = beatsToEvenOut.Min();
            TimeSpan last = beatsToEvenOut.Max();

            //TODO invert?

            TimeSpan firstSpan = beatsToEvenOut[1] - first;

            int count = beatsToEvenOut.Count - 1;
            int n = count - 1;

            TimeSpan totalSpan = last - first;
            TimeSpan totalSpanByBase = firstSpan.Multiply(count);
            TimeSpan totalSpanByAddition = totalSpan - totalSpanByBase;

            int totalAdditions = (n * (n + 1)) / 2;

            TimeSpan addedSpan = totalSpanByAddition.Divide(totalAdditions);

            otherBeats.Add(first);

            TimeSpan previous = first;

            for (int i = 0; i < count; i++)
            {
                previous += firstSpan + addedSpan.Multiply(i);
                otherBeats.Add(previous);
            }

            SetAllBeats(otherBeats);
        }

        private void NormalizePattern()
        {
            PatternFillOptionsDialog dialog = new PatternFillOptionsDialog(_previousPattern) { Owner = this };
            dialog.PatternChanged += PreviewNormalize;

            try
            {
                if (dialog.ShowDialog() != true) return;

                _previousPattern = dialog.Result;
                NormalizePattern(_previousPattern, 0);
            }
            finally
            {
                dialog.PatternChanged -= PreviewNormalize;
                ClearPreview();
            }
        }

        private void PreviewNormalize(object sender, PatternEventArgs e)
        {
            bool[] pattern = e.Pattern;
            List<TimeSpan> beatsToEvenOut = GetSelectedBeats();

            List<TimeSpan> otherBeats = new List<TimeSpan>();

            if (beatsToEvenOut.Count < 2)
                return;

            int numberOfBeats = beatsToEvenOut.Count;
            if (numberOfBeats < 2)
                return;

            TimeSpan first = beatsToEvenOut.Min();
            TimeSpan last = beatsToEvenOut.Max();

            int beatsInSet = 0;
            for (int i = 0; i < pattern.Length - 1; i++)
                if (pattern[i])
                    beatsInSet++;

            int fullSets = numberOfBeats / beatsInSet;
            int beatsInOpenSet = numberOfBeats % beatsInSet;

            if (beatsInOpenSet == 0)
            {
                beatsInOpenSet = beatsInSet;
                fullSets--;
            }

            int openSetLength = 0;

            for (int i = 0; i < pattern.Length - 1; i++)
            {
                if (beatsInOpenSet == 0)
                    break;

                openSetLength++;

                if (pattern[i])
                    beatsInOpenSet--;
            }

            int measureCount = openSetLength + (pattern.Length - 1) * fullSets;

            TimeSpan beatDuration = (last - first).Divide(measureCount - 1);

            for (int i = 0; i < measureCount; i++)
            {
                if (pattern[i % (pattern.Length - 1)])
                    otherBeats.Add(first + beatDuration.Multiply(i));
            }

            BeatBar.PreviewBeats = new BeatCollection(otherBeats);
            BeatBar.PreviewHighlightInterval = pattern.Count(p => p);
        }

        private void NormalizePattern(bool[] pattern, int additionalBeats)
        {
            List<TimeSpan> beatsToEvenOut = GetSelectedBeats();

            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            List<TimeSpan> otherBeats = Beats.Where(t => t < tBegin || t > tEnd).ToList();

            if (beatsToEvenOut.Count < 2)
                return;

            int numberOfBeats = beatsToEvenOut.Count + additionalBeats;
            if (numberOfBeats < 2)
                return;

            TimeSpan first = beatsToEvenOut.Min();
            TimeSpan last = beatsToEvenOut.Max();

            int beatsInSet = 0;
            for (int i = 0; i < pattern.Length - 1; i++)
                if (pattern[i])
                    beatsInSet++;

            int fullSets = numberOfBeats / beatsInSet;
            int beatsInOpenSet = numberOfBeats % beatsInSet;

            if (beatsInOpenSet == 0)
            {
                beatsInOpenSet = beatsInSet;
                fullSets--;
            }

            int openSetLength = 0;

            for (int i = 0; i < pattern.Length - 1; i++)
            {
                if (beatsInOpenSet == 0)
                    break;

                openSetLength++;

                if (pattern[i])
                    beatsInOpenSet--;
            }

            int measureCount = openSetLength + (pattern.Length - 1) * fullSets;

            TimeSpan beatDuration = (last - first).Divide(measureCount - 1);

            for (int i = 0; i < measureCount; i++)
            {
                if (pattern[i % (pattern.Length - 1)])
                    otherBeats.Add(first + beatDuration.Multiply(i));
            }

            Fadeout.SetText(beatDuration.TotalMilliseconds.ToString("f0") + "ms", TimeSpan.FromSeconds(4));

            SetAllBeats(otherBeats);
        }

        private void Fade(TimeSpan firstLength, TimeSpan lastLength)
        {
            List<TimeSpan> beatsToEvenOut = GetSelectedBeats();

            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            List<TimeSpan> otherBeats = Beats.Where(t => t < tBegin || t > tEnd).ToList();

            if (beatsToEvenOut.Count < 2)
                return;

            TimeSpan first = beatsToEvenOut.Min();
            TimeSpan last = beatsToEvenOut.Max();

            var iterations = FindBestIterations(firstLength, lastLength, last - first);

            for (int i = 0; i <= iterations.Item3; i++)
            {
                // ReSharper disable once PossibleLossOfFraction
                // Can't happen - because math :)
                otherBeats.Add(first + iterations.Item1.Multiply(i) + iterations.Item2.Multiply(i * (i - 1) / 2));
            }

            Fadeout.SetText("n = " + iterations.Item3, TimeSpan.FromSeconds(4));

            SetAllBeats(otherBeats);
        }

        private Tuple<TimeSpan, TimeSpan, int> FindBestIterations(TimeSpan firstLength, TimeSpan lastLength, TimeSpan duration)
        {
            bool invert = firstLength > lastLength;

            if (invert)
            {
                TimeSpan temp = firstLength;
                firstLength = lastLength;
                lastLength = temp;
            }

            int lowest = (int)Math.Floor(duration.Divide(lastLength));
            int highest = (int)Math.Ceiling(duration.Divide(firstLength));

            double ticksFirst = firstLength.Ticks;
            double ticksLast = lastLength.Ticks;
            double ticksDuration = duration.Ticks;

            int bestN = 0;
            double closestN = double.MaxValue;

            for (int n = lowest; n <= highest; n++)
            {
                double ticksIncrement = (ticksLast - ticksFirst) / n;
                double totalDuration = (((n + 1.0) * n) / 2.0) * ticksIncrement + ticksFirst * (n + 1);

                double distanceToTarget = Math.Abs(ticksDuration - totalDuration);
                if (distanceToTarget < closestN)
                {
                    closestN = distanceToTarget;
                    bestN = n;
                }
            }

            return GetGain(ticksFirst, ticksLast, ticksDuration, bestN, invert);
        }

        private Tuple<TimeSpan, TimeSpan, int> GetGain(double ticksFirst, double ticksLast, double ticksDuration, int bestN, bool invert)
        {
            long finalTicksIncrement = (long)(ticksLast - ticksFirst) / bestN;
            long totalIncrements = ((bestN * (1 + bestN)) / 2) * finalTicksIncrement;
            long firstTickLength = (long)(ticksDuration - totalIncrements) / (bestN - 1);

            if (invert)
            {
                return new Tuple<TimeSpan, TimeSpan, int>(
                    TimeSpan.FromTicks(firstTickLength + bestN * finalTicksIncrement),
                    TimeSpan.FromTicks(-finalTicksIncrement),
                    bestN);
            }
            else
            {
                return new Tuple<TimeSpan, TimeSpan, int>(
                    TimeSpan.FromTicks(firstTickLength),
                    TimeSpan.FromTicks(finalTicksIncrement),
                    bestN);
            }
        }

        private void PatternFill(bool[] pattern)
        {
            List<TimeSpan> beatsToEvenOut = GetSelectedBeats();

            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            List<TimeSpan> otherBeats = Beats.Where(t => t < tBegin || t > tEnd).ToList();

            if (beatsToEvenOut.Count < 2)
                return;

            TimeSpan first = beatsToEvenOut.Min();
            TimeSpan last = beatsToEvenOut.Max();

            int numberOfBeats = beatsToEvenOut.Count;
            if (numberOfBeats < 2)
                numberOfBeats = 2;

            TimeSpan tStart = first;
            TimeSpan intervall = (last - first).Divide(numberOfBeats - 1);
            TimeSpan smallIntervall = intervall.Divide(pattern.Length - 1);

            for (int i = 0; i < numberOfBeats; i++)
            {
                otherBeats.Add(tStart + intervall.Multiply(i));
                if (i + 1 < numberOfBeats)
                {
                    for (int j = 1; j < pattern.Length - 1; j++)
                    {
                        if (pattern[j])
                            otherBeats.Add(tStart + intervall.Multiply(i) + smallIntervall.Multiply(j));
                    }
                }
            }

            Fadeout.SetText(smallIntervall.TotalMilliseconds.ToString("f0") + "ms", TimeSpan.FromSeconds(4));

            SetAllBeats(otherBeats);
        }

        private List<TimeSpan> GetSelectedBeats()
        {
            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            return Beats.GetBeats(tBegin, tEnd).ToList();
        }

        private void mnuShowBeatDuration_Click(object sender, RoutedEventArgs e)
        {
            TimeSpan previous = TimeSpan.MinValue;
            TimeSpan next = TimeSpan.MaxValue;

            TimeSpan position = videoPlayer.GetPosition();

            foreach (TimeSpan t in Beats)
            {
                if (t < position)
                    previous = t;
                else
                {
                    next = t;
                    break;
                }
            }

            if (previous == TimeSpan.MinValue || next == TimeSpan.MaxValue)
            {
                MessageBox.Show("Can't find adjecent beat!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show((next - previous).TotalMilliseconds.ToString("f0"), "Duration", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (GridSampleRect.IsKeyboardFocusWithin)
            {
                switch (e.Key)
                {
                    case Key.Escape:
                        GridSplitter.Focus();
                        break;
                }

                return;
            }

            bool control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            bool caps = Keyboard.IsKeyToggled(Key.CapsLock);
            var pattern = _previousPattern ?? new bool[] { true, true };
            int patternBeats = Math.Max(pattern.Count(b => b) - 1, 1);

            bool handled = true;
            switch (e.Key)
            {
                case Key.Home:
                    {
                        GotoSelectionBegin();
                        break;
                    }
                case Key.End:
                    {
                        GotoSelectionEnd();
                        break;
                    }
                case Key.PageDown:
                    {
                        GotoNextBookMark();
                        break;
                    }
                case Key.PageUp:
                    {
                        GotoPreviousBookMark();
                        break;
                    }
                case Key.Space:
                    {
                        if (videoPlayer.TimeSource.IsPlaying)
                            videoPlayer.TimeSource.Pause();
                        else
                            videoPlayer.TimeSource.Play();
                        break;
                    }
                case Key.Left:
                case Key.Right:
                    {
                        double multiplier = e.Key == Key.Right ? 1 : -1;

                        if (cckRTL.IsChecked == true)
                            multiplier *= -1;

                        if (control && shift)
                            ShiftTime(TimeSpan.FromMilliseconds(multiplier * 100));
                        else if (control)
                            ShiftTime(BeatBarDuration.Multiply(multiplier / 2.0));
                        else if (shift)
                            ShiftTime(TimeSpan.FromMilliseconds(multiplier * 20));
                        else
                            ShiftTime(TimeSpan.FromMilliseconds(multiplier * 1000));
                        break;
                    }
                case Key.N:
                    {
                        Normalize();
                        break;
                    }
                case Key.O:
                    {
                        NormalizePattern();
                        break;
                    }
                case Key.P:
                    {
                        PatternFill();
                        break;
                    }
                case Key.F:
                    {
                        Fade();
                        break;
                    }
                case Key.C:
                {
                    if (shift)
                        EnforceCommonBeatDuration();
                    else
                        FindCommonBeatDuration();
                    break;
                }
                case Key.E:
                    {
                        EqualizeBeatLengths();
                        break;
                    }
                case Key.T:
                    {
                        FadeNormalize();
                        break;
                    }
                case Key.Delete:
                    {
                        DeleteBeatsWithinMarkers();
                        break;
                    }
                case Key.Add:
                    {
                        if (control)
                        {
                            if (shift)
                                NormalizePattern(pattern, patternBeats);
                            else
                                NormalizePattern(pattern, 1);
                        }
                        else
                            Normalize(1, !caps);
                        break;
                    }
                case Key.Subtract:
                    {
                        if (control)
                        {
                            if (shift)
                                NormalizePattern(pattern, -patternBeats);
                            else
                                NormalizePattern(pattern, -1);
                        }
                        else
                            Normalize(-1, !caps);
                        break;
                    }
                case Key.F3:
                    {
                        FindShortestBeatLongerThanPrevious();
                        break;
                    }
                case Key.F5:
                    {
                        JumpToFirst();
                        break;
                    }
                case Key.F6:
                    {
                        SnapToClosestBeat();
                        break;
                    }
                case Key.F7:
                    {
                        JumpToShortest();
                        break;
                    }
                case Key.F8:
                    {
                        JumpToLast();
                        break;
                    }
                case Key.Enter:
                    {
                        if (IsNumpadEnterKey(e))
                        {
                            if (control)
                                NormalizePattern(_previousPattern ?? new bool[] { true, true }, 0);
                            else
                                Normalize(0, !caps);
                        }
                        else
                        {
                            handled = false;
                        }
                        break;
                    }
                case Key.NumPad1:
                    {
                        AddPositionNow(0);
                        break;
                    }
                case Key.NumPad2:
                    {
                        AddPositionNow(50);
                        break;
                    }
                case Key.NumPad3:
                    {
                        AddPositionNow(99);
                        break;
                    }
                default:
                    handled = false;
                    break;
            }

            if (handled)
                e.Handled = true;
        }

        private void EnforceCommonBeatDuration()
        {
            List<TimeSpan> beatsToEvenOut = GetSelectedBeats();

            if (beatsToEvenOut.Count < 2)
                return;

            TimeSpan first = beatsToEvenOut.First();
            TimeSpan last = beatsToEvenOut.Last();
            TimeSpan duration = last - first;

            TimeSpan shortest = TimeSpan.MaxValue;

            for (int i = 1; i < beatsToEvenOut.Count; i++)
            {
                TimeSpan dur = beatsToEvenOut[i] - beatsToEvenOut[i - 1];
                if (dur < shortest)
                    shortest = dur;
            }

            List<TimeSpan> relativeTimeSpans = beatsToEvenOut.Select(b => b - first).ToList();

            int minBeats = beatsToEvenOut.Count - 1;
            int maxBeats = (int)Math.Ceiling(duration.Divide(shortest)) * 4;
            double minTotalDeviation = double.MaxValue;

            int bestBeats = -1;
            double singleMaxDeviation = double.MaxValue;

            for (int x = minBeats; x <= maxBeats; x++)
            {
                double totalDeviation = 0;
                double beatDuration = duration.TotalMilliseconds / x;
                double maxDeviation = 0;

                foreach (TimeSpan span in relativeTimeSpans)
                {
                    double deviation = Remainer(span.TotalMilliseconds, beatDuration);
                    if (deviation > beatDuration / 2.0)
                        deviation = beatDuration - deviation;

                    if (deviation > maxDeviation)
                        maxDeviation = deviation;

                    totalDeviation += deviation;

                    if (totalDeviation > minTotalDeviation)
                        break;
                }

                if (totalDeviation < minTotalDeviation)
                {
                    singleMaxDeviation = maxDeviation;
                    minTotalDeviation = totalDeviation;
                    bestBeats = x;
                }
            }

            TimeSpan bestDuration = duration.Divide(bestBeats);

            List<TimeSpan> newBeats = new List<TimeSpan>();

            double newBeatDuration = duration.TotalMilliseconds / bestBeats;
            double newMaxDeviation = 0;
            double newTotalDeviation = 0;

            foreach (TimeSpan span in relativeTimeSpans)
            {
                int multiplier = (int) Math.Floor(span.TotalMilliseconds / newBeatDuration);
                double deviation = Remainer(span.TotalMilliseconds, newBeatDuration);
                if (deviation > newBeatDuration / 2.0)
                {
                    deviation = newBeatDuration - deviation;
                    multiplier++;
                }

                newTotalDeviation += deviation;

                if (deviation > newMaxDeviation)
                    newMaxDeviation = deviation;

                TimeSpan newSpan = TimeSpan.FromTicks((long)(duration.Ticks * multiplier / (double)bestBeats));
                newBeats.Add(newSpan);
            }

            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            List<TimeSpan> otherBeats = Beats.Where(t => t < tBegin || t > tEnd).ToList();
            otherBeats.AddRange(newBeats.Select(b => b + first));
            SetAllBeats(otherBeats);

            Fadeout.SetText($"Common Beat Duration {bestDuration.TotalMilliseconds:f2}ms, Deviation max: {newMaxDeviation:f2}ms, total: {newTotalDeviation}ms", TimeSpan.FromSeconds(8));
        }

        private double Remainer(double number, double divisor)
        {
            double result = number - Math.Floor(number / divisor) * divisor;
            if (result < 0)
                return Math.Abs(result);
            return result;
        }

        private void FindCommonBeatDuration()
        {
            List<TimeSpan> beatsToEvenOut = GetSelectedBeats();

            if (beatsToEvenOut.Count < 2)
                return;

            TimeSpan first = beatsToEvenOut.First();
            TimeSpan last = beatsToEvenOut.Last();
            TimeSpan duration = last - first;

            TimeSpan shortest = TimeSpan.MaxValue;

            for (int i = 1; i < beatsToEvenOut.Count; i++)
            {
                TimeSpan dur = beatsToEvenOut[i] - beatsToEvenOut[i - 1];
                if (dur < shortest)
                    shortest = dur;
            }

            List<TimeSpan> relativeTimeSpans = beatsToEvenOut.Select(b => b - first).ToList();

            int minBeats = beatsToEvenOut.Count - 1;
            int maxBeats = (int) Math.Ceiling(duration.Divide(shortest)) * 4;
            double minTotalDeviation = double.MaxValue;

            int bestBeats = -1;
            double singleMaxDeviation = double.MaxValue;

            for (int x = minBeats; x <= maxBeats; x++)
            {
                double totalDeviation = 0;
                double beatDuration = duration.TotalMilliseconds / x;
                double maxDeviation = 0;
                
                foreach (TimeSpan span in relativeTimeSpans)
                {
                    double deviation = Remainer(span.TotalMilliseconds, beatDuration);
                    if (deviation > beatDuration / 2.0)
                        deviation = beatDuration - deviation;

                    if (deviation > maxDeviation)
                        maxDeviation = deviation;

                    totalDeviation += deviation;

                    if (totalDeviation > minTotalDeviation)
                        break;
                }

                if (totalDeviation < minTotalDeviation)
                {
                    singleMaxDeviation = maxDeviation;
                    minTotalDeviation = totalDeviation;
                    bestBeats = x;
                }
            }

            TimeSpan bestDuration = duration.Divide(bestBeats);

            Fadeout.SetText($"Best Common Beat Duration {bestDuration.TotalMilliseconds:f2}ms, Deviation max: {singleMaxDeviation:f2}ms, Total: {minTotalDeviation:f0}", TimeSpan.FromSeconds(8));
        }

        private void EqualizeBeatLengths()
        {
            List<TimeSpan> beatsToEvenOut = GetSelectedBeats();

            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            List<TimeSpan> otherBeats = Beats.Where(t => t < tBegin || t > tEnd).ToList();

            if (beatsToEvenOut.Count <= 2)
                return;

            const double factor = 0.1;

            for (int i = 0; i < beatsToEvenOut.Count; i++)
            {
                if (i == 0 || i == beatsToEvenOut.Count - 1)
                {
                    otherBeats.Add(beatsToEvenOut[i]);
                }
                else
                {
                    TimeSpan beatBefore = beatsToEvenOut[i] - beatsToEvenOut[i - 1];
                    TimeSpan beatAfter = beatsToEvenOut[i + 1] - beatsToEvenOut[i];
                    TimeSpan average = (beatBefore + beatAfter).Divide(2);

                    TimeSpan newLength = beatBefore.Multiply(1.0 - factor) + average.Multiply(factor);
                    TimeSpan absolutePosition = beatsToEvenOut[i - 1] + newLength;
                    otherBeats.Add(absolutePosition);
                }
            }

            Fadeout.SetText($"Beats equalized by {factor:p}", TimeSpan.FromSeconds(4));

            SetAllBeats(otherBeats);
        }

        TimeSpan _previouslyShortest = TimeSpan.MaxValue;
        private bool[] _previousPattern;
        private ConversionSettings _previousConversionSettings;

        private void FindShortestBeatLongerThanPrevious()
        {
            if (Beats == null) return;
            if (Beats.Count < 2) return;

            TimeSpan shortest = TimeSpan.MaxValue;
            TimeSpan position = TimeSpan.Zero;

            for (int i = 0; i < Beats.Count - 1; i++)
            {
                TimeSpan duration = Beats[i + 1] - Beats[i];
                if ((duration <= shortest) && ((duration > _previouslyShortest) || (_previouslyShortest == TimeSpan.MaxValue)))
                {
                    shortest = duration;
                    position = Beats[i];
                }
            }

            _previouslyShortest = shortest;

            videoPlayer.SetPosition(position);
            Fadeout.SetText(shortest.TotalMilliseconds.ToString("f0") + "ms", TimeSpan.FromSeconds(3));
        }

        private void AddPositionNow(byte position)
        {
            TimeSpan timeSpan = videoPlayer.GetPosition();
            if (Positions == null)
                Positions = new PositionCollection();

            Positions.Add(new TimedPosition
            {
                Position = position,
                TimeStamp = timeSpan
            });
        }

        private void SnapToClosestBeat()
        {
            videoPlayer.SetPosition(GetClosestBeat(videoPlayer.GetPosition()));
        }

        private void GotoNextBookMark()
        {
            var pos = videoPlayer.GetPosition();
            if (!Bookmarks.Any(t => t > pos))
                return;

            TimeSpan bookmark = Bookmarks.First(t => t > pos);
            lstBookmarks.SelectedItem = bookmark;
            videoPlayer.SetPosition(bookmark);
        }

        private void GotoPreviousBookMark()
        {
            var pos = videoPlayer.GetPosition();
            if (!Bookmarks.Any(t => t < pos))
                return;

            TimeSpan bookmark = Bookmarks.FindLast(t => t < pos);
            lstBookmarks.SelectedItem = bookmark;
            videoPlayer.SetPosition(bookmark);
        }

        public void SetTitle(string filePath)
        {
            Title = "ScriptPlayer Video Sync - " + Path.GetFileNameWithoutExtension(filePath);
        }

        private void BeatBar_OnTimeMouseDown(object sender, TimeSpan e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                RemoveClosestBeat(e);
            else
            {
                Beats.Add(e);
                Beats.Sort();
            }

            BeatBar.InvalidateVisual();
        }

        private void RemoveClosestBeat(TimeSpan timeSpan)
        {
            if (Beats.Count < 1) return;
            TimeSpan closest = GetClosestBeat(timeSpan);
            Beats.Remove(closest);
        }

        private TimeSpan GetClosestBeat(TimeSpan timeSpan)
        {
            return Beats.OrderBy(b => Math.Abs(b.Ticks - timeSpan.Ticks)).First();
        }

        private static bool IsNumpadEnterKey(KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return false;

            // To understand the following UGLY implementation please check this MSDN link. Suggested workaround to differentiate between the Return key and Enter key.
            // https://social.msdn.microsoft.com/Forums/vstudio/en-US/b59e38f1-38a1-4da9-97ab-c9a648e60af5/whats-the-difference-between-keyenter-and-keyreturn?forum=wpf
            try
            {
                return (bool)typeof(KeyEventArgs).InvokeMember("IsExtendedKey", BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Instance, null, e, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return false;
        }

        private void mnuSetMarkerMode_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null)
                return;

            TimeLineHeader header = FindPlacementTarget(menuItem) as TimeLineHeader;
            if (header == null) return;

            header.MarkerMode = (MarkerMode)menuItem.Tag;
        }

        private UIElement FindPlacementTarget(MenuItem sender)
        {
            DependencyObject currentElement = sender;
            while (currentElement != null)
            {
                ContextMenu element = currentElement as ContextMenu;
                if (element != null)
                    return element.PlacementTarget;
                currentElement = VisualTreeHelper.GetParent(currentElement);
            }
            return null;
        }

        private void BeatBar_OnTimeMouseRightDown(object sender, TimeSpan e)
        {
            if (KeepMarkerDuration())
            {
                TimeSpan currentDuration = TimeSpan.FromTicks(Math.Abs(_marker1.Ticks - _marker2.Ticks));

                SetMarker(1, e);
                SetMarker(2, e + currentDuration);
            }
            else
            {
                SetMarker(1, e);
                SetMarker(2, e);
            }
        }

        private void BeatBar_OnTimeMouseRightMove(object sender, TimeSpan e)
        {
            if (KeepMarkerDuration())
            {
                TimeSpan currentDuration = TimeSpan.FromTicks(Math.Abs(_marker1.Ticks - _marker2.Ticks));

                SetMarker(1, e);
                SetMarker(2, e + currentDuration);
            }
            else
            {
                SetMarker(2, e);
            }
        }

        private void BeatBar_OnTimeMouseRightUp(object sender, TimeSpan e)
        {
            if (KeepMarkerDuration())
            {
                TimeSpan currentDuration = TimeSpan.FromTicks(Math.Abs(_marker1.Ticks - _marker2.Ticks));

                SetMarker(1, e);
                SetMarker(2, e + currentDuration);
            }
            else
            {
                SetMarker(2, e);
            }
        }

        private bool KeepMarkerDuration()
        {
            if (_marker1 == TimeSpan.MinValue) return false;
            if (_marker2 == TimeSpan.MinValue) return false;
            if (_marker1 == _marker2) return false;

            return Keyboard.IsKeyToggled(Key.Scroll);
        }

        private void btnLoadBookmarks_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Filter = "Log Files|*.log;*.txt|All Files|*.*"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            string[] lines = File.ReadAllLines(dialog.FileName);

            List<TimeSpan> newBookmarks = new List<TimeSpan>();

            foreach (string line in lines)
            {
                TimeSpan t;
                if (TimeSpan.TryParse(line, out t))
                    newBookmarks.Add(t);
            }

            newBookmarks.Sort();

            Bookmarks = newBookmarks;
        }

        private void CckRTL_OnChecked(object sender, RoutedEventArgs e)
        {
            BeatBar.FlowDirection = FlowDirection.RightToLeft;
        }

        private void CckRTL_OnUnchecked(object sender, RoutedEventArgs e)
        {
            BeatBar.FlowDirection = FlowDirection.LeftToRight;
        }

        private void mnuShiftSelected_Click(object sender, RoutedEventArgs e)
        {
            double shiftBy = GetDouble(0.0);
            if (Double.IsNaN(shiftBy))
                return;

            TimeSpan shift = TimeSpan.FromMilliseconds((int)shiftBy);

            ShiftSelection(shift);
        }

        private void ShiftSelection(TimeSpan shift)
        {
            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            List<TimeSpan> beatsToShift = Beats.GetBeats(tBegin, tEnd).Select(b => b + shift).ToList();
            List<TimeSpan> otherBeats = Beats.Where(t => t < tBegin || t > tEnd).ToList();

            List<TimedPosition> positionsToShift = Positions.Where(t => t.TimeStamp >= tBegin && t.TimeStamp <= tEnd).Select(b => new TimedPosition { Position = b.Position, TimeStamp = b.TimeStamp + shift }).ToList();
            List<TimedPosition> otherPositions = Positions.Where(t => t.TimeStamp < tBegin || t.TimeStamp > tEnd).ToList();

            SetAllBeats(otherBeats.Concat(beatsToShift));
            Positions = new PositionCollection(otherPositions.Concat(positionsToShift));

            _marker1 += shift;
            _marker2 += shift;

            BeatBar.Marker1 = _marker1;
            BeatBar.Marker2 = _marker2;
        }

        private void mnuLoadVorze_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "Vorze Script|*.csv" };

            if (dialog.ShowDialog(this) != true)
                return;

            VorzeScriptLoader loader = new VorzeScriptLoader();
            List<VorzeScriptAction> actions = loader.Load(dialog.FileName).Cast<VorzeScriptAction>().ToList();

            List<FunScriptAction> funActions = VorzeToFunscriptConverter.Convert(actions);

            var pos = funActions.Select(s => new TimedPosition()
            {
                TimeStamp = s.TimeStamp,
                Position = s.Position
            });

            Positions = new PositionCollection(pos);

            /*
            SaveFileDialog dialog2 = new SaveFileDialog
            {
                Filter = "Funscript|*.funscript|All Files|*.*",
                FileName = Path.GetFileNameWithoutExtension(_videoFile)
            };

            if (dialog2.ShowDialog(this) != true) return;

            FunScriptFile script = new FunScriptFile
            {
                Inverted = false,
                Range = 90,
                Version = new Version(1, 0),
                Actions = funActions
            };

            string content = JsonConvert.SerializeObject(script);

            File.WriteAllText(dialog2.FileName, content, new UTF8Encoding(false));
            */
        }

        private void mnuBeatsToPositions_Click(object sender, RoutedEventArgs e)
        {
            BeatConversionSettingsDialog dialog = new BeatConversionSettingsDialog(_previousConversionSettings);
            dialog.Owner = this;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (dialog.ShowDialog() != true)
                return;

            _previousConversionSettings = dialog.Result;

            ConvertBeats(_previousConversionSettings);
        }

        private void mnuSelectedBeatsToPositions_Click(object sender, RoutedEventArgs e)
        {
            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            BeatConversionSettingsDialog dialog = new BeatConversionSettingsDialog(_previousConversionSettings);
            dialog.Owner = this;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (dialog.ShowDialog() != true)
                return;

            _previousConversionSettings = dialog.Result;

            List<TimeSpan> beatsToConvert = Beats.GetBeats(tBegin,tEnd).ToList();

            if (beatsToConvert.Count == 0)
                return;

            List<TimedPosition> otherPositions = Positions.Where(t => t.TimeStamp < tBegin || t.TimeStamp > tEnd).ToList();

            int startIndex = Beats.IndexOf(beatsToConvert.First());

            var convertedPositions = BeatsToFunScriptConverter.Convert(beatsToConvert, _previousConversionSettings, startIndex);

            Positions = new PositionCollection(otherPositions.Concat(convertedPositions.Select(f => new TimedPosition
            {
                Position = f.Position,
                TimeStamp = f.TimeStamp
            })));
        }

        private void ConvertBeats(ConversionSettings settings)
        {
            var funscript = BeatsToFunScriptConverter.Convert(Beats, settings);

            Positions = new PositionCollection(funscript.Select(f => new TimedPosition
            {
                Position = f.Position,
                TimeStamp = f.TimeStamp
            }));
        }

        private void mnuLoadOtt_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "One Touch Script|*.ott" };

            if (dialog.ShowDialog(this) != true)
                return;

            RealTouchScriptLoader loader = new RealTouchScriptLoader();
            List<FunScriptAction> actions = loader.Load(dialog.FileName).Cast<FunScriptAction>().ToList();

            var pos = actions.Select(s => new TimedPosition
            {
                TimeStamp = s.TimeStamp,
                Position = s.Position
            });

            Positions = new PositionCollection(pos);
        }

        private void btnFirstMarker_Click(object sender, RoutedEventArgs e)
        {
            GotoSelectionBegin();
        }

        private void GotoSelectionBegin()
        {
            videoPlayer.SetPosition(_marker1 < _marker2 ? _marker1 : _marker2);
        }

        private void btnSecondMarker_Click(object sender, RoutedEventArgs e)
        {
            GotoSelectionEnd();
        }

        private void GotoSelectionEnd()
        {
            videoPlayer.SetPosition(_marker1 > _marker2 ? _marker1 : _marker2);
        }

        private void DirectInputControl_OnBeat(object sender, EventArgs e)
        {
            Beats.Add(videoPlayer.GetPosition());
            SetAllBeats(Beats);
        }

        private void mnuPositionsToBeats_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, "Are you sure you want to re-convert ALL positions to beats?",
                    "Confirmation required", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            SetAllBeats(Positions.Select(p => p.TimeStamp));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            RefreshHeatMap();
        }

        private void RefreshHeatMap()
        {
            if (videoPlayer == null)
                return;

            Brush heatmap = HeatMapGenerator.Generate2(Beats.ToList(), TimeSpan.FromSeconds(10), TimeSpan.Zero, videoPlayer.Duration);
            SeekBar.Background = heatmap;
        }

        private void MnuSaveThumbnails_Click(object sender, RoutedEventArgs e)
        {
            VideoThumbnailCollection thumbs = SeekBar.Thumbnails;
            if (thumbs == null) return;

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Video Thumbnails|*.thumbs";

            if (dialog.ShowDialog(this) != true) return;

            using (FileStream stream = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write))
                thumbs.Save(stream);
        }

        private void MnuLoadThumbnails_Click(object sender, RoutedEventArgs e)
        {
            VideoThumbnailCollection thumbs = new VideoThumbnailCollection();
            
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Video Thumbnails|*.thumbs";

            if (dialog.ShowDialog(this) != true) return;

            using (FileStream stream = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read))
                thumbs.Load(stream);

            SeekBar.Thumbnails = thumbs;
        }

        private void BtnApplySoundShiftToSelection_Click(object sender, RoutedEventArgs e)
        {
            ShiftSelection(TimeSpan.FromMilliseconds(BeatBar.SoundDelay));
            BeatBar.SoundDelay = 0;
        }

        private void BtnYMinus_Click(object sender, RoutedEventArgs e)
        {
            SampleY = Math.Max(0, SampleY - 1);
        }

        private void BtnYPlus_Click(object sender, RoutedEventArgs e)
        {
            SampleY++;
        }

        private void BtnWMinus_Click(object sender, RoutedEventArgs e)
        {
            SampleW = Math.Max(1, SampleW - 1);
        }

        private void BtnWPlus_Click(object sender, RoutedEventArgs e)
        {
            SampleW++;
        }

        private void BtnXMinus_Click(object sender, RoutedEventArgs e)
        {
            SampleX = Math.Max(0, SampleX - 1);
        }

        private void BtnXPlus_Click(object sender, RoutedEventArgs e)
        {
            SampleX++;
        }

        private void BtnHMinus_Click(object sender, RoutedEventArgs e)
        {
            SampleH = Math.Max(1, SampleH - 1);
        }

        private void BtnHPlus_Click(object sender, RoutedEventArgs e)
        {
            SampleH++;
        }
    }
}

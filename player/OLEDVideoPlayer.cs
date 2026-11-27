using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace OledVideoSync
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var app = new Application();
            app.Run(new PlayerWindow(args.Length > 0 ? args[0] : null));
        }
    }

    internal sealed class PlayerWindow : Window
    {
        private const int ClipMs = 19400;
        private const string ClipName = "gayanalewke - 7707286013323889940.mp4";
        private const string OriginalClipName = "7707286013323889940_original.mp4";
        private const string BundledClipResource = "OledVideoSync.CompatibleClip.mp4";
        private readonly MediaElement video = new MediaElement();
        private readonly Button play = new Button();
        private readonly Button replay = new Button();
        private readonly Button fullScreenButton = new Button();
        private readonly ComboBox ports = new ComboBox();
        private readonly TextBlock connection = new TextBlock();
        private readonly TextBlock time = new TextBlock();
        private readonly Slider seek = new Slider();
        private readonly DispatcherTimer timer = new DispatcherTimer();
        private readonly Grid page = new Grid();
        private readonly WrapPanel toolbar = new WrapPanel();
        private readonly StackPanel controls = new StackPanel();
        private readonly Border videoFrame = new Border { Background = Brushes.Black };
        private readonly Border controlTray = new Border();
        private readonly Stopwatch pointerClock = Stopwatch.StartNew();
        private bool fullScreen;
        private Rect windowedBounds;
        private WindowState windowedState;
        private long lastPointerMs;
        private SerialPort link;
        private bool boardReady, mediaReady, playing, ended, updatingSlider;
        private int lastSent = -1;
        private string serialInput = "";
        private string bundledVideoPath;
        private DateTime nextProbe, probeDeadline;

        internal PlayerWindow(string path)
        {
            Title = "OLED Lyrics Player";
            Width = 620;
            Height = 770;
            MinWidth = 490;
            MinHeight = 350;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            UseLayoutRounding = true;
            Background = new SolidColorBrush(Color.FromRgb(19, 23, 29));
            Foreground = Brushes.White;
            FontSize = 14;

            page.Margin = new Thickness(16);
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = page;

            toolbar.Margin = new Thickness(0, 0, 0, 12);
            page.Children.Add(toolbar);
            AddButton(toolbar, new Button(), "Open video", OpenVideo);
            AddButton(toolbar, new Button(), "Use bundled clip", LoadBundledVideo);
            ports.Width = 80;
            ports.Margin = new Thickness(0, 0, 8, 0);
            ports.VerticalContentAlignment = VerticalAlignment.Center;
            ports.DropDownOpened += delegate { RefreshPorts(); };
            toolbar.Children.Add(ports);
            AddButton(toolbar, new Button(), "Connect", delegate { Connect((string)ports.SelectedItem); });
            connection.VerticalAlignment = VerticalAlignment.Center;
            toolbar.Children.Add(connection);

            controlTray.Margin = new Thickness(0, 12, 0, 0);
            controlTray.Child = controls;
            Grid.SetRow(controlTray, 2);
            Panel.SetZIndex(controlTray, 1);
            page.Children.Add(controlTray);
            seek.Minimum = 0;
            seek.Maximum = ClipMs;
            seek.IsMoveToPointEnabled = true;
            seek.ValueChanged += delegate
            {
                if (!updatingSlider && mediaReady) SeekTo((int)seek.Value);
            };
            controls.Children.Add(seek);
            var transport = new StackPanel { Orientation = Orientation.Horizontal };
            controls.Children.Add(transport);
            AddButton(transport, play, "Play", TogglePlayback);
            AddButton(transport, replay, "Replay", delegate { SeekTo(0); StartPlayback(); });
            AddButton(transport, fullScreenButton, "Full screen", ToggleFullScreen);
            fullScreenButton.ToolTip = "F11 toggles full screen. Esc exits.";
            time.VerticalAlignment = VerticalAlignment.Center;
            transport.Children.Add(time);

            video.LoadedBehavior = MediaState.Manual;
            video.UnloadedBehavior = MediaState.Close;
            video.ScrubbingEnabled = true;
            video.Stretch = Stretch.Uniform;
            video.MediaOpened += delegate
            {
                video.Pause();
                // Some Windows decoders report MP4 duration in whole seconds.
                if (!video.NaturalDuration.HasTimeSpan ||
                    Math.Abs(video.NaturalDuration.TimeSpan.TotalMilliseconds - ClipMs) > 1000)
                {
                    ShowVideoHelp("Choose the 19.40-second clip or click Use bundled clip.");
                    return;
                }
                video.Position = TimeSpan.Zero;
                video.IsMuted = false;
                mediaReady = true;
                SetClock(0);
                UpdateControls();
            };
            video.MediaEnded += delegate
            {
                playing = false;
                ended = true;
                SetClock(ClipMs);
                UpdateControls();
            };
            video.MediaFailed += delegate
            {
                mediaReady = playing = ended = false;
                UpdateControls();
                ShowVideoHelp("Windows couldn't decode this file. Click Use bundled clip for the compatible copy.");
            };
            videoFrame.Child = video;
            Grid.SetRow(videoFrame, 1);
            page.Children.Add(videoFrame);
            PreviewKeyDown += HandlePlayerKeys;
            PreviewMouseMove += delegate { if (fullScreen) RevealFullScreenControls(); };

            timer.Interval = TimeSpan.FromMilliseconds(35);
            timer.Tick += delegate
            {
                PollBoard();
                if (mediaReady) SetClock(PositionMs());
                if (fullScreen && playing && !controlTray.IsMouseOver &&
                    pointerClock.ElapsedMilliseconds - lastPointerMs >= 2400)
                    controlTray.Visibility = Visibility.Collapsed;
            };
            Loaded += delegate
            {
                RefreshPorts();
                Connect((string)ports.SelectedItem);
                if (path != null && File.Exists(path)) LoadVideo(path);
                else LoadBundledVideo();
                timer.Start();
            };
            Closed += delegate
            {
                timer.Stop();
                video.Close();
                video.Source = null;
                CloseBoard();
                if (bundledVideoPath != null)
                {
                    try { File.Delete(bundledVideoPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            };
            SetClock(0);
            UpdateControls();
        }

        private static void AddButton(Panel panel, Button button, string label, Action action)
        {
            button.Content = label;
            button.Padding = new Thickness(12, 5, 12, 5);
            button.Margin = new Thickness(0, 0, 8, 0);
            button.Click += delegate { action(); };
            panel.Children.Add(button);
        }

        private void HandlePlayerKeys(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11 || (fullScreen && e.Key == Key.Escape))
            {
                if (!e.IsRepeat) ToggleFullScreen();
                e.Handled = true;
            }
            else if (fullScreen && e.Key == Key.Space && play.IsEnabled)
            {
                if (!e.IsRepeat) TogglePlayback();
                e.Handled = true;
            }
        }

        private void RevealFullScreenControls()
        {
            lastPointerMs = pointerClock.ElapsedMilliseconds;
            controlTray.Visibility = Visibility.Visible;
        }

        private void ToggleFullScreen()
        {
            if (!fullScreen)
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                IntPtr monitor = MonitorFromWindow(handle, 2); // Nearest monitor.
                var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                bool haveBounds = GetMonitorInfo(monitor, ref info);
                windowedState = WindowState;
                windowedBounds = WindowState == WindowState.Normal
                    ? new Rect(Left, Top, ActualWidth, ActualHeight)
                    : RestoreBounds;

                fullScreen = true;
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                page.Margin = new Thickness(0);
                toolbar.Visibility = Visibility.Collapsed;
                Grid.SetRow(videoFrame, 0);
                Grid.SetRowSpan(videoFrame, 3);
                controlTray.Margin = new Thickness(0);
                controlTray.Padding = new Thickness(16, 8, 16, 12);
                controlTray.Background = new SolidColorBrush(Color.FromArgb(224, 19, 23, 29));
                fullScreenButton.Content = "Exit full screen";
                RevealFullScreenControls();

                NativeRect bounds = info.Monitor;
                const uint PlacementFlags = 0x0004 | 0x0010 | 0x0020; // Keep Z-order, don't activate, refresh frame.
                if (!haveBounds || !SetWindowPos(handle, IntPtr.Zero, bounds.Left, bounds.Top,
                                                 bounds.Right - bounds.Left, bounds.Bottom - bounds.Top,
                                                 PlacementFlags))
                    WindowState = WindowState.Maximized;
            }
            else
            {
                fullScreen = false;
                WindowState = WindowState.Normal;
                ResizeMode = ResizeMode.CanResize;
                WindowStyle = WindowStyle.SingleBorderWindow;
                Left = windowedBounds.Left;
                Top = windowedBounds.Top;
                Width = windowedBounds.Width;
                Height = windowedBounds.Height;
                WindowState = windowedState;
                page.Margin = new Thickness(16);
                toolbar.Visibility = Visibility.Visible;
                Grid.SetRowSpan(videoFrame, 1);
                Grid.SetRow(videoFrame, 1);
                controlTray.Margin = new Thickness(0, 12, 0, 0);
                controlTray.Padding = new Thickness(0);
                controlTray.Background = null;
                controlTray.Visibility = Visibility.Visible;
                fullScreenButton.Content = "Full screen";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { internal int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo { internal int Size; internal NativeRect Monitor, Work; internal uint Flags; }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y,
                                                int width, int height, uint flags);

        private void OpenVideo()
        {
            var dialog = new OpenFileDialog { Filter = "MP4 video (*.mp4)|*.mp4", FileName = ClipName };
            if (dialog.ShowDialog(this) == true) LoadVideo(dialog.FileName);
        }

        private void ShowVideoHelp(string message)
        {
            if (fullScreen) ToggleFullScreen();
            MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadBundledVideo()
        {
            string newPath = null;
            try
            {
                if (bundledVideoPath == null || !File.Exists(bundledVideoPath))
                {
                    using (Stream resource = typeof(PlayerWindow).Assembly.GetManifestResourceStream(BundledClipResource))
                    {
                        if (resource == null) throw new IOException("Bundled clip missing.");
                        newPath = Path.Combine(Path.GetTempPath(), "OLEDVideoSync-" + Guid.NewGuid().ToString("N") + ".mp4");
                        using (var file = new FileStream(newPath, FileMode.CreateNew, FileAccess.Write))
                            resource.CopyTo(file);
                    }
                    bundledVideoPath = newPath;
                }
                LoadVideo(bundledVideoPath);
            }
            catch (Exception e)
            {
                if (!(e is IOException || e is UnauthorizedAccessException)) throw;
                if (newPath != null && newPath != bundledVideoPath)
                {
                    try { File.Delete(newPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                ShowVideoHelp("The bundled clip couldn't be prepared. Open an H.264 MP4 copy of the 19.40-second video.");
            }
        }

        private void LoadVideo(string path)
        {
            string filename = Path.GetFileName(path);
            if (String.Equals(filename, OriginalClipName, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(filename, ClipName, StringComparison.OrdinalIgnoreCase))
            {
                LoadBundledVideo();
                return;
            }
            mediaReady = playing = ended = false;
            video.Close();
            video.IsMuted = true;
            video.Source = new Uri(Path.GetFullPath(path));
            SetClock(0);
            UpdateControls();
            video.Play();
        }

        private int PositionMs()
        {
            return ended ? ClipMs : Math.Max(0, Math.Min(ClipMs, (int)video.Position.TotalMilliseconds));
        }

        private void SeekTo(int milliseconds)
        {
            ended = milliseconds == ClipMs;
            video.Position = TimeSpan.FromMilliseconds(milliseconds);
            if (ended && playing) video.Pause();
            if (ended) playing = false;
            SetClock(milliseconds);
            UpdateControls();
        }

        private void TogglePlayback()
        {
            if (!playing) { StartPlayback(); return; }
            video.Pause();
            playing = false;
            SetClock(PositionMs());
            UpdateControls();
        }

        private void StartPlayback()
        {
            if (!mediaReady || !boardReady) return;
            if (ended) SeekTo(0);
            video.Play();
            playing = true;
            UpdateControls();
        }

        private void SetClock(int milliseconds)
        {
            updatingSlider = true;
            seek.Value = milliseconds;
            updatingSlider = false;
            time.Text = TimeSpan.FromMilliseconds(milliseconds).ToString(@"m\:ss\.ff") + " / 0:19.40";
            if (boardReady && lastSent != milliseconds)
            {
                if (Send("T" + milliseconds.ToString(CultureInfo.InvariantCulture) + "\n"))
                    lastSent = milliseconds;
            }
        }

        private void UpdateControls()
        {
            if (fullScreen) RevealFullScreenControls();
            play.Content = playing ? "Pause" : "Play";
            play.IsEnabled = mediaReady && (boardReady || playing);
            replay.IsEnabled = mediaReady && boardReady;
            seek.IsEnabled = mediaReady;
        }

        private void RefreshPorts()
        {
            var previous = (string)ports.SelectedItem;
            var available = SerialPort.GetPortNames();
            Array.Sort(available, StringComparer.OrdinalIgnoreCase);
            ports.ItemsSource = available;
            if (Array.IndexOf(available, previous) >= 0) ports.SelectedItem = previous;
            else if (Array.IndexOf(available, "COM3") >= 0) ports.SelectedItem = "COM3";
            else if (available.Length == 1) ports.SelectedIndex = 0;
        }

        private void Connect(string name)
        {
            if (playing) TogglePlayback();
            CloseBoard();
            if (name == null)
            {
                connection.Text = "Select the Mega's USB port";
                return;
            }
            try
            {
                link = new SerialPort(name, 115200) { WriteTimeout = 180, DtrEnable = true };
                link.Open();
                serialInput = "";
                // Opening USB serial resets the Mega. Keep the line quiet
                // while its bootloader waits to decide whether to run the sketch.
                nextProbe = DateTime.UtcNow.AddSeconds(3);
                probeDeadline = nextProbe.AddSeconds(4);
                connection.Text = "Connecting to " + name + "…";
            }
            catch (Exception e)
            {
                if (!(e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)) throw;
                FailConnection("Port unavailable. Close Serial Monitor and reconnect.");
            }
        }

        private void PollBoard()
        {
            if (link == null) return;
            try
            {
                serialInput += link.ReadExisting();
                if (!boardReady && serialInput.Contains("OLED SYNC 1"))
                {
                    boardReady = true;
                    connection.Text = "OLED connected on " + link.PortName;
                    UpdateControls();
                }
                if (serialInput.Length > 256) serialInput = serialInput.Substring(serialInput.Length - 32);
                if (boardReady) return;
                DateTime now = DateTime.UtcNow;
                if (now >= probeDeadline) FailConnection("Mega sync sketch did not respond.");
                else if (now >= nextProbe)
                {
                    Send("?");
                    nextProbe = now.AddSeconds(1);
                }
            }
            catch (Exception e)
            {
                if (!(e is IOException || e is InvalidOperationException)) throw;
                FailConnection("USB disconnected. Reconnect the Mega.");
            }
        }

        private bool Send(string value)
        {
            try { link.Write(value); return true; }
            catch (Exception e)
            {
                if (!(e is IOException || e is InvalidOperationException || e is TimeoutException)) throw;
                FailConnection("USB disconnected. Reconnect the Mega.");
                return false;
            }
        }

        private void FailConnection(string message)
        {
            CloseBoard();
            connection.Text = message;
            if (!playing) return;
            video.Pause();
            playing = false;
            UpdateControls();
        }

        private void CloseBoard()
        {
            boardReady = false;
            lastSent = -1;
            if (link != null)
            {
                try { link.Dispose(); }
                catch (IOException) { }
                finally { link = null; }
            }
            UpdateControls();
        }
    }
}

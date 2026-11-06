
using System;
using System.IO;
using System.Linq;
using System.Threading;
using NAudio.Wave;

namespace ConsoleApp1
{
    internal class Program
    {
        static void Main()
        {
            Console.Title = "MISTOTIFY - Console Music Player";
            Console.CursorVisible = false;

            // Najdeme slozku Music primo vedle Program.cs
            string projectFolder = AppContext.BaseDirectory;

            string musicFolder = Path.GetFullPath(
                Path.Combine(projectFolder, @"..\..\..\Music"));

            Directory.CreateDirectory(musicFolder);

            string[] songs = Directory.GetFiles(musicFolder)
                .Where(file =>
                    file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file)
                .ToArray();

            if (songs.Length == 0)
            {
                Console.WriteLine("MISTOTIFY");
                Console.WriteLine("==============================");
                Console.WriteLine("Nenasli jsme zadne skladby!");
                Console.WriteLine();
                Console.WriteLine("Hledana slozka:");
                Console.WriteLine(musicFolder);
                Console.WriteLine();
                Console.WriteLine("Podporovane formaty: MP3, WAV");
                Console.ReadKey();
                return;
            }

            using MusicPlayer player = new MusicPlayer(songs);

            player.Play(0);

            while (true)
            {
                player.CheckPlayback();

                Draw(player);

                if (Console.KeyAvailable)
                {
                    ConsoleKey key = Console.ReadKey(true).Key;

                    switch (key)
                    {
                        case ConsoleKey.Spacebar:
                            player.TogglePause();
                            break;

                        case ConsoleKey.N:
                            player.Next();
                            break;

                        case ConsoleKey.P:
                            player.Previous();
                            break;

                        case ConsoleKey.UpArrow:
                            player.ChangeVolume(0.05f);
                            break;

                        case ConsoleKey.DownArrow:
                            player.ChangeVolume(-0.05f);
                            break;

                        case ConsoleKey.S:
                            player.Shuffle = !player.Shuffle;
                            break;

                        case ConsoleKey.Q:
                            return;
                    }
                }

                Thread.Sleep(150);
            }
        }

        static void Draw(MusicPlayer player)
        {
            Console.SetCursorPosition(0, 0);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("==============================================");
            Console.WriteLine("                 MISTOTIFY");
            Console.WriteLine("==============================================");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine(" NOW PLAYING:");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(" " + player.CurrentSong);
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine(" PLAYLIST:");

            for (int i = 0; i < player.Songs.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(
                    player.Songs[i]);

                string prefix = i == player.CurrentIndex ? " > " : "   ";

                Console.WriteLine(prefix + name);
            }

            Console.WriteLine();
            Console.WriteLine("----------------------------------------------");

            Console.WriteLine(
                " STATUS: " +
                (player.IsPaused ? "PAUSED" :
                player.IsPlaying ? "PLAYING" : "STOPPED"));

            Console.WriteLine(
                $" VOLUME: {(int)(player.Volume * 100)}%");

            Console.WriteLine(
                $" TIME: {player.Position:mm\\:ss} / {player.Duration:mm\\:ss}");

            int progress = player.Duration.TotalSeconds > 0
                ? (int)(player.Position.TotalSeconds /
                    player.Duration.TotalSeconds * 30)
                : 0;

            progress = Math.Clamp(progress, 0, 30);

            Console.Write("[");
            Console.Write(new string('#', progress));
            Console.Write(new string('-', 30 - progress));
            Console.WriteLine("]");

            Console.WriteLine(
                " SHUFFLE: " + (player.Shuffle ? "ON" : "OFF"));

            Console.WriteLine("----------------------------------------------");
            Console.WriteLine(" SPACE = Pause / Play");
            Console.WriteLine(" N     = Next song");
            Console.WriteLine(" P     = Previous song");
            Console.WriteLine(" UP    = Volume up");
            Console.WriteLine(" DOWN  = Volume down");
            Console.WriteLine(" S     = Shuffle ON/OFF");
            Console.WriteLine(" Q     = Exit");

            Console.WriteLine("==============================================");
        }
    }

    internal class MusicPlayer : IDisposable
    {
        public string[] Songs { get; }

        public int CurrentIndex { get; private set; }

        public float Volume { get; private set; } = 0.7f;

        public bool IsPaused { get; private set; }

        public bool Shuffle { get; set; }

        public bool IsPlaying =>
            output != null &&
            output.PlaybackState == PlaybackState.Playing;

        public string CurrentSong =>
            Path.GetFileName(Songs[CurrentIndex]);

        public TimeSpan Position =>
            reader?.CurrentTime ?? TimeSpan.Zero;

        public TimeSpan Duration =>
            reader?.TotalTime ?? TimeSpan.Zero;

        private WaveOutEvent? output;
        private AudioFileReader? reader;

        private bool trackStarted;

        private readonly Random random = new Random();

        public MusicPlayer(string[] songs)
        {
            Songs = songs;
        }

        public void Play(int index)
        {
            if (index < 0 || index >= Songs.Length)
                return;

            StopCurrent();

            CurrentIndex = index;

            try
            {
                reader = new AudioFileReader(Songs[index]);
                reader.Volume = Volume;

                output = new WaveOutEvent
                {
                    DesiredLatency = 150
                };

                output.Init(reader);
                output.Play();

                IsPaused = false;
                trackStarted = true;
            }
            catch (Exception ex)
            {
                Console.Clear();
                Console.WriteLine("CHYBA PREHRAVANI:");
                Console.WriteLine(ex.Message);
                StopCurrent();
            }
        }

        public void TogglePause()
        {
            if (output == null)
                return;

            if (IsPaused)
            {
                output.Play();
                IsPaused = false;
            }
            else if (IsPlaying)
            {
                output.Pause();
                IsPaused = true;
            }
        }

        public void Next()
        {
            int next;

            if (Shuffle && Songs.Length > 1)
            {
                do
                {
                    next = random.Next(Songs.Length);
                }
                while (next == CurrentIndex);
            }
            else
            {
                next = (CurrentIndex + 1) % Songs.Length;
            }

            Play(next);
        }

        public void Previous()
        {
            Play((CurrentIndex - 1 + Songs.Length) % Songs.Length);
        }

        public void ChangeVolume(float amount)
        {
            Volume = Math.Clamp(Volume + amount, 0f, 1f);

            if (reader != null)
                reader.Volume = Volume;
        }

        public void CheckPlayback()
        {
            if (trackStarted &&
                output != null &&
                output.PlaybackState == PlaybackState.Stopped)
            {
                trackStarted = false;
                Next();
            }
        }

        private void StopCurrent()
        {
            trackStarted = false;

            if (output != null)
            {
                output.Stop();
                output.Dispose();
                output = null;
            }

            if (reader != null)
            {
                reader.Dispose();
                reader = null;
            }

            IsPaused = false;
        }

        public void Dispose()
        {
            StopCurrent();
        }
    }
}

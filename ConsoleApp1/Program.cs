using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;

namespace ConsoleApp1
{
    internal class Program
    {
        static string musicFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MISTOTIFY", "Music");

        [STAThread]
        static void Main()
        {
            Console.Title = "MISTOTIFY";
            Console.CursorVisible = false;

            Directory.CreateDirectory(musicFolder);

            string[] songs = Directory.GetFiles(musicFolder)
                .Where(IsMusicFile)
                .OrderBy(x => x)
                .ToArray();

            using MusicPlayer player = new MusicPlayer(songs);

            if (player.Songs.Count > 0)
                player.Play(0);

            Console.Clear();

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
                        case ConsoleKey.A:
                            AddMusic(player);
                            break;
                        case ConsoleKey.Q:
                            return;
                    }
                }

                Thread.Sleep(150);
            }
        }

        static bool IsMusicFile(string file)
        {
            string ext = Path.GetExtension(file);

            return ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".wav", StringComparison.OrdinalIgnoreCase);
        }

        static void AddMusic(MusicPlayer player)
        {
            Console.Clear();
            Console.WriteLine("========== MISTOTIFY ==========");
            Console.WriteLine("1 - Add individual songs");
            Console.WriteLine("2 - Add entire folder");
            Console.WriteLine("ESC - Back");

            ConsoleKey choice = Console.ReadKey(true).Key;

            string[] imported = Array.Empty<string>();

            if (choice == ConsoleKey.D1 || choice == ConsoleKey.NumPad1)
                imported = PickMusicFiles();

            else if (choice == ConsoleKey.D2 || choice == ConsoleKey.NumPad2)
                imported = PickMusicFolder();

            else if (choice == ConsoleKey.Escape)
                return;

            int oldCount = player.Songs.Count;
            player.AddSongs(imported);

            if (oldCount == 0 && player.Songs.Count > 0)
                player.Play(0);

            Console.Clear();
            Console.WriteLine("IMPORT COMPLETED!");
            Console.WriteLine("Imported: " + imported.Length);
            Console.WriteLine("Total songs: " + player.Songs.Count);
            Console.WriteLine("Press any key...");
            Console.ReadKey(true);
            Console.Clear();
        }

        static string[] PickMusicFiles()
        {
            using OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select music",
                Filter = "Music (*.mp3;*.wav)|*.mp3;*.wav",
                Multiselect = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return Array.Empty<string>();

            return ImportFiles(dialog.FileNames);
        }

        static string[] PickMusicFolder()
        {
            using FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                Description = "Select music folder",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return Array.Empty<string>();

            List<string> files = new List<string>();
            SearchFolder(dialog.SelectedPath, files);

            return ImportFiles(files);
        }

        static void SearchFolder(string folder, List<string> files)
        {
            try
            {
                foreach (string file in Directory.GetFiles(folder))
                    if (IsMusicFile(file))
                        files.Add(file);

                foreach (string subfolder in Directory.GetDirectories(folder))
                    SearchFolder(subfolder, files);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        static string[] ImportFiles(IEnumerable<string> files)
        {
            Directory.CreateDirectory(musicFolder);
            List<string> imported = new List<string>();

            string library = Path.GetFullPath(musicFolder)
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            foreach (string source in files)
            {
                try
                {
                    if (!File.Exists(source) || !IsMusicFile(source))
                        continue;

                    string fullSource = Path.GetFullPath(source);

                    if (fullSource.StartsWith(library, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string name = Path.GetFileName(source);
                    string extension = Path.GetExtension(source);
                    string destination = Path.Combine(musicFolder, name);

                    if (File.Exists(destination))
                    {
                        string baseName = Path.GetFileNameWithoutExtension(name);
                        int number = 1;

                        do
                        {
                            destination = Path.Combine(
                                musicFolder,
                                $"{baseName} ({number}){extension}");
                            number++;
                        }
                        while (File.Exists(destination));
                    }

                    File.Copy(source, destination);
                    imported.Add(destination);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Import error: " + ex.Message);
                }
            }

            return imported.ToArray();
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
            Console.WriteLine("NOW PLAYING:");
            Console.WriteLine(player.CurrentSong);
            Console.WriteLine();
            Console.WriteLine("PLAYLIST:");

            if (player.Songs.Count == 0)
            {
                Console.WriteLine("No music found. Press A to add music.");
            }
            else
            {
                for (int i = 0; i < Math.Min(player.Songs.Count, 12); i++)
                {
                    string name = Path.GetFileNameWithoutExtension(player.Songs[i]);
                    Console.WriteLine((i == player.CurrentIndex ? " > " : "   ") + name);
                }
            }

            Console.WriteLine();
            Console.WriteLine("----------------------------------------------");
            Console.WriteLine("STATUS: " + (player.IsPaused ? "PAUSED" :
                player.IsPlaying ? "PLAYING" : "STOPPED"));
            Console.WriteLine($"VOLUME: {(int)(player.Volume * 100)}%");
            Console.WriteLine($"TIME: {player.Position:mm\\:ss} / {player.Duration:mm\\:ss}");

            int progress = player.Duration.TotalSeconds > 0
                ? (int)(player.Position.TotalSeconds / player.Duration.TotalSeconds * 30)
                : 0;

            progress = Math.Clamp(progress, 0, 30);

            Console.Write("[");
            Console.Write(new string('#', progress));
            Console.Write(new string('-', 30 - progress));
            Console.WriteLine("]");

            Console.WriteLine("SHUFFLE: " + (player.Shuffle ? "ON" : "OFF"));
            Console.WriteLine("----------------------------------------------");
            Console.WriteLine("SPACE = Pause / Play");
            Console.WriteLine("N = Next song");
            Console.WriteLine("P = Previous song");
            Console.WriteLine("UP / DOWN = Volume");
            Console.WriteLine("S = Shuffle");
            Console.WriteLine("A = Add music");
            Console.WriteLine("Q = Exit");
            Console.WriteLine("==============================================");
        }
    }

    internal class MusicPlayer : IDisposable
    {
        public List<string> Songs { get; }

        public int CurrentIndex { get; private set; } = -1;
        public float Volume { get; private set; } = 0.7f;
        public bool IsPaused { get; private set; }
        public bool Shuffle { get; set; }

        private WaveOut? output;
        private AudioFileReader? reader;
        private bool trackStarted;
        private readonly Random random = new Random();

        public bool IsPlaying =>
            output != null &&
            output.PlaybackState == PlaybackState.Playing;

        public string CurrentSong =>
            CurrentIndex >= 0 && CurrentIndex < Songs.Count
                ? Path.GetFileName(Songs[CurrentIndex])
                : "No song selected";

        public TimeSpan Position => reader?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan Duration => reader?.TotalTime ?? TimeSpan.Zero;

        public MusicPlayer(string[] songs)
        {
            Songs = songs.ToList();
        }

        public void AddSongs(IEnumerable<string> songs)
        {
            foreach (string song in songs)
                if (!Songs.Contains(song, StringComparer.OrdinalIgnoreCase))
                    Songs.Add(song);
        }

        public void Play(int index)
        {
            if (index < 0 || index >= Songs.Count)
                return;

            StopCurrent();
            CurrentIndex = index;

            try
            {
                reader = new AudioFileReader(Songs[index]);
                reader.Volume = Volume;

                output = new WaveOut();
                output.Init(reader);
                output.Play();

                IsPaused = false;
                trackStarted = true;
            }
            catch (Exception ex)
            {
                Console.Clear();
                Console.WriteLine("PLAYBACK ERROR:");
                Console.WriteLine(ex.Message);
                Console.ReadKey(true);
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
            if (Songs.Count == 0)
                return;

            int next;

            if (Shuffle && Songs.Count > 1)
            {
                do
                    next = random.Next(Songs.Count);
                while (next == CurrentIndex);
            }
            else
                next = (CurrentIndex + 1) % Songs.Count;

            Play(next);
        }

        public void Previous()
        {
            if (Songs.Count == 0)
                return;

            Play((CurrentIndex - 1 + Songs.Count) % Songs.Count);
        }

        public void ChangeVolume(float amount)
        {
            Volume = Math.Clamp(Volume + amount, 0f, 1f);

            if (reader != null)
                reader.Volume = Volume;
        }

        public void CheckPlayback()
        {
            if (trackStarted && output != null &&
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
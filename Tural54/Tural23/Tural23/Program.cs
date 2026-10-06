using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(3, 3);
    private static int _activeCount = 0;
    private static int _waitingCount = 0;
    private static int _completedCount = 0;

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.CursorVisible = false;

        var files = new[]
        {
            ("mp4_sample_file_25MB.mp4", 25),
            ("mp4_sample_file_50MB.mp4", 50),
            ("mp4_sample_file_100MB.mp4", 100),
            ("mp4_sample_file_200MB.mp4", 200)
        };

        var cts = new CancellationTokenSource();

        _ = Task.Run(() =>
        {
            if (Console.ReadKey(true).Key == ConsoleKey.Escape)
            {
                cts.Cancel();
            }
        });

        Console.Clear();
        DrawHeader();

        var tasks = new List<Task>();
        for (int i = 0; i < files.Length; i++)
        {
            int id = i + 1;
            string fileName = files[i].Item1;
            int totalSize = files[i].Item2;

            tasks.Add(DownloadFileAsync(id, fileName, totalSize, cts.Token));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            lock (Console.Out)
            {
                Console.SetCursorPosition(0, 15);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Bütün yükləmələr ləğv edildi.");
                Console.ResetColor();
            }
        }

        lock (Console.Out)
        {
            Console.SetCursorPosition(0, 16);
            Console.WriteLine("Proses başa çatdı. Çıxmaq üçün hər hansı bir düyməyə basın.");
        }
        Console.CursorVisible = true;
    }

    private static async Task DownloadFileAsync(int id, string fileName, int totalSizeMb, CancellationToken token)
    {
        Interlocked.Increment(ref _waitingCount);
        UpdateStatusLine();

        await _semaphore.WaitAsync(token);

        Interlocked.Decrement(ref _waitingCount);
        Interlocked.Increment(ref _activeCount);
        UpdateStatusLine();

        int downloadedMb = 0;
        while (downloadedMb < totalSizeMb)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(300, token);
            downloadedMb += Math.Max(1, totalSizeMb / 20);
            if (downloadedMb > totalSizeMb) downloadedMb = totalSizeMb;

            int percent = (downloadedMb * 100) / totalSizeMb;
            DrawProgressBar(id, fileName, percent, downloadedMb, totalSizeMb, "Downloading");
        }

        Interlocked.Decrement(ref _activeCount);
        Interlocked.Increment(ref _completedCount);
        UpdateStatusLine();
        DrawProgressBar(id, fileName, 100, totalSizeMb, totalSizeMb, "Completed");

        _semaphore.Release();
    }

    private static void DrawHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.SetCursorPosition(0, 0);
        Console.WriteLine("┌────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                    MULTI-THREAD VIDEO DOWNLOADER       │");
        Console.WriteLine("│                    Semaphore: MAXIMUM 3                │");
        Console.WriteLine("├────────────────────────────────────────────────────────┤");
        Console.ResetColor();
        Console.Write("│  ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("1-3");
        Console.ResetColor();
        Console.WriteLine("  → Downloading                                          │");

        Console.Write("│  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("4+ ");
        Console.ResetColor();
        Console.WriteLine("  → Waiting                                              │");

        Console.Write("│  ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("ESC");
        Console.ResetColor();
        Console.WriteLine("  → Cancel ALL                                           │");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("└────────────────────────────────────────────────────────┘");
        Console.ResetColor();
    }

    private static void DrawProgressBar(int id, string fileName, int percent, int currentMb, int totalMb, string status)
    {
        lock (Console.Out)
        {
            Console.SetCursorPosition(0, 9 + id);
            string bar = string.Concat(new string('█', percent / 4), new string('░', 25 - (percent / 4)));
            Console.Write($"#{id,-2} ");
            if (status == "Downloading")
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{status,-11}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"{status,-11}");
                Console.ResetColor();
            }

            Console.Write($" [{bar}] {percent,3}%  {currentMb,4} MB/{totalMb} MB   \n");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"    {fileName}");
            Console.ResetColor();
        }
    }

    private static void UpdateStatusLine()
    {
        lock (Console.Out)
        {
            Console.SetCursorPosition(0, 15);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"Aktiv: {_activeCount} | Gözləyən: {_waitingCount} | Tamamlanan: {_completedCount} | Xəta: 0    ");
            Console.ResetColor();
        }
    }
}
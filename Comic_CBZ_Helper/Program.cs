using System.Text.RegularExpressions;
using System.Text;
using System.Numerics;
using System.IO.Compression;
using System.Security;

namespace ConsoleApp1
{
    class Program
    {
        private static readonly object lockObject = new();
        static void Main(string[] args)
        {
            // 设置控制台的输出编码为UTF-8
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.OutputEncoding = Encoding.GetEncoding(936);

            // 确认运行模式
            Console.Write("请输入运行模式（0:append,1:init）：");
            string? runMode = Console.ReadLine();

            // 确保指定模式合法
            if (runMode != "0" && runMode != "1")
            {
                Console.WriteLine("运行模式不合法。");
                PauseBeforeExit(0);
                return;
            }

            string listFilePath = string.Empty;
            if (runMode == "1") // init模式
            {
                // 获取list.txt文件路径
                Console.Write("请输入list.txt文件的完整路径：");
                listFilePath = Console.ReadLine() ?? "";

                // 确保list.txt文件存在
                if (string.IsNullOrWhiteSpace(listFilePath) || !File.Exists(listFilePath))
                {
                    Console.WriteLine("list.txt 文件不存在。");
                    PauseBeforeExit(0);
                    return;
                }
            }

            // 获取上级文件夹路径
            Console.Write("请输入漫画所在的根路径：");
            string? parentFolder = Console.ReadLine();

            // 确保上级文件夹存在
            if (string.IsNullOrWhiteSpace(parentFolder) || !Directory.Exists(parentFolder))
            {
                Console.WriteLine("根路径不存在。");
                PauseBeforeExit(0);
                return;
            }

            // 获取保存 CBZ 的根文件夹路径
            Console.Write("请输入保存CBZ位置：");
            string? outputFolder = Console.ReadLine();

            // 确保 CBZ 文件夹存在
            if (string.IsNullOrWhiteSpace(outputFolder) || !Directory.Exists(outputFolder))
            {
                Console.WriteLine("CBZ 文件夹不存在。");
                PauseBeforeExit(0);
                return;
            }

            List<string> comicFolders;
            if (runMode == "1") // init模式
            {
                List<string> folderOrder = new(File.ReadAllLines(listFilePath));
                comicFolders = [.. Directory.GetDirectories(parentFolder).OrderBy(folder => folderOrder.IndexOf(Path.GetFileName(folder)))];
            }
            else // append模式或其他
            {
                // 获取所有漫画文件夹
                comicFolders = new List<string>(Directory.GetDirectories(parentFolder));
            }
            int totalFolders = comicFolders.Count;
            int processedFolders = 0;
            object consoleLock = new();

            // 设置初始日期
            DateTime currentDate = new(2020, 1, 1, 0, 0, 0);
            int count = 0;
            // 循环处理每个漫画文件夹
            Parallel.ForEach(comicFolders, new ParallelOptions { MaxDegreeOfParallelism = 4 }, comicFolder =>
            {
                lock (consoleLock)
                {
                    // 输出开始处理提示信息
                    Console.WriteLine($"开始处理漫画文件夹 {comicFolder}...");
                }

                // 确保漫画文件夹中包含图像文件
                string[] imageFiles = Directory.GetFiles(comicFolder, "*.*", SearchOption.AllDirectories)
                                             .Where(file => file.ToLower().EndsWith(".jpg") || file.ToLower().EndsWith(".png"))
                                             .ToArray();

                if (imageFiles.Length == 0)
                {
                    lock (consoleLock)
                    {
                        Console.WriteLine($"在漫画文件夹 {comicFolder} 中未找到任何图像文件 (.jpg or .png)。");
                    }
                    return; // 继续处理下一个漫画文件夹
                }

                // Sort the imageFiles using the NaturalSortComparer
                Array.Sort(imageFiles, new NaturalSortComparer());

                // 生成 CBZ 文件名为漫画文件夹的名称
                string cbzFileName = Path.Combine(outputFolder, $"{Path.GetFileName(comicFolder)}.cbz");

                // 检查 CBZ 文件是否已经存在，若存在则跳过当前漫画文件夹的处理
                if (File.Exists(cbzFileName))
                {
                    lock (consoleLock)
                    {
                        Console.WriteLine($"CBZ 文件 {cbzFileName} 已存在，跳过。");
                    }
                    return;
                }

                // 临时存储重命名后的文件路径
                string tempFolder = Path.Combine(comicFolder, "temp");
                // 检查 temp 文件夹是否存在，如果存在，则删除
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true); // true 表示递归删除文件夹中的所有内容
                }

                // 创建新的 temp 文件夹
                Directory.CreateDirectory(tempFolder);

                // 重新编号并复制图像文件到临时文件夹
                for (int i = 0; i < imageFiles.Length; i++)
                {
                    string newFileName = $"{i + 1:D4}{Path.GetExtension(imageFiles[i])}";
                    string newFilePath = Path.Combine(tempFolder, newFileName);
                    File.Copy(imageFiles[i], newFilePath);
                }

                // 获取重新编号后的图像文件
                string[] renamedImageFiles = Directory.GetFiles(tempFolder);

                // 创建 CBZ 文件
                using (FileStream zipToOpen = new(cbzFileName, FileMode.Create))
                {
                    using ZipArchive archive = new(zipToOpen, ZipArchiveMode.Create);
                    // 添加图像文件到 CBZ 文件
                    foreach (string imageFile in renamedImageFiles)
                    {
                        archive.CreateEntryFromFile(imageFile, Path.GetFileName(imageFile));
                    }
                    string comicInfoXmlContent = runMode == "1" ? GenerateComicInfoXml2(comicFolder, currentDate) : GenerateComicInfoXml(comicFolder);

                    ZipArchiveEntry comicInfoEntry = archive.CreateEntry("ComicInfo.xml");
                    using StreamWriter writer = new(comicInfoEntry.Open());
                    writer.Write(comicInfoXmlContent);
                }

                // 删除临时文件夹
                Directory.Delete(tempFolder, true);

                // 增加日期
                lock (lockObject)
                {
                    currentDate = currentDate.AddHours(7);
                    count++;
                }

                lock (consoleLock)
                {
                    Console.WriteLine($"漫画文件夹 {comicFolder} 转换完成。");
                }

                Interlocked.Increment(ref processedFolders);
                lock (consoleLock)
                {
                    ShowProgress(processedFolders, totalFolders);
                }
            });

            PauseBeforeExit(count);
        }
        private static void ShowProgress(int current, int total)
        {
            Console.CursorLeft = 0;
            int width = 50; // 进度条宽度
            int progress = (int)((current / (double)total) * width);
            Console.Write("[");
            Console.Write(new string('=', progress));
            Console.Write(new string(' ', width - progress));
            Console.Write($"] {current}/{total} 完成");
            if (current == total)
            {
                Console.WriteLine("\n处理完成！");
            }
        }
        // 生成 ComicInfo.xml 内容
        private static string GenerateComicInfoXml(string comicFolder)
        {
            string title = SecurityElement.Escape(Path.GetFileName(comicFolder));
            int year = DateTime.Now.Year;
            int month = DateTime.Now.Month;
            int day = DateTime.Now.Day;

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ComicInfo>
    <Title>{title}</Title>
    <Year>{year}</Year>
    <Month>{month}</Month>
    <Day>{day}</Day>
</ComicInfo>";
        }
        // 生成 ComicInfo.xml 内容
        private static string GenerateComicInfoXml2(string comicFolder, DateTime date)
        {
            string title = SecurityElement.Escape(Path.GetFileName(comicFolder));
            int year = date.Year;
            int month = date.Month;
            int day = date.Day;

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ComicInfo>
    <Title>{title}</Title>
    <Year>{year}</Year>
    <Month>{month}</Month>
    <Day>{day}</Day>
</ComicInfo>";
        }
        // 暂停程序，等待用户输入任意键后退出
        private static void PauseBeforeExit(int count)
        {
            Console.WriteLine("已处理" + count);
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }

        // Custom Natural Sort Comparer
        public class NaturalSortComparer : IComparer<string>
        {
            public int Compare(string? x, string? y)
            {
                if (x == null || y == null)
                {
                    return x == null ? (y == null ? 0 : -1) : 1;
                }
                // Get the file names from the full file paths
                string fileNameX = Path.GetFileName(x);
                string fileNameY = Path.GetFileName(y);

                // Define the regex pattern to match numbers in the file names
                string pattern = @"(\d+)";

                // Get all the matches of numbers in the file names
                MatchCollection matchesX = Regex.Matches(fileNameX, pattern);
                MatchCollection matchesY = Regex.Matches(fileNameY, pattern);

                // Compare the matches one by one
                int matchCount = Math.Min(matchesX.Count, matchesY.Count);
                for (int i = 0; i < matchCount; i++)
                {
                    BigInteger numX = BigInteger.Parse(matchesX[i].Value);
                    BigInteger numY = BigInteger.Parse(matchesY[i].Value);

                    int numComparison = numX.CompareTo(numY);
                    if (numComparison != 0)
                        return numComparison;

                    // Compare the non-numeric parts between the matched numbers
                    int nonNumericComparison = fileNameX.IndexOf(matchesX[i].Value) - fileNameY.IndexOf(matchesY[i].Value);
                    if (nonNumericComparison != 0)
                        return nonNumericComparison;
                }

                // If the numbers are the same up to this point, compare the remaining non-numeric parts
                return fileNameX.CompareTo(fileNameY);
            }
        }

    }
}


using System.Text.RegularExpressions;
using System.Text;
using System.Numerics;
using System.IO.Compression;
using System.Security;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            // 设置控制台的输出编码为UTF-8
            System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.OutputEncoding = Encoding.GetEncoding(936);

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
            // 读取txt文件中的文件夹顺序
            string listFilePath = @"G:\list.txt";
            if (!File.Exists(listFilePath))
            {
                Console.WriteLine("list.txt 文件不存在。");
                PauseBeforeExit(0);
                return;
            }
            List<string> folderOrder = [.. File.ReadAllLines(listFilePath)];

            // 获取所有漫画文件夹并按顺序排序
            List<string> comicFolders = [.. Directory.GetDirectories(parentFolder).OrderBy(folder => folderOrder.IndexOf(Path.GetFileName(folder)))];
            // 获取所有漫画文件夹
            //string[] comicFolders = Directory.GetDirectories(parentFolder);

            // 设置初始日期
            DateTime currentDate = new(2020, 1, 1, 0, 0, 0);
            int count = 0;
            // 循环处理每个漫画文件夹
            foreach (string comicFolder in comicFolders)
            {
                // 输出开始处理提示信息
                Console.WriteLine($"开始处理漫画文件夹 {comicFolder}...");

                // 确保漫画文件夹中包含图像文件
                string[] imageFiles = Directory.GetFiles(comicFolder, "*.*", SearchOption.AllDirectories)
                                             .Where(file => file.ToLower().EndsWith(".jpg") || file.ToLower().EndsWith(".png"))
                                             .ToArray();

                if (imageFiles.Length == 0)
                {
                    Console.WriteLine($"在漫画文件夹 {comicFolder} 中未找到任何图像文件 (.jpg or .png)。");
                    continue; // 继续处理下一个漫画文件夹
                }

                // Sort the imageFiles using the NaturalSortComparer
                Array.Sort(imageFiles, new NaturalSortComparer());

                // 生成 CBZ 文件名为漫画文件夹的名称
                string cbzFileName = Path.Combine(outputFolder, $"{Path.GetFileName(comicFolder)}.cbz");

                // 检查 CBZ 文件是否已经存在，若存在则跳过当前漫画文件夹的处理
                if (File.Exists(cbzFileName))
                {
                    Console.WriteLine($"CBZ 文件 {cbzFileName} 已存在，跳过。");
                    continue;
                }

                // 临时存储重命名后的文件路径
                string tempFolder = Path.Combine(comicFolder, "temp");
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

                    // 创建 ComicInfo.xml 并添加到 CBZ 文件
                    //string comicInfoXmlContent = GenerateComicInfoXml(comicFolder);
                    // init needed
                    string comicInfoXmlContent = GenerateComicInfoXml2(comicFolder, currentDate);
                    ZipArchiveEntry comicInfoEntry = archive.CreateEntry("ComicInfo.xml");
                    using StreamWriter writer = new(comicInfoEntry.Open());
                    writer.Write(comicInfoXmlContent);
                }

                // 删除临时文件夹
                Directory.Delete(tempFolder, true);

                // 增加日期
                currentDate = currentDate.AddHours(7);

                Console.WriteLine($"漫画文件夹 {comicFolder} 转换完成。");
                count++;
            }

            PauseBeforeExit(count);
        }

        // 生成 ComicInfo.xml 内容
        private static string GenerateComicInfoXml(string comicFolder)
        {
            string title = SecurityElement.Escape(comicFolder);
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
            string title = SecurityElement.Escape(comicFolder);
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


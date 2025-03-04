using System.Diagnostics;
using ConsoleApp2;
using System.Drawing;
using System.Text;
using System.Xml.Linq;
using Dithering;
using System.Security.AccessControl;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV;
using System.Collections.Concurrent;


// This code is based on the "CSharp-Dithering" project by mcraiha Kaarlo Räihä.
// The source code is available at: https://github.com/mcraiha/CSharp-Dithering
// License: Unlicense
//
// The rest of this project is licensed under: GNU General Public License v3.0

// Этот код основан на проекте "CSharp-Dithering" авторства mcraiha Kaarlo Räihä.
// Исходный код доступен по ссылке: https://github.com/mcraiha/CSharp-Dithering
// Лицензия: Unlicense
//
// Остальная часть данного проекта защищена лицензией: GNU General Public License v3.0



namespace ConsoleApp2
{
    internal class Program
    {
        private const uint DEFAULT_FRAMERATE = 30;
        private const uint DEFAULT_PHOTOCOUNT = 1;
        private static int semafors = 5;

        static async Task Main(string[] args)
        {
            string filePath, totalPath;
            uint photoCount, framerate;
            string Checker = null;
            Console.WriteLine("Введите путь к файлу (png, jpg, mp4):");
            filePath = DelTrashFromPath(Console.ReadLine());



            if (!ValidateFilePath(filePath))
            {
                return;
            }

            Console.WriteLine("Введите вероятное количество одновременно выполняемых задач:");
            Checker = Console.ReadLine();
            if (!IsNumeric(Checker))
            {
                Console.WriteLine("Количество задач не распознано");
                Checker = $"{semafors}";
            }
            semafors = Convert.ToInt32(Checker);

            Console.WriteLine("Введите путь к желаемому выходному файлу:");
            totalPath = DelTrashFromPath(Console.ReadLine());

            if (!ValidateDirectoryPath(totalPath))
            {
                return;
            }

            if (IsImageFile(filePath))
            {
                photoCount = await HandleImageProcessing(filePath, totalPath);
                PhotoProcessor processor = new PhotoProcessor(filePath, totalPath, photoCount, semafors);
                await processor.ProcessFramesAsync();
            }
            else if (IsVideoFile(filePath))
            {
                framerate = await HandleVideoProcessing();
                VideoProcessor processor = new VideoProcessor(filePath, totalPath, framerate, semafors);
                await processor.ProcessFramesAsync();
            }
            else
            {
                Console.WriteLine($"Файл {GetFileName(filePath)} не найден. Пожалуйста, проверьте путь и попробуйте снова.");
            }
        }
        private static bool IsNumeric(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return false; // Возвращаем false для пустой или null строки
            }

            foreach (char c in input)
            {
                if (!char.IsDigit(c))
                {
                    return false; // Если найден нецифровой символ, возвращаем false
                }
            }

            return true; // Если все символы цифры, возвращаем true
        }
        private static string DelTrashFromPath(string path)
        {
            string[] pathArray = path.Split((char)'.');
            if (path == null) { return ""; }
            pathArray = path.Split((char)'.');
            return string.Join(".", pathArray.Take(pathArray.Length)).Replace("\"", "");
        }
        private static bool ValidateFilePath(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"Файл {GetFileName(path)} не найден. Пожалуйста, проверьте путь и попробуйте снова.");
                return false;
            }
            return true;
        }

        private static bool ValidateDirectoryPath(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine("Выходная директория не найдена. Прекращаю выполнение.");
                return false;
            }
            return true;
        }

        private static bool IsImageFile(string path) =>
            path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase);

        private static bool IsVideoFile(string path) =>
            path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);

        private static string GetFileName(string path) =>
            path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries)[^1];

        private static async Task<uint> HandleImageProcessing(string filePath, string totalPath)
        {
            Console.WriteLine("Необходимо обработать одно фото в этой директории?\nY - одно фото. N - все фото, найденные в директории");
            string input = Console.ReadLine();

            if (string.IsNullOrEmpty(input) || !char.IsLetter(input[0]))
            {
                throw new InvalidOperationException("Ошибка, дан неверный ответ");
            }

            char choice = char.ToUpper(input[0]); // Приводим первый символ к верхнему регистру

            return choice switch
            {
                'Y' => 1,
                'N' => 2,
                _ => throw new InvalidOperationException("Ошибка, дан неверный ответ")
            };
        }

        private static async Task<uint> HandleVideoProcessing()
        {
            Console.WriteLine("Сколько fps в видео? _Можно тыкнуть энтер, тогда буду обрабатывать в 30fps_");
            string input = Console.ReadLine();
            return string.IsNullOrEmpty(input) ? DEFAULT_FRAMERATE : Convert.ToUInt32(input);
        }
    }
    public class UniqueIndexGenerator
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private int _currentIndex = 1;

        public async Task<int> GetNextIndexAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                return _currentIndex++;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
    public class VideoProcessor
    {
        private readonly string VideoPath;
        private readonly string TotalPath;
        private readonly uint FRAMERATE;
        private readonly int semafors;

        public VideoProcessor(string videoPath, string totalPath, uint frameRate = 30, int semafors = 1)
        {
            VideoPath = videoPath;
            TotalPath = totalPath;
            FRAMERATE = frameRate;
            this.semafors = semafors;
        }
        public async Task ProcessFramesAsync()
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(semafors); // Ограничиваем до semafors потоков
            List<Task> tasks = new List<Task>();
            var indexGenerator = new UniqueIndexGenerator(); // Создаем экземпляр генератора уникальных индексов
            ConcurrentDictionary<int, bool> inds = new ConcurrentDictionary<int, bool>(); // Используем ConcurrentDictionary для отслеживания индексов

            // Используем ConcurrentQueue для хранения кадров и их индексов
            ConcurrentQueue<(int index, Bitmap frame)> frameQueue = new ConcurrentQueue<(int, Bitmap)>();

            foreach (var frame in VideoFrameExtractor.ExtractFrames(VideoPath, FRAMERATE))
            {
                int index = await indexGenerator.GetNextIndexAsync(); // Получаем уникальный индекс
                frameQueue.Enqueue((index, frame)); // Добавляем в очередь
            }

            // Запускаем задачи
            while (frameQueue.TryDequeue(out var item))
            {
                await semaphore.WaitAsync(); // Ждем, пока не освободится место

                // Запускаем задачу
                tasks.Add(Task.Run(async () =>
                {

                    try
                    {
                        int index = item.index;
                        Bitmap frame = item.frame; // Извлекаем кадр из кортежа
                        string filePath = TotalPath + $"\\photo{index}.txt";

                        // Проверяем, существует ли файл или индекс уже добавлен
                        if (File.Exists(filePath) || inds.ContainsKey(index))
                        {
                            // Если файл существует или индекс уже добавлен, просто освобождаем кадр и семафор, и переходим к следующему
                            throw new Exception("File Exists"); // Переходим к следующему кадру
                        }

                        if (inds.TryAdd(index, true) == false)
                        {
                            throw new Exception("inds error");
                        }

                        PhotoTileConverter converter = new PhotoTileConverter(VideoPath, "", filePath);
                        // Добавляем текущий индекс
                        await converter.Convert(index, frame); // Выполняем конвертацию асинхронно
                    }
                    catch (Exception ex) // Обработка исключений
                    {
                        Console.WriteLine($"Произошла ошибка при конвертации: {ex.Message}");
                    }
                    finally
                    {
                        // Удаляем индекс из inds в любом случае
                        inds.TryRemove(item.index, out _); // Удаляем индекс
                        item.frame.Dispose(); // Освобождаем кадр
                        semaphore.Release(); // Освобождаем семафор
                    }
                }));
            }

            // Ожидаем завершения всех задач
            await Task.WhenAll(tasks);
        }

    }
    public class VideoFrameExtractor
    {
        public static IEnumerable<Bitmap> ExtractFrames(string videoPath, uint frameRate)
        {
            using (var capture = new VideoCapture(videoPath))
            {
                double fps = capture.Get(CapProp.Fps);
                int frameInterval = (int)(fps / frameRate);

                Mat frame = new Mat();
                int frameCount = 0;

                while (true)
                {
                    capture.Read(frame);
                    if (frame.IsEmpty)
                        break;

                    if (frameCount % frameInterval == 0)
                    {
                        Bitmap bitmap = frame.ToBitmap();
                        yield return bitmap; // Возвращаем кадр по одному
                    }

                    frameCount++;
                }
            }
        }
    }
    public class PhotoProcessor
    {
        private readonly string PhotoPath;
        private readonly string TotalPath;
        private readonly uint PhotoCount;
        private readonly int semafors;

        public PhotoProcessor(string videoPath, string totalPath, uint photoCount = 1, int semafors = 1)
        {
            PhotoPath = videoPath;
            TotalPath = totalPath;
            PhotoCount = photoCount;
            this.semafors = semafors > photoCount ? Convert.ToInt32(photoCount) : semafors;
        }
        public async Task ProcessFramesAsync()
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(semafors); // Ограничиваем до semafors потоков
            List<Task> tasks = new List<Task>();
            var indexGenerator = new UniqueIndexGenerator(); // Создаем экземпляр генератора уникальных индексов
            ConcurrentDictionary<int, bool> inds = new ConcurrentDictionary<int, bool>(); // Используем ConcurrentDictionary для отслеживания индексов
            IEnumerable<string> imageFiles;
            // Используем ConcurrentQueue для хранения кадров и их индексов
            ConcurrentQueue<(int index, Bitmap frame)> frameQueue = new ConcurrentQueue<(int, Bitmap)>();
            string filePath = PhotoPath;
            if (PhotoCount > 1)
            {
                imageFiles = Directory.GetFiles(Path.GetDirectoryName(PhotoPath), "*.*", SearchOption.TopDirectoryOnly)
                                      .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                  f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                imageFiles = [PhotoPath];
            }

            // Перебираем файлы и выводим их названия
            foreach (var frame in PhotoReturner.GetPhotos(imageFiles))
            {
                int index = await indexGenerator.GetNextIndexAsync(); // Получаем уникальный индекс
                frameQueue.Enqueue((index, frame)); // Добавляем в очередь
            }


            // Запускаем задачи
            while (frameQueue.TryDequeue(out var item))
            {
                await semaphore.WaitAsync(); // Ждем, пока не освободится место

                // Запускаем задачу
                tasks.Add(Task.Run(async () =>
                {

                    try
                    {
                        int index = item.index;
                        Bitmap frame = item.frame; // Извлекаем кадр из кортежа
                        string filePath = TotalPath + $"\\photo{index}.txt";

                        // Проверяем, существует ли файл или индекс уже добавлен
                        if (File.Exists(filePath) || inds.ContainsKey(index))
                        {
                            // Если файл существует или индекс уже добавлен, просто освобождаем кадр и семафор, и переходим к следующему
                            throw new Exception("File Exists"); // Переходим к следующему кадру
                        }

                        if (inds.TryAdd(index, true) == false)
                        {
                            throw new Exception("inds error");
                        }

                        PhotoTileConverter converter = new PhotoTileConverter(PhotoPath, "", filePath);
                        // Добавляем текущий индекс
                        await converter.Convert(index, frame); // Выполняем конвертацию асинхронно
                    }
                    catch (Exception ex) // Обработка исключений
                    {
                        Console.WriteLine($"Произошла ошибка при конвертации: {ex.Message}");
                    }
                    finally
                    {
                        // Удаляем индекс из inds в любом случае
                        inds.TryRemove(item.index, out _); // Удаляем индекс
                        item.frame.Dispose(); // Освобождаем кадр
                        semaphore.Release(); // Освобождаем семафор
                    }
                }));
            }

            // Ожидаем завершения всех задач
            await Task.WhenAll(tasks);
        }

    }
    public class PhotoReturner
    {
        public static IEnumerable<Bitmap> GetPhotos(IEnumerable<string> imageFiles)
        {
            // Получаем все файлы с расширениями .jpg и .png

            foreach (var file in imageFiles)
            {
                // Загружаем изображение и возвращаем его
                using (var bitmap = Bitmap.FromFile(file))
                {
                    yield return new Bitmap(bitmap); // Возвращаем копию изображения
                }
            }
        }
    }
    public class BinaryWorker
    {
        /// <summary>
        /// Путь к файлу результата
        /// </summary>
        private string _path = ""; // Приватное поле для хранения пути к файлу

        public string Path
        {
            get { return _path; } // Возвращает значение из приватного поля
            set { _path = value; } // Устанавливает значение в приватное поле
        }

        // Конструктор класса, принимает путь к файлу, по умолчанию установлен путь к TET.txt
        public BinaryWorker(string path)
        {
            Path = path; // Инициализация пути
        }

        // Список для хранения значений из файла
        public List<(bool, bool, ushort, byte)> FileValues = new List<(bool, bool, ushort, byte)>();

        /// <summary>
        /// Читает данные из файла и возвращает список кортежей, содержащих информацию о блоках.
        /// </summary>
        /// <returns>
        /// Список кортежей, где каждый кортеж содержит:
        ///   <list type="bullet">
        ///     <item><description>bool isWall - указывает, является ли блок стеной</description></item>
        ///     <item><description>bool isTorch - указывает, является ли блок факелом</description></item>
        ///     <item><description>ushort id - идентификатор блока</description></item>
        ///     <item><description>byte paintId - идентификатор краски блока</description></item>
        ///   </list>
        /// </returns>
        internal List<(bool isWall, bool isTorch, ushort id, byte paintId)> Read()
        {
            List<(bool, bool, ushort, byte)> Array = new List<(bool, bool, ushort, byte)>(); // Создаем новый список для хранения прочитанных данных
            byte[] bytes = File.ReadAllBytes(Path); // Читаем все байты из файла

            // Извлечение ширины и высоты из первых байтов
            ushort WidthStart = (ushort)((bytes[0] & 0xff) + ((bytes[1] & 0xff) << 8));
            ushort Width = (ushort)((bytes[2] & 0xff) + ((bytes[3] & 0xff) << 8));
            ushort Height = (ushort)((bytes[4] & 0xff) + ((bytes[5] & 0xff) << 8));

            // Чтение данных из файла и добавление их в список
            for (int i = 6; i < bytes.Length && i + 4 < bytes.Length; i += 5)
            {
                Array.Add((
                    Convert.ToBoolean(bytes[i]), // Признак стены
                    Convert.ToBoolean(bytes[i + 1]), // Признак факела
                    (ushort)((bytes[i + 2] & 0xff) + ((bytes[i + 3] & 0xff) << 8)), // ID
                    bytes[i + 4] // Цвет
                ));
            }
            return Array; // Возвращаем список прочитанных значений
        }

        /// <summary>
        /// Конвертирует число в формате ushort в строку заданной длины (до 8)
        /// </summary>
        /// <param name="value">Число для конвертации</param>
        /// <param name="length">Длина результирующей строки</param>
        /// <returns>Строка в двоичном формате</returns>
        private static string ConvertToBinary(ushort value, byte length)
        {
            string tmp = String.Empty; // Временная строка для хранения нулей
            string result = Convert.ToString(value, 2); // Конвертируем число в двоичную строку

            // Добавляем нули в начало строки до нужной длины
            for (int i = 0; i < (length - result.Length); i++)
            {
                tmp += "0"; // Добавление нуля
            }
            return tmp + result; // Возвращаем строку с нулями и результатом
        }

        // Метод для записи данных в файл
        internal void Write(ushort Width, ushort Height, ushort WidthStart = 0, List<(bool, bool, ushort, byte)> Array = null)
        {
            Array = Array ?? FileValues; // Если массив не передан, используем значения по умолчанию
            using (var stream = File.Open(Path, FileMode.Create)) // Открываем файл для записи
            {
                using (var binaryWriter = new BinaryWriter(stream, Encoding.UTF8, false)) // Создаем бинарный писатель
                {
                    // Записываем ширину, высоту и значения в файл
                    binaryWriter.Write(WidthStart); // Начальная ширина
                    binaryWriter.Write(Width); // Ширина
                    binaryWriter.Write(Height); // Высота
                    for (int index = 0; index < Array.Count; index++)
                    {
                        binaryWriter.Write(Array[index].Item1); // Стена?
                        binaryWriter.Write(Array[index].Item2); // Факел?
                        binaryWriter.Write(Array[index].Item3); // ID
                        binaryWriter.Write(Array[index].Item4); // Цвет
                    }
                }
            }
        }
    }

    public class Pixels
    {
        // Статический список для хранения объектов Pixel
        public List<Pixel> Objects = new List<Pixel>();

        // Конструктор класса, принимает массив строк, представляющих пути
        public Pixels(string[] Path, bool Mode = true)
        {
            // "1:4:29:000000:Block:Torches:ShadowPaint"
            if (Mode == true)
            {
                foreach (string tileLine in Path)
                {
                    // Разделяем строку на части, используя двоеточие (:) в качестве разделителя
                    string[] parts = tileLine.Split(':');
                    // Создаем объект Pixel и добавляем его в список
                    Add(new Pixel(parts, parts[0] == "0" ? true : false));
                }
                return;
            }
            if (Mode == false)
            {
                foreach (string tileLine in Path)
                {
                    // Разделяем строку на части, используя двоеточие (:) в качестве разделителя
                    string[] parts = tileLine.Split(':');
                    // Создаем объект Pixel и добавляем его в список
                    Add(new Pixel(parts, true));
                }
                return;
            }

            /*
            // Закомментированный код для загрузки данных из XML файла
            if (Path is string)
            {
                XElement file = XDocument.Load(Path.ToString()).Element("Settings");
                List<Pixel> pixels = new List<Pixel>();
                if (file.Element("Tiles") == null || file.Element("Walls") == null)
                {
                    throw new Exception("XML файл поврежден");
                }
                foreach (XElement item in file.Element("Tiles").Elements("Tile"))
                {
                    Add(new Pixel(item, false));
                }
                foreach (XElement item in file.Element("Walls").Elements("Wall"))
                {
                    Add(new Pixel(item, true));
                }
            }
            else if (Path is string[])
            {
                foreach (var tileLine in Path)
                {
                    // Разделяем строку на части, используя двоеточие (:) в качестве разделителя
                    string[] parts = tileLine.Split(':');
                    // Создаем объект Pixel и добавляем его в список
                    Add(new Pixel(parts, parts[0] == "0" ? true : false));
                }
            }
            */

            /*
            // Закомментированный код для задания пути к XML файлу
            string path = @"C:\Users\Сисьадмин\Documents\PixelArtCreatorByMixailka\settings.xml";
            */
        }

        // Метод для добавления объекта Pixel в список
        public void Add(Pixel pixel)
        {
            Objects.Add(pixel); // Добавляем пиксель в список
        }

        // Метод для удаления объекта Pixel из списка
        public void Del(Pixel pixel)
        {
            Objects.Remove(pixel); // Удаляем пиксель из списка
        }
        List<(bool, bool, ushort, byte)> _pixels;
        // Метод для получения списка пикселей в виде кортежей
        public List<(bool, bool, ushort, byte)> GetPixels()
        {
            _pixels ??= Objects.Select(x => (x.Wall, x.WallAtached, x.id, x.paint)).ToList();
            return _pixels;
        }
        List<(byte, byte, byte)> _colors;
        // Метод для получения списка цветов пикселей
        public List<(byte, byte, byte)> GetColors()
        {
            _colors ??= Objects.Select(x => x.color).ToList();
            return _colors;
        }
    }

    public partial class Pixel
    {
        public string Name; // Имя пикселя
        public bool Wall = false; // Признак стены
        public ushort id; // ID пикселя
        public byte paint; // ID краски
        public (byte, byte, byte) color; // Цвет пикселя
        public bool WallAtached = false; // Признак прикрепленного факела

        // Конструктор для создания пикселя из XML элемента
        public Pixel(XElement element, bool wall = false)
        {
            Name = element.Attribute("name").Value; // Получаем имя из атрибута
            Wall = wall; // Устанавливаем признак стены
            id = Convert.ToUInt16(element.Attribute("num").Value); // Получаем ID
            paint = Convert.ToByte(element.Attribute("paintID").Value); // Получаем ID краски
            color = ToBytes(element.Attribute("color").Value) ?? (0,0,0); // Получаем цвет
            WallAtached = element.Attribute("Torch").Value == "true" ? true : false; // Устанавливаем признак прикрепленного факела
        }

        // Конструктор для создания пикселя из массива строк
        public Pixel(string[] parts, bool wall = false)
        {
            Name = string.Concat(parts[5], " ", parts[6]); // Формируем имя
            Wall = wall; // Устанавливаем признак стены
            id = Convert.ToUInt16(parts[1]); // Получаем ID
            paint = Convert.ToByte(parts[2]); // Получаем ID краски
            color = ToBytes(parts[3]) ?? (0, 0, 0); // Получаем цвет
            WallAtached = DefTorchs.IndexOf(parts[1]) != -1 ? true : false; // Устанавливаем признак прикрепленного факела
        }

        // Метод для конвертации шестнадцатеричного значения в байты
        public static (byte, byte, byte)? ToBytes(string hexValue)
        {
            int hexColor = Convert.ToInt32(hexValue.Replace("#", ""), 16); // Преобразуем шестнадцатеричное значение в целое число
            return ((byte)((hexColor >> 16) & 0xff), // Извлекаем красный компонент
                    (byte)((hexColor >> 8) & 0xff),  // Извлекаем зеленый компонент
                    (byte)(hexColor & 0xff));        // Извлекаем синий компонент
        }

        // Статический список для хранения значений по умолчанию для факелов
        private static List<string> DefTorchs
        {
            get { return _defTorchs; }
            set { _defTorchs = value; }
        }
        // Статический список для хранения значений по умолчанию для исключений стен
        private static List<string> DefExceptionsWalls
        {
            get { return _defExceptionsWalls; }
            set { _defExceptionsWalls = value; }
        }
        private static List<string> DefExceptionsTiles
        {
            get { return _defExceptionsTiles; }
            set { _defExceptionsTiles = value; }
        }
    }

    internal class PhotoTileConverter
    {
        private string _path;
        private string _totalpath;
        private string _tilespath;

        public PhotoTileConverter(string PhotoPath, string TilesPath = "", string TotalPath = "")
        {
            _path = PhotoPath;
            _tilespath = TilesPath;
            _totalpath = TotalPath == "" ? System.Environment.GetEnvironmentVariable("USERPROFILE") + "\\Documents" + "\\PixelArtCreatorByMixailka\\photo.txt" : TotalPath;
        }

        private Color[]? _colors;
        public Color[] Colors
        {
            get { return _colors ?? []; }
            set { _colors = value; }
        }

        /// <summary>
        /// Читает цвета пикселей из заданного изображения (Bitmap) и возвращает массив, 
        /// содержащий цвета, сгруппированные в подмассивы по 100000 пикселей каждый.
        /// </summary>
        /// <param name="bitmap">Изображение, из которого будут извлечены цвета пикселей.</param>
        /// <param name="width">Ширина изображения (выходной параметр).</param>
        /// <param name="height">Высота изображения (выходной параметр).</param>
        /// <returns>Массив, где каждый элемент представляет собой последовательность цветов пикселей.</returns>
        /// <remarks>
        /// Функция проходит по каждому пикселю изображения, начиная с верхнего левого угла 
        /// и двигаясь вниз по столбцам, затем вправо к следующему столбцу. Каждый цвет 
        /// добавляется в список, который разбивается на подмассивы по 100000 элементов.
        /// </remarks>
        public IEnumerable<Color>[] ReadPhoto(Bitmap bitmap, out int width, out int height)
        {
            width = bitmap.Width;
            height = bitmap.Height;
            List<Color> pixelColors = new List<Color>(width * height);

            for (var i = 0; i < width; i++)
            {
                for (var j = 0; j < height; j++)
                {
                    Color pixelColor = bitmap.GetPixel(i, j);
                    pixelColors.Add(pixelColor);
                }
            }

            return Enumerable.Range(0, (int)Math.Ceiling((double)(width * height) / 100000.0))
                             .Select(i => pixelColors.Skip(i * 100000).Take(100000)).ToArray();
        }

        /// <summary>
        /// Конвертирует изображение в текстовый файл, представляющий его цвета.
        /// </summary>
        public async Task Convert(int i, Bitmap bitmap = null)
        {
            ColorApproximater approximater = new ColorApproximater(_tilespath);
            bitmap ??= (Bitmap)Image.FromFile(_path);
            Console.WriteLine($"{i}");
            Console.WriteLine($"Task {i} started on thread {Thread.CurrentThread.ManagedThreadId}");
            await Task.Run(() =>
            {
                try
                {
                    var Dither = new AtkinsonDithering();
                    Dither.Do((Bitmap)bitmap, approximater, new BinaryWorker(_totalpath));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n\nОШИБКА В КОНВЕРТАЦИИ!!! {ex}\n\n");
                }
            });
        }
    }
}
namespace Dithering
{

    #region Dithering Implementation (Optimized with Error Handling)

    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Runtime.InteropServices;

    public abstract class DitheringBase<T>
    {
        protected int width;
        protected int height;
        protected int channelsPerPixel;
        protected ColorFunction colorFunction;
        private IImageFormat<T> currentBitmap;

        public delegate void ColorFunction(in T[] inputColors, ref T[] outputColors, ColorApproximater colorApproximater);

        protected DitheringBase(ColorFunction colorfunc)
        {
            colorFunction = colorfunc ?? throw new ArgumentNullException(nameof(colorfunc));
        }

        public IImageFormat<T> DoDithering(IImageFormat<T> input, ColorApproximater approximater)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (approximater == null)
                throw new ArgumentNullException(nameof(approximater));

            try
            {
                width = input.GetWidth();
                height = input.GetHeight();
                channelsPerPixel = input.GetChannelsPerPixel();
                currentBitmap = input;

                T[] originalPixel = new T[channelsPerPixel];
                T[] newPixel = new T[channelsPerPixel];
                double[] quantError = new double[channelsPerPixel];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        input.GetPixelChannels(x, y, ref originalPixel);
                        colorFunction(in originalPixel, ref newPixel, approximater);

                        if (newPixel.Length != channelsPerPixel)
                            throw new InvalidOperationException("Color function returned incorrect pixel format.");

                        input.SetPixelChannels(x, y, newPixel);
                        input.GetQuantErrorsPerChannel(in originalPixel, in newPixel, ref quantError);
                        PushError(x, y, quantError);
                    }
                }
                return input;
            }
            catch (Exception ex)
            {
                throw new DitheringException("Dithering process failed.", ex);
            }
        }

        protected bool IsValidCoordinate(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }

        protected abstract void PushError(int x, int y, double[] quantError);

        protected void ModifyImageWithErrorAndMultiplier(int x, int y, double[] quantError, double multiplier)
        {
            T[] tempBuffer = new T[channelsPerPixel];
            currentBitmap.GetPixelChannels(x, y, ref tempBuffer);
            currentBitmap.ModifyPixelChannelsWithQuantError(ref tempBuffer, quantError, multiplier);
            currentBitmap.SetPixelChannels(x, y, tempBuffer);
        }
    }

    public sealed class AtkinsonDitheringRGBByte : DitheringBase<byte>
    {
        private static readonly int[] xOffsets = { 1, 2, -1, 0, 1, 0 };
        private static readonly int[] yOffsets = { 0, 0, 1, 1, 1, 2 };

        public AtkinsonDitheringRGBByte(ColorFunction colorfunc) : base(colorfunc) { }

        protected override void PushError(int x, int y, double[] quantError)
        {
            const double multiplier = 1.0 / 8.0;
            for (int i = 0; i < xOffsets.Length; i++)
            {
                int nx = x + xOffsets[i];
                int ny = y + yOffsets[i];
                if (IsValidCoordinate(nx, ny))
                    ModifyImageWithErrorAndMultiplier(nx, ny, quantError, multiplier);
            }
        }
    }

    public class DitheringException : Exception
    {
        public DitheringException(string message, Exception inner) : base(message, inner) { }
    }

    public interface IImageFormat<T>
    {
        int GetWidth();
        int GetHeight();
        int GetChannelsPerPixel();
        T[] GetRawContent();
        void SetPixelChannels(int x, int y, T[] newValues);
        T[] GetPixelChannels(int x, int y);
        void GetPixelChannels(int x, int y, ref T[] pixelStorage);
        double[] GetQuantErrorsPerChannel(T[] originalPixel, T[] newPixel);
        void GetQuantErrorsPerChannel(in T[] originalPixel, in T[] newPixel, ref double[] errorValues);
        void ModifyPixelChannelsWithQuantError(ref T[] modifyValues, double[] quantErrors, double multiplier);
    }

    public sealed class TempByteImageFormat : IImageFormat<byte>
    {
        private readonly byte[,,] content3d;
        private readonly byte[] content1d;
        public int Width { get; }
        public int Height { get; }
        public int ChannelsPerPixel { get; }

        public TempByteImageFormat(byte[,,] input, bool createCopy = false)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (createCopy)
            {
                content3d = (byte[,,])input.Clone();
            }
            else
            {
                content3d = input;
            }
            content1d = null;
            Width = input.GetLength(0);
            Height = input.GetLength(1);
            ChannelsPerPixel = input.GetLength(2);
        }

        public TempByteImageFormat(byte[] input, int width, int height, int channels, bool createCopy = false)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (width * height * channels != input.Length)
                throw new ArgumentException("Invalid dimensions.");

            content3d = null;
            if (createCopy)
            {
                content1d = new byte[input.Length];
                Buffer.BlockCopy(input, 0, content1d, 0, input.Length);
            }
            else
            {
                content1d = input;
            }
            Width = width;
            Height = height;
            ChannelsPerPixel = channels;
        }

        public int GetWidth() => Width;
        public int GetHeight() => Height;
        public int GetChannelsPerPixel() => ChannelsPerPixel;

        public byte[] GetRawContent()
        {
            if (content1d != null) return content1d;

            byte[] result = new byte[Width * Height * ChannelsPerPixel];
            int index = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    for (int c = 0; c < ChannelsPerPixel; c++)
                    {
                        result[index++] = content3d[x, y, c];
                    }
                }
            }
            return result;
        }

        public void SetPixelChannels(int x, int y, byte[] newValues)
        {
            ValidateCoordinates(x, y);
            if (newValues == null || newValues.Length != ChannelsPerPixel)
                throw new ArgumentException("Invalid pixel data.");

            if (content1d != null)
            {
                int index = (y * Width + x) * ChannelsPerPixel;
                Array.Copy(newValues, 0, content1d, index, ChannelsPerPixel);
            }
            else
            {
                for (int i = 0; i < ChannelsPerPixel; i++)
                {
                    content3d[x, y, i] = newValues[i];
                }
            }
        }

        public byte[] GetPixelChannels(int x, int y)
        {
            ValidateCoordinates(x, y);
            byte[] pixel = new byte[ChannelsPerPixel];
            GetPixelChannels(x, y, ref pixel);
            return pixel;
        }

        public void GetPixelChannels(int x, int y, ref byte[] pixelStorage)
        {
            ValidateCoordinates(x, y);
            if (pixelStorage == null || pixelStorage.Length < ChannelsPerPixel)
                throw new ArgumentException("Invalid pixel buffer.");

            if (content1d != null)
            {
                int index = (y * Width + x) * ChannelsPerPixel;
                Array.Copy(content1d, index, pixelStorage, 0, ChannelsPerPixel);
            }
            else
            {
                for (int i = 0; i < ChannelsPerPixel; i++)
                {
                    pixelStorage[i] = content3d[x, y, i];
                }
            }
        }

        public double[] GetQuantErrorsPerChannel(byte[] originalPixel, byte[] newPixel)
        {
            if (originalPixel == null || newPixel == null)
                throw new ArgumentNullException();
            if (originalPixel.Length != ChannelsPerPixel || newPixel.Length != ChannelsPerPixel)
                throw new ArgumentException("Invalid pixel data.");

            double[] errors = new double[ChannelsPerPixel];
            GetQuantErrorsPerChannel(originalPixel, newPixel, ref errors);
            return errors;
        }

        public void GetQuantErrorsPerChannel(in byte[] originalPixel, in byte[] newPixel, ref double[] errorValues)
        {
            if (originalPixel == null || newPixel == null || errorValues == null)
                throw new ArgumentNullException();
            if (originalPixel.Length != ChannelsPerPixel || newPixel.Length != ChannelsPerPixel || errorValues.Length != ChannelsPerPixel)
                throw new ArgumentException("Array length mismatch.");

            for (int i = 0; i < ChannelsPerPixel; i++)
            {
                errorValues[i] = originalPixel[i] - newPixel[i];
            }
        }

        public void ModifyPixelChannelsWithQuantError(ref byte[] modifyValues, double[] quantErrors, double multiplier)
        {
            if (modifyValues == null || quantErrors == null)
                throw new ArgumentNullException();
            if (modifyValues.Length != ChannelsPerPixel || quantErrors.Length != ChannelsPerPixel)
                throw new ArgumentException("Array length mismatch.");

            for (int i = 0; i < ChannelsPerPixel; i++)
            {
                double newValue = modifyValues[i] + quantErrors[i] * multiplier;
                modifyValues[i] = Clamp(newValue);
            }
        }

        private static byte Clamp(double value)
        {
            return (byte)Math.Clamp(value, 0, 255);
        }

        private void ValidateCoordinates(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException($"Coordinates ({x}, {y}) are out of bounds.");
        }
    }

    public class AtkinsonDithering
    {
        private static readonly int ColorFunctionMode = 1;

        public Bitmap Do(Bitmap image, ColorApproximater approximater, BinaryWorker worker)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));
            if (approximater == null)
                throw new ArgumentNullException(nameof(approximater));

            try
            {
                var atkinson = new AtkinsonDitheringRGBByte(ColorFunction);
                byte[,,] bytes = ReadBitmapToColorBytes(image);
                var tempImage = new TempByteImageFormat(bytes);
                atkinson.DoDithering(tempImage, approximater);
                WriteToBitmap(image, tempImage.GetPixelChannels,worker,approximater);
                return image;
            }
            catch (Exception ex)
            {
                throw new DitheringException("Dithering failed.", ex);
            }
        }

        private void ColorFunction(in byte[] input, ref byte[] output, ColorApproximater approximater)
        {
            if (ColorFunctionMode == 0)
                TrueColorBytesToWebSafeColorBytes(input, ref output);
            else
                TrueColorBytesToPalette(input, ref output, approximater);
        }

        private void TrueColorBytesToWebSafeColorBytes(in byte[] input, ref byte[] output)
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = (byte)(Math.Round(input[i] / 51.0) * 51);
            }
        }

        private static void TrueColorBytesToPalette(in byte[] input, ref byte[] output, ColorApproximater approximater)
        {
            output = approximater.Convert((input[0], input[1], input[2]));
        }

        private byte[,,] ReadBitmapToColorBytes(Bitmap bitmap)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            byte[,,] bytes = new byte[bitmap.Width, bitmap.Height, 3];

            try
            {
                byte[] buffer = new byte[data.Stride * data.Height];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                for (int y = 0; y < bitmap.Height; y++)
                {
                    int srcRow = y * data.Stride;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int srcIndex = srcRow + x * 3;
                        bytes[x, y, 0] = buffer[srcIndex + 2]; // R
                        bytes[x, y, 1] = buffer[srcIndex + 1]; // G
                        bytes[x, y, 2] = buffer[srcIndex];     // B
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bytes;
        }

        private void WriteToBitmap(Bitmap bitmap, Func<int, int, byte[]> reader, BinaryWorker worker, ColorApproximater approximater)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {

                    byte[] read = reader(x, y);
                    worker.FileValues.Add(approximater.GetColor((read[0], read[1], read[2])));
                }
            }
            worker.Write((ushort)bitmap.Width, (ushort)bitmap.Height);
        }
    }
    #endregion
    public static class FileDirectoryTools
    {
        public static bool IsFileAccessible(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false; // Путь не должен быть пустым
            }

            return File.Exists(filePath) &&
                   (new FileInfo(filePath).Attributes & FileAttributes.ReadOnly) == 0; // Проверка на доступность
        }
    }
    
    /// <summary>
    /// This code is the intellectual property of MKSO4KA (Mixailkin) and is protected by copyright law.
    /// Unauthorized copying, modification, or distribution of this code without explicit permission from the author is strictly prohibited.
    /// 
    /// License: GNU General Public License v3.0
    /// 
    /// For inquiries, please contact the author at: mk06ru@gmail.com
    /// </summary>
    public partial class ColorApproximater
    {
        public static string[] TilesDefault
        {
            get { return _tilesDefault; }
            set { _tilesDefault = value; }
        }
        private string _tilesPath = "";
        public string TilesPath
        {
            get { return _tilesPath; }
            set { _tilesPath = value; }
        }
        /// <summary>
        /// Call:
        /// <br></br>     Color color = Color.White;
        /// <br></br>     ColorApproximater Approximater = new ColorApproximater(list_colors);
        /// <br></br>     var cl = Approximater.Convert(color);
        /// </summary>
        public ColorApproximater(string path2tiles = "", int maxlenght = 1000)
        {
            TilesPath = path2tiles;
            _maxLenght = maxlenght;
            if (TilesPath == "")
            {
                _pixels = new Pixels(TilesDefault);
            }
            else
            {
                if (!FileDirectoryTools.IsFileAccessible(TilesPath))
                {
                    throw new FileNotFoundException("Файл не доступен или не существует: " + TilesPath);
                }
                _pixels = new Pixels(File.ReadAllLines(TilesPath));
            }
            
            _hueRgbRange = SetHueEqRgb();
            _findedColors = [];
            _convertedColors = [];
            _colors = [];
            _list_colors = _pixels.GetColors().ToArray();
            SetColors();
        }
        public ColorApproximater(Pixels colorslist, string Path = "", int maxlenght = 1000)
        {
            _pixels = colorslist;
            _maxLenght = maxlenght;
            _hueRgbRange = SetHueEqRgb();
            _findedColors = new List<(byte, byte, byte)>();
            _convertedColors = new List<(byte, byte, byte)>();
            _colors = new List<List<(byte, byte, byte)>>();
            _list_colors = colorslist.GetColors().ToArray();
            SetColors();
        }
        public (bool, bool, ushort, byte) GetColor((byte, byte, byte) a)
        {
            return _pixels.GetPixels()[_pixels.GetColors().IndexOf(a)];
        }

        /// <summary>
        /// The Convert method takes a Color object as an argument and returns a Color? object.
        /// <br></br>Inside the method, an empty Diffs list is created that will store the differences between the color of the color and each color in the array obtained using the GetColors method and the index obtained using the GetIndexOfColor method.
        /// <br></br> Next, a loop occurs in which for each color from the array the difference is calculated using the ColorDiff method and added to the Diffs list.
        /// <br></br> Finally, the method returns the color from the array that has the minimum color difference.
        /// <br></br>
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public byte[] Convert((byte, byte, byte) color)
        {
            //SetColors();
            int index;
            if ((index = _findedColors.IndexOf(color)) != -1)
            {
                return [_convertedColors[index].Item1, _convertedColors[index].Item2, _convertedColors[index].Item3];
            }
            List<double> Diffs = new List<double>();
            int indas = GetIndexOfColor(color);
            var Array = GetColors(indas);
            foreach (var item in Array)
            {
                Diffs.Add(ColorDiff(item, color));
            }

            _findedColors.Add(color);
            var color2 = Array[Diffs.IndexOf(Diffs.Min())];
            _convertedColors.Add(color2);
            if (_findedColors.Count == _maxLenght)
            {
                ResetAHalfOfConverted();
            }
            return [color2.Item1, color2.Item2, color2.Item3];
        }
        private static (byte, byte, byte)[] ColorsToBytes(Color[] colors)
        {
            (byte, byte, byte)[] result = new (byte, byte, byte)[colors.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (colors[i].R, colors[i].G, colors[i].B);
            }
            return result;
        }
        /// <summary>
        /// Private enumeration called _color, which represents different colors. 
        /// </summary>
        private enum _color
        {
            Red = 0,
            OrangeRed = 1,
            Orange = 2,
            OrangeYellow = 3,
            Yellow = 4,
            LemonYellow = 5,
            YellowGreen = 6,
            SapGreen = 7,
            Green = 8,
            BluishGreen = 9,
            Turquoise = 10,
            GreenishCyan = 11,
            CyanBlue = 12,
            BluishCyan = 13,
            Blue = 14,
            BlueViolet = 15,
            Violet = 16,
            PurpleViolet = 17,
            Purple = 18,
            PurpleMagenta = 19,
            Magenta = 20,
            Crimson = 21,
            Scarlet = 22,
            ScarletRed = 23
            // Here is the documentation for each color:
            // - Red: Represents the color red.Value: 0.
            // - OrangeRed: Represents the color orange-red.Value: 1.
            // - Orange: Represents the color orange. Value: 2.
            // - OrangeYellow: Represents the color orange-yellow.Value: 3.
            // - Yellow: Represents the color yellow. Value: 4.
            // - LemonYellow: Represents the color lemon-yellow.Value: 5.
            // - YellowGreen: Represents the color yellow-green.Value: 6.
            // - SapGreen: Represents the color sap green.Value: 7.
            // - Green: Represents the color green. Value: 8.
            // - BluishGreen: Represents the color bluish green.Value: 9.
            // - Turquoise: Represents the color turquoise. Value: 10.
            // - GreenishCyan: Represents the color greenish cyan.Value: 11.
            // - CyanBlue: Represents the color cyan-blue.Value: 12.
            // - BluishCyan: Represents the color bluish cyan.Value: 13.
            // - Blue: Represents the color blue. Value: 14.
            // - BlueViolet: Represents the color blue-violet.Value: 15.
            // - Violet: Represents the color violet. Value: 16.
            // - PurpleViolet: Represents the color purple-violet.Value: 17.
            // - Purple: Represents the color purple. Value: 18.
            // - PurpleMagenta: Represents the color purple-magenta.Value: 19.
            // - Magenta: Represents the color magenta. Value: 20.
            // - Crimson: Represents the color crimson. Value: 21.
            // - Scarlet: Represents the color scarlet. Value: 22.
            // - ScarletRed: Represents the color scarlet-red.Value: 23.
        }
        /// <summary>
        /// HueRange list that contains the hue degree ranges for each color in the previous code. Each element of the list is a tuple of two numbers representing the starting and ending degrees of hue for the corresponding color.
        /// For example, the first element of the list (352.5, 7.5) indicates that the color shade Red corresponds to the degree range from 352.5 to 7.5.
        /// This list is used to define the range of degrees for the hue of each color when performing color operations.
        /// </summary>
        private static readonly List<(float, float)> HueRange = new List<(float, float)>(24)
        {
            (352.5f, 7.5f),
            (7.5f, 22.5f),
            (22.5f, 37.5f),
            (37.5f, 52.5f),
            (52.5f, 67.5f),
            (67.5f, 82.5f),
            (82.5f, 97.5f),
            (97.5f, 112.5f),
            (112.5f, 127.5f),
            (127.5f, 142.5f),
            (142.5f, 157.5f),
            (157.5f, 172.5f),
            (172.5f, 187.5f),
            (187.5f, 202.5f),
            (202.5f, 217.5f),
            (217.5f, 232.5f),
            (232.5f, 247.5f),
            (247.5f, 262.5f),
            (262.5f, 277.5f),
            (277.5f, 292.5f),
            (292.5f, 307.5f),
            (307.5f, 322.5f),
            (322.5f, 337.5f),
            (337.5f, 352.5f)
        };
        private static int _maxLenght;
        public static int MaxLenght
        {
            get { return _maxLenght; }
        }
        private List<(byte, byte, byte)> _findedColors;
        private List<(byte, byte, byte)> _convertedColors;
        private List<List<(byte, byte, byte)>> _hueRgbRange;
        private List<List<(byte, byte, byte)>> _colors;
        private (byte, byte, byte)[] _list_colors;
        private static List<int> skip_colorslist = new List<int>();
        private Pixels _pixels;
        /// <summary>
        /// Парсит данные тайлов из файла tiles.txt и заполняет массивы цветов и тайлов.
        /// </summary>

        private void Reset()
        {
            _findedColors.Clear();
            _convertedColors.Clear();
        }


        /// <summary>
        ///The Colors class contains several static methods for working with colors.
        /// </summary>
        #region Colors
        /// <summary>
        ///The SetHueEqRgb method creates a new list of color lists, where each inner list contains colors corresponding to a specific degree range. This method uses the GetColorsFromHueRange method.
        /// </summary>
        /// <returns></returns>
        private static List<List<(byte, byte, byte)>> SetHueEqRgb()
        {
            (float, float) Hue;
            List<List<(byte, byte, byte)>> list = new List<List<(byte, byte, byte)>>(24);
            for (int i = 0; i < HueRange.Count; i += 1)
            {
                Hue = HueRange[i];
                list.Add(GetColorsFromHueRange(Hue));
            }
            list[HueRange.Count - 1].RemoveAt(list[HueRange.Count - 1].Count - 1);
            return list;
        }
        private float GetHue(byte r, byte g, byte b)
        {

            if (r == g && g == b)
                return 0f;

            MinMaxRgb(out int min, out int max, r, g, b);

            float delta = max - min;
            float hue;

            if (r == max)
                hue = (g - b) / delta;
            else if (g == max)
                hue = (b - r) / delta + 2f;
            else
                hue = (r - g) / delta + 4f;

            hue *= 60f;
            if (hue < 0f)
                hue += 360f;

            return hue;
        }
        private static void MinMaxRgb(out int min, out int max, byte r, byte g, byte b)
        {
            if (r > g)
            {
                max = r;
                min = g;
            }
            else
            {
                max = g;
                min = r;
            }
            if (b > max)
            {
                max = b;
            }
            else if (b < min)
            {
                min = b;
            }
        }
        /// <summary>
        ///The SetColors method initializes the _colors list and fills it with the colors from _list_colors. It then sorts each internal list by its color degree value
        /// </summary>
        private void SetColors()
        {
            for (int i = 0; i < 24; i += 1)
            {
                _colors.Add(new List<(byte, byte, byte)>());
            }
            foreach ((byte, byte, byte) color in _list_colors)
            {
                _colors[GetIndexOfColor(color)].Add((color.Item1, color.Item2, color.Item3));
            }
            for (int ind = 0; ind < _colors.Count; ind += 1)
            {
                _colors[ind] = _colors[ind].OrderBy(x => GetHue(x.Item1, x.Item2, x.Item3)).ToList();
            }
            for (int i = 0; i < GetColors().Count; i++)
            {
                if (GetColors(i).Count == 0)
                    skip_colorslist.Add(i);
            }
        }
        private List<List<(byte, byte, byte)>> GetColors()
        {
            return _colors;
        }
        private List<(byte, byte, byte)> GetColors(int id)
        {
            return _colors[id];
        }
        #endregion
        /// <summary>
        ///The Conversation class contains several static methods for converting colors from the hsl color model.
        /// </summary>
        private static class Conversation
        {
            public static Color ToRGB(double H, double S, double L)
            {
                // 
                byte r = 0;
                byte g = 0;
                byte b = 0;
                if (S == 0)
                {
                    r = g = b = (byte)(L * 255);
                }
                else
                {
                    double v1, v2;
                    double hue = (double)H / 360;

                    v2 = (L < 0.5) ? (L * (1 + S)) : ((L + S) - (L * S));
                    v1 = 2 * L - v2;

                    r = (byte)(255 * HueToRGB(v1, v2, hue + (1.0f / 3)));
                    g = (byte)(255 * HueToRGB(v1, v2, hue));
                    b = (byte)(255 * HueToRGB(v1, v2, hue - (1.0f / 3)));
                }

                return Color.FromArgb(r, g, b);
            }
            public static (byte, byte, byte) ToBytes(double H, double S, double L)
            {
                // 
                byte r = 0;
                byte g = 0;
                byte b = 0;
                if (S == 0)
                {
                    r = g = b = (byte)(L * 255);
                }
                else
                {
                    double v1, v2;
                    double hue = (double)H / 360;

                    v2 = (L < 0.5) ? (L * (1 + S)) : ((L + S) - (L * S));
                    v1 = 2 * L - v2;

                    r = (byte)(255 * HueToRGB(v1, v2, hue + (1.0f / 3)));
                    g = (byte)(255 * HueToRGB(v1, v2, hue));
                    b = (byte)(255 * HueToRGB(v1, v2, hue - (1.0f / 3)));
                }

                return (r, g, b);
            }
            private static double HueToRGB(double v1, double v2, double vH)
            {
                if (vH < 0)
                    vH += 1;

                if (vH > 1)
                    vH -= 1;

                if ((6 * vH) < 1)
                    return (v1 + (v2 - v1) * 6 * vH);

                if ((2 * vH) < 1)
                    return v2;

                if ((3 * vH) < 2)
                    return (v1 + (v2 - v1) * ((2.0f / 3) - vH) * 6);

                return v1;
            }

        }

        private static Color HSLToRGB(double H, double S = 1, double L = 0.5)
        {
            return Conversation.ToRGB(H, S, L);
        }
        private static (byte, byte, byte) HSLToBytes(double H, double S = 1, double L = 0.5)
        {
            return Conversation.ToBytes(H, S, L);
        }
        private void ResetAHalfOfConverted()
        {
            _findedColors = _findedColors.Skip(MaxLenght / 2).ToList();
            _convertedColors = _convertedColors.Skip(MaxLenght / 2).ToList();
        }

        private static float ColorDiff(Color c1, Color c2)
        {
            return (float)Math.Sqrt((c1.R - c2.R) * (c1.R - c2.R)
                                 + (c1.G - c2.G) * (c1.G - c2.G)
                                 + (c1.B - c2.B) * (c1.B - c2.B));
        }
        private static float ColorDiff((byte, byte, byte) c1, (byte, byte, byte) c2)
        {
            return (float)Math.Sqrt((c1.Item1 - c2.Item1) * (c1.Item1 - c2.Item1)
                                 + (c1.Item2 - c2.Item2) * (c1.Item2 - c2.Item2)
                                 + (c1.Item3 - c2.Item3) * (c1.Item3 - c2.Item3));
        }

        private int GetIndexOfColor(Color color)
        {
            List<float> diffs = new List<float>(HueRange.Count * 16);
            List<float> tmp = new List<float> {
                720, 720, 720, 720,
                720, 720, 720, 720,
                720, 720, 720, 720,
                720, 720, 720, 720};
            for (int rangeInd = 0; rangeInd < HueRange.Count; rangeInd++)
            {
                if (skip_colorslist.Contains(rangeInd))
                {
                    diffs.AddRange(tmp);
                    continue;
                }
                foreach (var item in _hueRgbRange[rangeInd])
                {
                    diffs.Add(ColorDiff(color, Color.FromArgb(item.Item1, item.Item2, item.Item3)));
                }
            }
            return (int)Math.Floor((double)diffs.IndexOf(diffs.Min()) / 16);
        }
        private int GetIndexOfColor((byte, byte, byte) color)
        {
            List<float> diffs = new List<float>(HueRange.Count * 16);
            List<float> tmp = new List<float> {
                720, 720, 720, 720,
                720, 720, 720, 720,
                720, 720, 720, 720,
                720, 720, 720, 720};
            for (int rangeInd = 0; rangeInd < HueRange.Count; rangeInd++)
            {
                if (skip_colorslist.Contains(rangeInd))
                {
                    diffs.AddRange(tmp);
                    continue;
                }
                foreach (var item in _hueRgbRange[rangeInd])
                {
                    diffs.Add(ColorDiff(color, item));
                }
            }
            return (int)Math.Floor((double)diffs.IndexOf(diffs.Min()) / 16);
        }
        private static List<(byte, byte, byte)> GetColorsFromHueRange((float, float) hue)
        {
            float min = hue.Item1, max = hue.Item2;
            List<(byte, byte, byte)> list = new List<(byte, byte, byte)>(16);
            if (min == 352.5)
            {
                for (float degree = min; degree < 360; degree += 1)
                {
                    list.Add(HSLToBytes(degree));
                }
                for (float degree = 0.5f; degree <= hue.Item2; degree += 1)
                {
                    list.Add(HSLToBytes(degree));
                }
            }
            else
            {
                for (float degree = hue.Item1; degree <= hue.Item2; degree += 1)
                {
                    list.Add(HSLToBytes(degree));
                }
            }
            return list;
        }



    }
}



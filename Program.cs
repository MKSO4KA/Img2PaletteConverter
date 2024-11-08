using System.Diagnostics;
using ConsoleApp2;
using System.Drawing;
using System.Text;
using System.Xml.Linq;
using Dithering;

namespace ConsoleApp2
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string PhotoPath, TilesPath, TotalPath;
            photopathNotFound:
            Console.WriteLine("Введите путь к фото");
            PhotoPath = Console.ReadLine();
            if (!File.Exists(PhotoPath))
            {
                Console.WriteLine("Файл не найден. Пожалуйста, проверьте путь и попробуйте снова.");
                goto photopathNotFound;
            }
            Console.WriteLine("Введите путь к тайлам");
            TilesPath = Console.ReadLine();
            if (!File.Exists(TilesPath))
            {
                Console.WriteLine("Файл не найден. Без пути к тайлам - использую дефолтные тайлы.");
                
            }
            Console.WriteLine("Введите путь к желаемому выходному файлу");
            TotalPath = Console.ReadLine();
            if (!File.Exists(TotalPath))
            {
                Console.WriteLine("Файл не найден. Без пути к желаемому выходному файлу сохраню в стандартй диектории(той, что создал плагин)");

            }
            PhotoTileConverter converter = new PhotoTileConverter(PhotoPath, TilesPath, TotalPath);
            
            converter.Convert();
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

        // Метод для чтения данных из файла
        internal List<(bool, bool, ushort, byte)> Read()
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
        public static List<Pixel> Objects = new List<Pixel>();

        // Конструктор класса, принимает массив строк, представляющих пути
        public Pixels(string[] Path)
        {
            foreach (string tileLine in Path)
            {
                // Разделяем строку на части, используя двоеточие (:) в качестве разделителя
                string[] parts = tileLine.Split(':');
                // Создаем объект Pixel и добавляем его в список
                Add(new Pixel(parts, parts[0] == "0" ? true : false));
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
            color = ToBytes(element.Attribute("color").Value); // Получаем цвет
            WallAtached = element.Attribute("Torch").Value == "true" ? true : false; // Устанавливаем признак прикрепленного факела
        }

        // Конструктор для создания пикселя из массива строк
        public Pixel(string[] parts, bool wall = false)
        {
            Name = string.Concat(parts[5], " ", parts[6]); // Формируем имя
            Wall = wall; // Устанавливаем признак стены
            id = Convert.ToUInt16(parts[1]); // Получаем ID
            paint = Convert.ToByte(parts[2]); // Получаем ID краски
            color = ToBytes(parts[3]); // Получаем цвет
            WallAtached = DefTorchs.IndexOf(parts[1]) != -1 ? true : false; // Устанавливаем признак прикрепленного факела
        }

        // Метод для конвертации шестнадцатеричного значения в байты
        private static (byte, byte, byte) ToBytes(string hexValue)
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

        private Color[] _colors;
        public Color[] Colors
        {
            get { return _colors ?? new Color[0]; }
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
        public void Convert()
        {
            ColorApproximater approximater = new ColorApproximater(_tilespath);
            Image bitmap = Image.FromFile(_path);

            AtkinsonDithering.Do((Bitmap)bitmap, approximater, new BinaryWorker(_totalpath));
        }
    }
}
namespace Dithering
{

    #region From Dithering Mischa

    public abstract class DitheringBase<T>
    {
        /// <summary>
        /// Width of bitmap
        /// </summary>
        protected int width;

        /// <summary>
        /// Height of bitmap
        /// </summary>
        protected int height;

        /// <summary>
        /// Color reduction function/method
        /// </summary>
        protected ColorFunction colorFunction;

        /// <summary>
        /// Current bitmap
        /// </summary>
        private IImageFormat<T> currentBitmap;

        /// <summary>
        /// Color function for color reduction
        /// </summary>
        /// <param name="inputColors">Input colors</param>
        /// <param name="outputColors">Output colors</param>
        public delegate void ColorFunction(in T[] inputColors, ref T[] outputColors, ColorApproximater colorApproximater = null);

        /// <summary>
        /// Base constructor
        /// </summary>
        /// <param name="colorfunc">Color reduction function/method</param>
        /// <param name="longName">Long name of dither method</param>
        /// <param name="fileNameAdd">Filename addition</param>
        protected DitheringBase(ColorFunction colorfunc, string longName, string fileNameAdd)
        {
            colorFunction = colorfunc;
            MethodLongName = longName;
            FileNameAddition = fileNameAdd;
        }

        /// <summary>
        /// Long name of the dither method
        /// </summary>
        public string MethodLongName { get; }

        /// <summary>
        /// Filename addition
        /// </summary>
        public string FileNameAddition { get; }

        /// <summary>
        /// Do dithering for chosen image with chosen color reduction method. Work horse, call this when you want to dither something
        /// </summary>
        /// <param name="input">Input image</param>
        /// <returns>Dithered image</returns>
        public IImageFormat<T> DoDithering(IImageFormat<T> input)
        {
            width = input.GetWidth();
            height = input.GetHeight();
            int channelsPerPixel = input.GetChannelsPerPixel();
            currentBitmap = input;

            T[] originalPixel = new T[channelsPerPixel];
            T[] newPixel = new T[channelsPerPixel];
            T[] tempBuffer = new T[channelsPerPixel]; // Инициализация здесь
            double[] quantError = new double[channelsPerPixel];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    input.GetPixelChannels(x, y, ref originalPixel);
                    colorFunction(in originalPixel, ref newPixel);

                    input.SetPixelChannels(x, y, newPixel);
                    input.GetQuantErrorsPerChannel(in originalPixel, in newPixel, ref quantError);
                    PushError(x, y, quantError);
                }
#if DEBUG
                // Вычисляем процент выполнения только по y
                int totalPixels = height; // Общее количество строк
                int processedRows = y + 1; // +1, чтобы учесть текущую строку
                double percentage = (double)processedRows / totalPixels * 100;

                // Выводим процент выполнения в отладочный вывод
                Debug.WriteLine($"Обработано: {percentage:F2}%");
#endif
            }

            return input;
        }

        /// <summary>
        /// Check if image coordinate is valid
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns>True if valid; False otherwise</returns>
        protected bool IsValidCoordinate(int x, int y)
        {
            return (0 <= x && x < width && 0 <= y && y < height);
        }

        /// <summary>
        /// How error cumulation should be handled. Implement this for every dithering method
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="quantError">Quantization error</param>
        protected abstract void PushError(int x, int y, double[] quantError);

        /// <summary>
        /// Modify image with error and multiplier
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="quantError">Quantization error</param>
        /// <param name="multiplier">Multiplier</param>
        public void ModifyImageWithErrorAndMultiplier(int x, int y, double[] quantError, double multiplier)
        {
            T[] tempBuffer = new T[width * height]; // Создаем временный буфер
            currentBitmap.GetPixelChannels(x, y, ref tempBuffer);
            currentBitmap.ModifyPixelChannelsWithQuantError(ref tempBuffer, quantError, multiplier);
            currentBitmap.SetPixelChannels(x, y, tempBuffer);
        }
    }

    public sealed class AtkinsonDitheringRGBByte : DitheringBase<byte>
    {
        /// <summary>
        /// Constructor for Atkinson dithering
        /// </summary>
        /// <param name="colorfunc">Color function</param>
        public AtkinsonDitheringRGBByte(ColorFunction colorfunc) : base(colorfunc, "Atkinson", "_ATK") { }

        /// <summary>
        /// Push error method for Atkinson dithering
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="quantError">Quantization error</param>
        override protected void PushError(int x, int y, double[] quantError)
        {
            int xMinusOne = x - 1;
            int xPlusOne = x + 1;
            int xPlusTwo = x + 2;
            int yPlusOne = y + 1;
            int yPlusTwo = y + 2;

            double multiplier = 1.0 / 8.0; // Atkinson Dithering has same multiplier for every item

            // Current row
            if (IsValidCoordinate(xPlusOne, y)) ModifyImageWithErrorAndMultiplier(xPlusOne, y, quantError, multiplier);
            if (IsValidCoordinate(xPlusTwo, y)) ModifyImageWithErrorAndMultiplier(xPlusTwo, y, quantError, multiplier);

            // Next row
            if (IsValidCoordinate(xMinusOne, yPlusOne)) ModifyImageWithErrorAndMultiplier(xMinusOne, yPlusOne, quantError, multiplier);
            if (IsValidCoordinate(x, yPlusOne)) ModifyImageWithErrorAndMultiplier(x, yPlusOne, quantError, multiplier);
            if (IsValidCoordinate(xPlusOne, yPlusOne)) ModifyImageWithErrorAndMultiplier(xPlusOne, yPlusOne, quantError, multiplier);

            // Next row
            if (IsValidCoordinate(x, yPlusTwo)) ModifyImageWithErrorAndMultiplier(x, yPlusTwo, quantError, multiplier);
        }
    }

    public interface IImageFormat<T>
    {
        /// <summary>
        /// Get width
        /// </summary>
        /// <returns>Width of image</returns>
        int GetWidth();

        /// <summary>
        /// Get height
        /// </summary>
        /// <returns>Height of image</returns>
        int GetHeight();

        /// <summary>
        /// Get channels per pixel
        /// </summary>
        /// <returns>Channels per pixel</returns>
        int GetChannelsPerPixel();

        /// <summary>
        /// Get raw content as array
        /// </summary>
        /// <returns>Array</returns>
        T[] GetRawContent();

        /// <summary>
        /// Set pixel channels of certain coordinate
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="newValues">New values</param>
        void SetPixelChannels(int x, int y, T[] newValues);

        /// <summary>
        /// Get pixel channels of certain coordinate
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns>Values as array</returns>
        T[] GetPixelChannels(int x, int y);

        /// <summary>
        /// Get pixel channels of certain coordinate
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="pixelStorage">Array where pixel channels values will be written</param>
        void GetPixelChannels(int x, int y, ref T[] pixelStorage);

        /// <summary>
        /// Get quantization errors per channel
        /// </summary>
        /// <param name="originalPixel">Original pixels</param>
        /// <param name="newPixel">New pixels</param>
        /// <returns>Error values as double array</returns>
        double[] GetQuantErrorsPerChannel(T[] originalPixel, T[] newPixel);

        /// <summary>
        /// Get quantization errors per channel
        /// </summary>
        /// <param name="originalPixel">Original pixels</param>
        /// <param name="newPixel">New pixels</param>
        /// <param name="errorValues">Error values as double array</param>
        void GetQuantErrorsPerChannel(in T[] originalPixel, in T[] newPixel, ref double[] errorValues);

    /// <summary>
    /// Modify existing values with quantization errors
    /// </summary>
    /// <param name="modifyValues">Values to modify</param>
    /// <param name="quantErrors">Quantization errors</param>
    /// <param name="multiplier">Multiplier</param>
        void ModifyPixelChannelsWithQuantError(ref T[] modifyValues, double[] quantErrors, double multiplier);
    }

    public sealed class TempByteImageFormat : IImageFormat<byte>
    {
        /// <summary>
        /// Width of bitmap
        /// </summary>
        public readonly int width;

        /// <summary>
        /// Height of bitmap
        /// </summary>
        public readonly int height;

        private readonly byte[,,] content3d;

        private readonly byte[] content1d;

        /// <summary>
        /// How many color channels per pixel
        /// </summary>
        public readonly int channelsPerPixel;

        /// <summary>
        /// Constructor for temp byte image format
        /// </summary>
        /// <param name="input">Input bitmap as three dimensional (width, height, channels per pixel) byte array</param>
        /// <param name="createCopy">True if you want to create copy of data</param>
        public TempByteImageFormat(byte[,,] input, bool createCopy = false)
        {
            content3d = createCopy ? (byte[,,])input.Clone() : input;
            content1d = null;
            width = input.GetLength(0);
            height = input.GetLength(1);
            channelsPerPixel = input.GetLength(2);
        }

        /// <summary>
        /// Constructor for temp byte image format
        /// </summary>
        /// <param name="input">Input byte array</param>
        /// <param name="imageWidth">Width</param>
        /// <param name="imageHeight">Height</param>
        /// <param name="imageChannelsPerPixel">Image channels per pixel</param>
        /// <param name="createCopy">True if you want to create copy of data</param>
        public TempByteImageFormat(byte[] input, int imageWidth, int imageHeight, int imageChannelsPerPixel, bool createCopy = false)
        {
            content3d = null;
            content1d = createCopy ? new byte[input.Length] : input;
            if (createCopy) Buffer.BlockCopy(input, 0, content1d, 0, input.Length);
            width = imageWidth;
            height = imageHeight;
            channelsPerPixel = imageChannelsPerPixel;
        }

        /// <summary>
        /// Get width of bitmap
        /// </summary>
        /// <returns>Width in pixels</returns>
        public int GetWidth() => width;

        /// <summary>
        /// Get height of bitmap
        /// </summary>
        /// <returns>Height in pixels</returns>
        public int GetHeight() => height;

        /// <summary>
        /// Get channels per pixel
        /// </summary>
        /// <returns>Channels per pixel</returns>
        public int GetChannelsPerPixel() => channelsPerPixel;

        /// <summary>
        /// Get raw content as byte array
        /// </summary>
        /// <returns>Byte array</returns>
        public byte[] GetRawContent()
        {
            if (content1d != null) return content1d;

            byte[] returnArray = new byte[width * height * channelsPerPixel];
            int currentIndex = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int i = 0; i < channelsPerPixel; i++)
                    {
                        returnArray[currentIndex++] = content3d[x, y, i];
                    }
                }
            }
            return returnArray;
        }

        /// <summary>
        /// Set pixel channels of certain coordinate
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="newValues">New values as object array</param>
        public void SetPixelChannels(int x, int y, byte[] newValues)
        {
            if (content1d != null)
            {
                int indexBase = y * width * channelsPerPixel + x * channelsPerPixel;
                for (int i = 0; i < channelsPerPixel; i++)
                {
                    content1d[indexBase + i] = newValues[i];
                }
            }
            else
            {
                for (int i = 0; i < channelsPerPixel; i++)
                {
                    content3d[x, y, i] = newValues[i];
                }
            }
        }

        /// <summary>
        /// Get pixel channels of certain coordinate
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate ```csharp
        /// <returns>Values as byte array</returns>
        public byte[] GetPixelChannels(int x, int y)
        {
            byte[] returnArray = new byte[channelsPerPixel];

            if (content1d != null)
            {
                int indexBase = y * width * channelsPerPixel + x * channelsPerPixel;
                for (int i = 0; i < channelsPerPixel; i++)
                {
                    returnArray[i] = content1d[indexBase + i];
                }
            }
            else
            {
                for (int i = 0; i < channelsPerPixel; i++)
                {
                    returnArray[i] = content3d[x, y, i];
                }
            }

            return returnArray;
        }

        /// <summary>
        /// Get pixel channels of certain coordinate
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="pixelStorage">Array where pixel channels values will be written</param>
        public void GetPixelChannels(int x, int y, ref byte[] pixelStorage)
        {
            if (content1d != null)
            {
                int indexBase = y * width * channelsPerPixel + x * channelsPerPixel;
                for (int i = 0; i < channelsPerPixel; i++)
                {
                    pixelStorage[i] = content1d[indexBase + i];
                }
            }
            else
            {
                for (int i = 0; i < channelsPerPixel; i++)
                {
                    pixelStorage[i] = content3d[x, y, i];
                }
            }
        }

        /// <summary>
        /// Get quantization errors per channel
        /// </summary>
        /// <param name="originalPixel">Original pixels</param>
        /// <param name="newPixel">New pixels</param>
        /// <returns>Error values as double array</returns>
        public double[] GetQuantErrorsPerChannel(byte[] originalPixel, byte[] newPixel)
        {
            double[] returnValue = new double[channelsPerPixel];

            for (int i = 0; i < channelsPerPixel; i++)
            {
                returnValue[i] = originalPixel[i] - newPixel[i];
            }

            return returnValue;
        }

        /// <summary>
        /// Get quantization errors per channel
        /// </summary>
        /// <param name="originalPixel">Original pixels</param>
        /// <param name="newPixel">New pixels</param>
        /// <param name="errorValues">Error values as double array</param>
        public void GetQuantErrorsPerChannel(in byte[] originalPixel, in byte[] newPixel, ref double[] errorValues)
        {
            for (int i = 0; i < channelsPerPixel; i++)
            {
                errorValues[i] = originalPixel[i] - newPixel[i];
            }
        }

        /// <summary>
        /// Modify existing values with quantization errors
        /// </summary>
        /// <param name="modifyValues">Values to modify</param>
        /// <param name="quantErrors">Quantization errors</param>
        /// <param name="multiplier">Multiplier</param>
        public void ModifyPixelChannelsWithQuantError(ref byte[] modifyValues, double[] quantErrors, double multiplier)
        {
            for (int i = 0; i < channelsPerPixel; i++)
            {
                modifyValues[i] = GetLimitedValue((byte)modifyValues[i], quantErrors[i] * multiplier);
            }
        }

        private static byte GetLimitedValue(byte original, double error)
        {
            double newValue = original + error;
            return Clamp(newValue, byte.MinValue, byte.MaxValue);
        }

        // C# doesn't have a Clamp method so we need this
        private static byte Clamp(double value, double min, double max)
        {
            return (value < min) ? (byte)min : (value > max) ? (byte)max : (byte)value;
        }
    }
    #endregion

    public class AtkinsonDithering
    {
        private static ColorApproximater _approximater;
        private static BinaryWorker _worker;
        private static readonly int ColorFunctionMode = 1;

        public static Bitmap Do(Bitmap image, ColorApproximater approximater, BinaryWorker worker)
        {
            _approximater = approximater;
            _worker = worker;
            //approximater = new ColorApproximater(new Color[] { Color.White, Color.Black, Color.AliceBlue });
            AtkinsonDitheringRGBByte atkinson = new AtkinsonDitheringRGBByte(ColorFunction);
            byte[,,] bytes = ReadBitmapToColorBytes(image);

            TempByteImageFormat temp = new TempByteImageFormat(bytes);
            Console.WriteLine("\nВсё вроде работает нормально, начинаю конвертацию!\n\n");
            atkinson.DoDithering(temp);

            WriteToBitmap(image, temp.GetPixelChannels);
            Console.WriteLine($"Конвертация завершена. Файл находится по пути {worker.Path}");
            return image;
        }
        private static void ColorFunction(in byte[] input, ref byte[] output, ColorApproximater approximater)
        {

            switch (ColorFunctionMode)
            {
                case 0:
                    TrueColorBytesToWebSafeColorBytes(input, ref output);
                    break;
                default:
                    TrueColorBytesToPalette(input, ref output);
                    break;
            }
        }
        private static void TrueColorBytesToWebSafeColorBytes(in byte[] input, ref byte[] output)
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = (byte)(Math.Round(input[i] / 51.0) * 51);
            }
        }
        private static void TrueColorBytesToPalette(in byte[] input, ref byte[] output)
        {
            output = _approximater.Convert((input[0], input[1], input[2]));
            //output = new byte[] { i.R, i.G, i.B};
        }

        private static byte[,,] ReadBitmapToColorBytes(Bitmap bitmap)
        {
            byte[,,] returnValue = new byte[bitmap.Width, bitmap.Height, 3];
            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Color color = bitmap.GetPixel(x, y);
                    returnValue[x, y, 0] = color.R;
                    returnValue[x, y, 1] = color.G;
                    returnValue[x, y, 2] = color.B;
                }
            }
            return returnValue;
        }

        private static void WriteToBitmap(Bitmap bitmap, Func<int, int, byte[]> reader)
        {
            BinaryWorker worker = _worker;
            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {

                    byte[] read = reader(x, y);
                    worker.FileValues.Add(_approximater.GetColor((read[0], read[1], read[2])));
                    Color color = Color.FromArgb(read[0], read[1], read[2]);
                    //bitmap.SetPixel(x, y, color);
                }
            }
            worker.Write((ushort)bitmap.Width, (ushort)bitmap.Height);
        }
    }

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
        public ColorApproximater(string path2tiles="", int maxlenght = 1000)
        {
            TilesPath = path2tiles;
            _maxLenght = maxlenght;
            _pixels = new Pixels(TilesPath == "" ? TilesDefault : File.ReadAllLines(TilesPath));
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
        private static List<(byte, byte, byte)> _findedColors;
        private static List<(byte, byte, byte)> _convertedColors;
        public List<List<(byte, byte, byte)>> _hueRgbRange;
        public List<List<(byte, byte, byte)>> _colors;
        private (byte, byte, byte)[] _list_colors;
        private static List<int> skip_colorslist = new List<int>();
        private Pixels _pixels;
        /// <summary>
        /// Парсит данные тайлов из файла tiles.txt и заполняет массивы цветов и тайлов.
        /// </summary>
        
        public void Reset()
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
        public static List<List<(byte, byte, byte)>> SetHueEqRgb()
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
        public void SetColors()
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
        public List<List<(byte, byte, byte)>> GetColors()
        {
            return _colors;
        }
        public List<(byte, byte, byte)> GetColors(int id)
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
        private static void ResetAHalfOfConverted()
        {
            _findedColors = _findedColors.Skip(MaxLenght / 2).ToList();
            _convertedColors = _convertedColors.Skip(MaxLenght / 2).ToList();
        }

        public static float ColorDiff(Color c1, Color c2)
        {
            return (float)Math.Sqrt((c1.R - c2.R) * (c1.R - c2.R)
                                 + (c1.G - c2.G) * (c1.G - c2.G)
                                 + (c1.B - c2.B) * (c1.B - c2.B));
        }
        public static float ColorDiff((byte, byte, byte) c1, (byte, byte, byte) c2)
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



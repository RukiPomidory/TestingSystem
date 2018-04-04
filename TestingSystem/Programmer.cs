using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestingSystem
{
    /// <summary>
    /// Инкапсулирует информацию о программисте
    /// </summary>
    public class Programmer
    {
        public string Name { get; set; }
        
        /// <summary>
        /// Текст, отображаемый как результат
        /// </summary>
        public string ResultText { get; set; }

        /// <summary>
        /// Процент выполненных тестов
        /// </summary>
        int result = 0;
        public int Result
        {
            get
            {
                return result;
            }
            set
            {
                result = value;
                ResultValueChanged();
            }
        }

        /// <summary>
        /// Является ли файл программы исполняемым
        /// </summary>
        public bool IsExe { get; }

        /// <summary>
        /// Язык программирования программиста
        /// </summary>
        public CodeLanguage language;

        /// <summary>
        /// Оригинальный путь программы
        /// </summary>
        public readonly string original;
        public string ID { get; }

        /// <summary>
        /// Путь файла с кодом программы
        /// </summary>
        public string programPath;

        /// <summary>
        /// Список, содержащий элементы для вывода в лог
        /// </summary>
        public List<string> Log { get; set; }

        /// <summary>
        /// омер первого проваленного теста
        /// </summary>
        public int firstFail;

        /// <summary>
        /// Список полученных ответов
        /// </summary>
        public List<string> UserAnswers { get; set; }

        /// <summary>
        /// Список правильных ответов
        /// </summary>
        public List<string> CorrectAnswers { get; set; }

        /// <summary>
        /// Общее время, затраченное на тестирование
        /// </summary>
        public long time = 0;

        /// <summary>
        /// Список уже использованных имен
        /// </summary>
        private static List<string> names;

        /// <summary>
        /// Количество созданных экземпляров 
        /// </summary>
        private static int count;
        
        static Programmer()
        {
            count = 1;
            names = new List<string>();
        }

        /// <summary>
        /// Вызывается при изменении результата
        /// </summary>
        public event Action ResultValueChanged;

        public Programmer()
        {
            ID = count++.ToString();
        }

        public Programmer(string path, string name = "programmer") : this()
        {
            original = path;
            programPath = $"{Directory.GetCurrentDirectory()}\\" +
                $"{Path.GetFileNameWithoutExtension(path)}{ID}\\{Path.GetFileName(path)}";
            Directory.CreateDirectory(Path.GetDirectoryName(programPath));
            File.Copy(path, programPath, true);
            if (names.Contains(name))
                name = name + ID;
            Name = name;
            Log = new List<string>();
            IsExe = Path.GetExtension(path) == ".exe";
            UserAnswers = new List<string>();
            CorrectAnswers = new List<string>();
        }

        public Programmer(CodeLanguage language, string path, string name = "programmer") : this(path, name)
        {
            this.language = language;
        }
    }
}

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
        public string ResultText { get; set; }
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
        public bool IsExe { get; }
        public CodeLanguage language;
        public readonly string original;
        public string ID { get; }
        public string programPath;
        public List<string> Log { get; set; }
        public int firstFail;
        public List<string> UserAnswers { get; set; }
        public List<string> CorrectAnswers { get; set; }
        public long time = 0;

        private static List<string> names;
        private static int count;
        
        static Programmer()
        {
            count = 1;
            names = new List<string>();
        }

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

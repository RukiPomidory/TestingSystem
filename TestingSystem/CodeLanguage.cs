using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TestingSystem
{
    /// <summary>
    /// Содержит информацию о языке программирования
    /// </summary>
    public class CodeLanguage
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string extension;
        public Func <Programmer, Process> CreatingMethod { get; set; }
        public Action<Process, Programmer> Preparation { get; set; }

        public CodeLanguage(string id, string name,
            Func<Programmer, Process> creatingMethod,
            Action<Process, Programmer> preparation = null)
        {
            ID = id;
            Name = name;
            CreatingMethod = creatingMethod ?? throw new ArgumentNullException();
            Preparation = preparation ?? ((proc, prog) => { });
        }

        public CodeLanguage(string id, string name,
            Func<Programmer, Process> creatingMethod,
            Action<Process, Programmer> preparation,
            string extension) : this(id, name, creatingMethod, preparation)
        {
            this.extension = extension;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}

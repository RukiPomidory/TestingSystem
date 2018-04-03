using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestingSystem
{
    /// <summary>
    /// Перечислимое считывание строчек файла
    /// </summary>
    public class FileData : IEnumerable<string>
    {
        StreamReader stream;

        public FileData(string path)
        {
            stream = new StreamReader(path);
        }

        ~FileData()
        {
            Close();
        }

        public void Close()
        {
            stream.Close();
        }

        public IEnumerator<string> GetEnumerator()
        {
            string result = stream.ReadLine();
            while (result != null)
            {
                yield return result;
                result = stream.ReadLine();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

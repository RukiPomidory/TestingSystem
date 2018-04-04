using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace TestingSystem
{
    /// <summary>
    /// Управление кнопками
    /// </summary>
    public class ButtonControl
    {
        public List<Func<bool>> Checkers { get; set; }
        public List<Button> buttons;

        /// <summary>
        /// Проверяет, имеет ли экземпляр делегаты, возвращающие true
        /// </summary>
        /// <returns>true, если есть хотя бы один метод, возвращающий true, иначе - false</returns>
        public bool IsEnabled
        {
            get
            {
                foreach (var c in Checkers)
                    if (c()) return true;
                return false;
            }
        }
        
        /// <summary>
        /// Создает экземпляр класса ButtonControl с пустым списком делегатов проверки
        /// </summary>
        public ButtonControl()
        {
            Checkers = new List<Func<bool>>();
        }

        /// <summary>
        /// Создает экземпляр класса ButtonControl с пустым списком делегатов проверки и списком кнопок
        /// </summary>
        /// <param name="buttons">Список кнопок, зависимых от делегатов проверки</param>
        public ButtonControl(params Button[] buttons) : this()
        {
            this.buttons = new List<Button>();
            this.buttons.AddRange(buttons);
        }

        /// <summary>
        /// Создает экземпляр класса ButtonControl имеющий список делегатов проверки и список кнопок
        /// </summary>
        /// <param name="checkers">Список делегатов проверки</param>
        /// <param name="buttons">Список кнопок, зависимых от делегатов проверки</param>
        public ButtonControl(List<Func<bool>> checkers, params Button[] buttons)
        {
            this.buttons = new List<Button>();
            Checkers = checkers;
            this.buttons.AddRange(buttons);
        }
        
        /// <summary>
        /// Добавляет делегат проверки в список
        /// </summary>
        /// <param name="checker"></param>
        public void Add(Func<bool> checker)
        {
            Checkers.Add(checker);
        }

        /// <summary>
        /// Запускает все делегаты проверки и в зависимости от результата меняет свойство IsEnabled у кнопок
        /// </summary>
        public void Check()
        {
            bool isEnabled = IsEnabled;
            foreach (var b in buttons)
                b.IsEnabled = isEnabled;
        }
    }
}
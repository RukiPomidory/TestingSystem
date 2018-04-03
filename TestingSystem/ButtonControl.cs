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
        public bool IsEnabled
        {
            get
            {
                foreach (var c in Checkers)
                    if (c()) return true;
                return false;
            }
        }

        public ButtonControl()
        {
            Checkers = new List<Func<bool>>();
        }

        public ButtonControl(params Button[] buttons) : this()
        {
            this.buttons = new List<Button>();
            this.buttons.AddRange(buttons);
        }

        public ButtonControl(List<Func<bool>> checkers, params Button[] buttons)
        {
            this.buttons = new List<Button>();
            Checkers = checkers;
            this.buttons.AddRange(buttons);
        }

        public void Add(Func<bool> checker)
        {
            Checkers.Add(checker);
        }

        public void Check()
        {
            bool isEnabled = IsEnabled;
            foreach (var b in buttons)
                b.IsEnabled = isEnabled;
        }
    }
}
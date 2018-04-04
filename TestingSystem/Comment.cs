using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestingSystem
{
    /// <summary>
    /// Инкапсулирует комментарии к результатам
    /// </summary>
    public class Comment
    {
        public string FirstGroup { get; set; }
        public string SecondGroup { get; set; }
        public string ThirdGroup { get; set; }

        /// <summary>
        /// Создает экземпляр Comment
        /// </summary>
        /// <param name="first">Именительный падеж</param>
        /// <param name="second">Родительный падеж, единственное число</param>
        /// <param name="third">Родительный падеж, множественное число</param>
        public Comment(string first, string second, string third)
        {
            FirstGroup = first;
            SecondGroup = second;
            ThirdGroup = third;
        }

        /// <summary>
        /// Возвращает комментарий в зависимости от переданного числа по правилам русского языка
        /// </summary>
        /// <param name="rating">Количество, от которого зависит комментарий</param>
        /// <returns>Комментарий</returns>
        public string GetAComment(int rating)
        {
            int rem = rating % 10;
            if (rem == 1) return FirstGroup;
            if (rem > 1 && rem < 5) return SecondGroup;
            return ThirdGroup;
        }
    }
}

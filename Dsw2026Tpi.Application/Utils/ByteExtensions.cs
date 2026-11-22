using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Utils
{
    public static class ByteExtensions
    {
        public static string ToSpanishDayOfWeek(this byte day) 
        {
            return day switch
            {
                1 =>  "Lunes",
                2 =>  "Martes", 
                3 =>  "Miércoles",
                4 =>  "Jueves" ,
                5 =>  "Viernes",
                6 =>  "Sábado",
                7 => "Domingo",
                _ => throw new ArgumentException($"El día '{day}' no es válido.")
            };
        }
    }
}

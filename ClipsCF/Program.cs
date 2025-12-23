using System;
using System.Windows.Forms;

namespace ClipsFormsExample
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                // Пытаемся запустить форму
                Application.Run(new ClipsFormsExample());
            }
            catch (Exception ex)
            {
                // Если упало при запуске (в конструкторе), покажем почему
                MessageBox.Show("Критическая ошибка при запуске:\n\n" + ex.ToString(), 
                    "Ошибка запуска", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Error);
            }
        }
    }
}

/*
 Начать для автоматического выбора
 Готово для ручного управления
 
Примеры ввода:

Обычный, но с I-mac
(assert (spread-slot (card-id "III Императрица") (time-modifier "Настоящее")))
(assert (spread-slot (card-id "0 Шут") (time-modifier "Будущее")))
(assert (spread-slot (card-id "I Маг") (time-modifier "Прошлое")))
(assert (answer (text "Начать")))

Безумный:
(assert (spread-slot (card-id "II Жрица") (time-modifier "Настоящее")))
(assert (spread-slot (card-id "VII Колесница") (time-modifier "Будущее")))
(assert (spread-slot (card-id "0 Шут") (time-modifier "Прошлое")))
(assert (answer (text "Начать")))

Работает и обычный ввод:

0 Шут прошлое
I Маг будущее
Начать


маг прошлое
влюбленные настоящее
жрица будущее
начать

Колесо фортуны прошлое
маг настоящее
влюбленные будущее
готово
*/
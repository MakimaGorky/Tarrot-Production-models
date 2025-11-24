using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TarotProductionSystem
{
    // --- МОДЕЛЬ ПРАВИЛА ---
    public class Rule
    {
        public string Id { get; set; }
        public List<string> Conditions { get; set; } = new List<string>(); // Антецеденты (ЕСЛИ)
        public string Conclusion { get; set; }       // Консеквент (ТО)
        public string Explanation { get; set; }      // Объяснение (ПОЧЕМУ/ЧТО ЭТО)

        public override string ToString()
        {
            return $"[{Id}]: IF ({string.Join(" AND ", Conditions)}) THEN {Conclusion}";
        }
    }

    // --- ПАРСЕР БАЗЫ ЗНАНИЙ ---
    public static class KnowledgeBaseLoader
    {
        public static List<Rule> LoadFromFile(string filePath)
        {
            var rules = new List<Rule>();

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"ОШИБКА: Файл {filePath} не найден.");
                return rules;
            }

            // Читаем все строки (Encoding.UTF8 важно для русского языка)
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);

            foreach (var line in lines)
            {
                string cleanLine = line.Trim();
                
                // Пропускаем пустые строки и комментарии
                if (string.IsNullOrEmpty(cleanLine) || cleanLine.StartsWith("//"))
                    continue;

                var parts = cleanLine.Split(';');

                // Очищаем пробелы вокруг каждого элемента
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();

                try
                {
                    // ВАРИАНТ 1: Факты первого уровня (Карта -> Значение)
                    // Структура: ID; Карта; Значение; Объяснение
                    // Пример: факт_1; 0 Шут; Новое начало; Символизирует обнуление...
                    if (parts.Length == 4)
                    {
                        rules.Add(new Rule
                        {
                            Id = parts[0],
                            Conditions = new List<string> { parts[1] },
                            Conclusion = parts[2],
                            Explanation = parts[3]
                        });
                    }
                    // ВАРИАНТ 2: Правила синтеза (Условие1 + Условие2 -> Вывод)
                    // Структура: ID; Условие1; Условие2; Вывод; Объяснение
                    // Пример: правило_1; Мудрость; Пробуждение; Понимание; Глубинное знание...
                    else if (parts.Length == 5)
                    {
                        rules.Add(new Rule
                        {
                            Id = parts[0],
                            Conditions = new List<string> { parts[1], parts[2] },
                            Conclusion = parts[3],
                            Explanation = parts[4]
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка парсинга строки: {line}\n{ex.Message}");
                }
            }

            Console.WriteLine($"Успешно загружено правил: {rules.Count}");
            return rules;
        }
    }

    // --- МАШИНА ВЫВОДА (Inference Engine) ---
    public class InferenceEngine
    {
        private List<Rule> _knowledgeBase;
        private HashSet<string> _facts;

        public InferenceEngine(List<Rule> rules, List<string> initialFacts)
        {
            _knowledgeBase = rules;
            _facts = new HashSet<string>(initialFacts); // Case-sensitive для простоты, лучше привести к Lower
        }

        // ПРЯМОЙ ВЫВОД
        public void RunForwardChaining()
        {
            Console.WriteLine("\n--- ЗАПУСК ПРЯМОГО ВЫВОДА ---");
            bool newFactAdded = true;
            int step = 1;

            while (newFactAdded)
            {
                newFactAdded = false;
                foreach (var rule in _knowledgeBase)
                {
                    // Если вывод уже известен, пропускаем
                    if (_facts.Contains(rule.Conclusion)) continue;

                    // Если все условия соблюдены
                    if (rule.Conditions.All(c => _facts.Contains(c)))
                    {
                        Console.WriteLine($"Шаг {step++}: Сработало [{rule.Id}]");
                        Console.WriteLine($"   ЕСЛИ: {string.Join(" + ", rule.Conditions)}");
                        Console.WriteLine($"   ТО:   {rule.Conclusion}");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"   КОММЕНТАРИЙ: {rule.Explanation}");
                        Console.ResetColor();
                        
                        _facts.Add(rule.Conclusion);
                        newFactAdded = true;
                    }
                }
            }
            Console.WriteLine("--- Вывод завершен ---");
        }

        // ОБРАТНЫЙ ВЫВОД (Для проверки гипотезы)
        public bool RunBackwardChaining(string targetGoal)
        {
            Console.WriteLine($"\n--- ЗАПУСК ОБРАТНОГО ВЫВОДА (Цель: {targetGoal}) ---");
            Stack<string> goalStack = new Stack<string>();
            goalStack.Push(targetGoal);

            HashSet<string> provedGoals = new HashSet<string>(_facts);
            List<string> derivationLog = new List<string>();
            HashSet<string> currentPath = new HashSet<string>(); // Для защиты от циклов

            while (goalStack.Count > 0)
            {
                string currentGoal = goalStack.Peek();

                if (provedGoals.Contains(currentGoal))
                {
                    goalStack.Pop();
                    currentPath.Remove(currentGoal);
                    continue;
                }

                // Ищем правила, ведущие к цели
                var applicableRules = _knowledgeBase.Where(r => r.Conclusion == currentGoal).ToList();

                if (applicableRules.Count == 0)
                {
                    // Тупик
                    return false; 
                }

                bool ruleApplied = false;
                bool hasPendingSubgoals = false;

                foreach (var rule in applicableRules)
                {
                    if (rule.Conditions.Any(c => currentPath.Contains(c))) continue; // Цикл

                    var missingConditions = rule.Conditions.Where(c => !provedGoals.Contains(c)).ToList();

                    if (missingConditions.Count == 0)
                    {
                        // Доказано!
                        provedGoals.Add(currentGoal);
                        derivationLog.Add($"Доказано: {currentGoal}\n     (Правило {rule.Id}: {rule.Explanation})");
                        
                        goalStack.Pop();
                        currentPath.Remove(currentGoal);
                        ruleApplied = true;
                        break;
                    }
                    else
                    {
                        foreach (var condition in missingConditions)
                        {
                            goalStack.Push(condition);
                            currentPath.Add(condition);
                        }
                        hasPendingSubgoals = true;
                        break; // Идем вглубь
                    }
                }

                if (!ruleApplied && !hasPendingSubgoals) return false;
            }

            Console.WriteLine("ЦЕЛЬ ДОСТИГНУТА! Цепочка вывода:");
            foreach (var log in derivationLog) Console.WriteLine(" -> " + log);
            return true;
        }
        
        public void PrintFinalAdvice()
        {
            // Ищем факты, которые похожи на советы (длинные предложения из 4 уровня)
            // В твоей базе советы длинные, можно фильтровать по длине или по наличию в правилах 4 уровня
            Console.WriteLine("\n=== ИТОГОВОЕ ПРЕДСКАЗАНИЕ ===");
            foreach(var f in _facts)
            {
                // Простая эвристика: если факт длинный (совет) и не является входной картой/временем
                if(f.Length > 30) 
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"★ {f}");
                    Console.ResetColor();
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // 1. Создаем файл базы данных (чтобы он физически существовал)
            string dbFileName = "database.txt";
            CreateDatabaseFileIfNeeded(dbFileName);

            // 2. Загружаем правила
            var rules = KnowledgeBaseLoader.LoadFromFile(dbFileName);

            if (rules.Count == 0) return;

            // 3. Сценарий: Пользователь вытянул карты
            // Давай возьмем цепочку, которая точно сработает по твоей базе:
            // 0 Шут -> Новое начало
            // XX Суд -> Пробуждение
            // (Правило 2: Новое начало + Пробуждение -> Понимание) WRONG?
            // Давай проверим "Правило 1": Мудрость (из II Жрица) + Пробуждение (из XX Суд) -> Понимание
            // Правило 61: Понимание + Будущее -> Осознанный путь к будущему
            // Правило 110: Осознанный путь к будущему + Устойчивость -> Совет...
            
            // Нам нужно получить "Устойчивость". Это "IV Император" -> "Стабильность"... 
            // Хм, в правиле 4: Изобилие + Забота -> Устойчивость. (Императрица)
            
            // СОБЕРЕМ ТЕСТОВЫЙ НАБОР ФАКТОВ:
            var initialFacts = new List<string> 
            { 
                "II Жрица",      // Даст "Мудрость"
                "XX Суд",        // Даст "Пробуждение"
                "III Императрица", // Даст "Изобилие", "Забота" -> "Устойчивость"
                "Будущее"        // Контекст времени
            };

            Console.WriteLine($"Входные данные: {string.Join(", ", initialFacts)}");

            // 4. Запуск прямого вывода
            var engine = new InferenceEngine(rules, initialFacts);
            engine.RunForwardChaining();
            
            engine.PrintFinalAdvice();

            // 5. Запуск обратного вывода (проверка)
            // Проверим, получили ли мы конкретный совет
            string goal = "Будьте уверены в своих действиях, и они принесут стабильность в будущем.";
            Console.WriteLine("\nНажмите Enter для проверки обратного вывода...");
            Console.ReadLine();
            
            var engineBack = new InferenceEngine(rules, initialFacts);
            bool success = engineBack.RunBackwardChaining(goal);
            
            if(!success) Console.WriteLine("Не удалось доказать цель по заданным картам.");

            Console.ReadLine();
        }

        // Хелпер для создания файла с твоими данными
        static void CreateDatabaseFileIfNeeded(string filename)
        {
            if (!File.Exists(filename))
            {
                // Тут я просто вставлю часть твоих данных для примера, 
                // но в реальности ты просто положишь свой заполненный файл рядом с exe
                Console.WriteLine("Файл базы данных не найден. Создайте файл database.txt с вашими правилами.");
                // В реальном проекте просто создай текстовый файл в папке проекта
            }
        }
    }
}
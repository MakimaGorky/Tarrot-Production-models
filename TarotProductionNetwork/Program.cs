using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TarotProductionSystem
{
    // --- КЛАСС ФАКТА ---
    public class Fact
    {
        public string Value { get; set; }
        public string TimeTag { get; set; } // "Прошлое", "Настоящее", "Будущее", "Mixed"
        

        public override string ToString() => $"{Value} [{TimeTag}]";

        public override bool Equals(object obj)
        {
            if (obj is Fact f) return Value == f.Value && TimeTag == f.TimeTag;
            return false;
        }

        public override int GetHashCode() => (Value + TimeTag).GetHashCode();
    }

    // --- КЛАСС ПРАВИЛА ---
    public class Rule
    {
        public string Id { get; set; }
        public List<string> Conditions { get; set; } = new List<string>();
        public string Conclusion { get; set; }
        public string Explanation { get; set; }
    }

    // --- ЗАГРУЗЧИК ---
    public static class KnowledgeBaseLoader
    {
        public static List<Rule> LoadFromFile(string filePath)
        {
            var rules = new List<Rule>();
            if (!File.Exists(filePath)) return rules;

            foreach (var line in File.ReadAllLines(filePath, Encoding.UTF8))
            {
                var clean = line.Trim();
                if (string.IsNullOrEmpty(clean) || clean.StartsWith("//")) continue;
                var parts = clean.Split(';').Select(p => p.Trim()).ToArray();

                try
                {
                    if (parts.Length == 4)
                        rules.Add(new Rule { Id = parts[0], Conditions = { parts[1] }, Conclusion = parts[2], Explanation = parts[3] });
                    else if (parts.Length == 5)
                        rules.Add(new Rule { Id = parts[0], Conditions = { parts[1], parts[2] }, Conclusion = parts[3], Explanation = parts[4] });
                }
                catch { }
            }
            return rules;
        }
    }

    // --- ДВИЖОК ВЫВОДА ---
    public class InferenceEngine
    {
        private List<Rule> _knowledgeBase;
        private HashSet<Fact> _facts;
        
        // Флаг стратегии: true = разрешить Mixed работать как Wildcard
        private bool _allowMixedStrategy; 

        public InferenceEngine(List<Rule> rules, bool allowMixedStrategy)
        {
            _knowledgeBase = rules;
            _facts = new HashSet<Fact>();
            _allowMixedStrategy = allowMixedStrategy;
        }

        public void AddInitialFact(string cardName, string timeTag)
        {
            _facts.Add(new Fact { Value = cardName, TimeTag = timeTag });
        }

        public void PrintInitialFacts()
        {
            Console.WriteLine("\nИспользуем расклад:");

            foreach (var fact in _facts)
            {
                Console.WriteLine(fact.ToString());
            }
        }

        public void RunForwardChaining()
        {
            Console.WriteLine($"\n--- ЗАПУСК ПРЯМОГО ВЫВОДА (Режим: {(_allowMixedStrategy ? "Свободный/Mixed" : "Строгий")}) ---");
            bool newFactAdded = true;
            int step = 1;

            while (newFactAdded)
            {
                newFactAdded = false;
                foreach (var rule in _knowledgeBase)
                {
                    // Пропускаем, если такой вывод с (потенциально) любым тегом уже есть, 
                    // чтобы не спамить, но для точности лучше проверять конкретные факты внутри.
                    
                    var matchingFacts = FindMatchingFactsForConditions(rule.Conditions);

                    foreach (var combination in matchingFacts)
                    {
                        // Пытаемся определить временной тег результата
                        string newTimeTag = ResolveTimeTag(combination, rule);

                        // Если newTimeTag == null, значит несовместимость времени
                        if (newTimeTag == null) continue;

                        Fact newFact = new Fact { Value = rule.Conclusion, TimeTag = newTimeTag };

                        if (!_facts.Contains(newFact))
                        {
                            Console.WriteLine($"Шаг {step++}: [{rule.Id}]");
                            Console.WriteLine($"   Использовано: {string.Join(" + ", combination.Select(f => f.ToString()))}");
                            Console.WriteLine($"   Получено:     {newFact}");
                            // Console.WriteLine($"   Суть: {rule.Explanation}"); // Можно раскомментить
                            
                            _facts.Add(newFact);
                            newFactAdded = true;
                        }
                    }
                }
            }
        }

        // --- ГЛАВНАЯ ЛОГИКА ОБРАБОТКИ ВРЕМЕНИ ---
        private string ResolveTimeTag(List<Fact> ingredients, Rule rule)
        {
            var tags = ingredients.Select(f => f.TimeTag).Distinct().ToList();

            // 1. ПРОВЕРКА: Требует ли правило конкретного времени (Слой 3)?
            var requiredTime = rule.Conditions.FirstOrDefault(c => IsTimeKeyword(c));
            
            if (requiredTime != null)
            {
                // Правило вида: "Значение + Будущее -> Вывод"
                // Находим факт-значение (не являющийся словом "Будущее")
                var mainFact = ingredients.FirstOrDefault(f => !IsTimeKeyword(f.Value));
                if (mainFact == null) return null;

                bool exactMatch = (mainFact.TimeTag == requiredTime);
                
                // ВАЖНО: Если включена Loose стратегия, то "Mixed" считается подходящим для всего
                bool mixedMatch = _allowMixedStrategy && (mainFact.TimeTag == "Mixed");

                if (exactMatch || mixedMatch)
                {
                    // Если сработал mixedMatch, мы "схлопываем" неопределенность в конкретное время,
                    // которого требовало правило. (Mixed + FutureRequirement -> Future Result)
                    return requiredTime; 
                }
                else
                {
                    return null; // Например, есть "Прошлое", а правило требует "Будущее" -> Отказ.
                }
            }

            // 2. ОБЫЧНЫЙ СИНТЕЗ (Слой 1 и 2)
            if (tags.Count == 1) return tags[0]; // Все ингредиенты из одного времени
            
            // Если ингредиенты разные (Past + Present), результат -> Mixed
            return "Mixed"; 
        }

        private bool IsTimeKeyword(string s) => s == "Будущее" || s == "Настоящее" || s == "Прошлое";

        private List<List<Fact>> FindMatchingFactsForConditions(List<string> conditions)
        {
            var results = new List<List<Fact>>();

            if (conditions.Count == 1)
            {
                foreach (var f in _facts) if (f.Value == conditions[0]) results.Add(new List<Fact> { f });
            }
            else if (conditions.Count == 2)
            {
                string c1 = conditions[0];
                string c2 = conditions[1];
                var facts1 = _facts.Where(f => f.Value == c1).ToList();
                var facts2 = _facts.Where(f => f.Value == c2).ToList();

                if (!IsTimeKeyword(c2)) // Обычное правило (Fact + Fact)
                {
                    foreach (var f1 in facts1)
                        foreach (var f2 in facts2)
                            results.Add(new List<Fact> { f1, f2 });
                }
                else // Правило времени (Fact + "TimeKeyword")
                {
                    // Возвращаем (Fact + Пустышка с именем требуемого времени)
                    foreach (var f1 in facts1)
                        results.Add(new List<Fact> { f1, new Fact { Value = c2, TimeTag = "RuleRequirement" } });
                    
                    // Обратный порядок (TimeKeyword + Fact) - если вдруг в базе правила написаны иначе
                    if(IsTimeKeyword(c1) && !IsTimeKeyword(c2))
                    {
                         var factsReal = _facts.Where(f => f.Value == c2).ToList();
                         foreach (var f in factsReal)
                             results.Add(new List<Fact> { new Fact { Value = c1, TimeTag = "RuleRequirement" }, f });
                    }
                }
            }
            return results;
        }

        public void PrintFinalAdvice()
        {
            Console.WriteLine("\n=== ПОЛУЧЕННЫЕ ПРЕДСКАЗАНИЯ ===");
            var adviceFound = false;
            foreach (var f in _facts)
            {
                // Фильтр: длинные строки, похожие на советы
                if (f.Value.Length > 40) 
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"★ {f.Value} ({f.TimeTag})");
                    Console.ResetColor();
                    adviceFound = true;
                }
            }
            if(!adviceFound) Console.WriteLine("Нет сформированных советов. Попробуйте включить режим 'Mixed' или изменить карты.");
        }
    }

    class FactPresets
    {
        // башня(П), дьявол(Н), колесо(Б) - пример с разным разрешением конфликтов.
        public static void TotalSlayEmotion(InferenceEngine engine)
        {
            engine.AddInitialFact("XVI Башня", "Прошлое");
            engine.AddInitialFact("XV Дьявол", "Настоящее");
            engine.AddInitialFact("X Колесо Фортуны", "Будущее");
        }

        // Жрица(П), Суд(Н), Император(Б)
        public static void CompletelyDefault(InferenceEngine engine)
        {
            engine.AddInitialFact("II Жрица", "Прошлое");
            engine.AddInitialFact("XX Суд", "Настоящее");
            engine.AddInitialFact("III Императрица", "Будущее");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string dbFile = "database.txt"; 
            // Создание файла-заглушки, если нет (для теста)
            if(!File.Exists(dbFile)) { File.WriteAllText(dbFile, "// Вставьте сюда вашу базу данных"); Console.WriteLine("Файл создан, заполните его!"); return; }

            var rules = KnowledgeBaseLoader.LoadFromFile(dbFile);

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== СИСТЕМА ПРЕДСКАЗАНИЙ ТАРО ===");
                Console.WriteLine("1. Строгий режим (Честный вывод: Прошлое + Настоящее != Будущее)");
                Console.WriteLine("2. Свободный режим (Mixed стратегия: Смешанный опыт подходит ко всему)");
                Console.WriteLine("0. Выход");
                Console.Write("Выберите режим: ");
                
                var key = Console.ReadLine();
                if (key == "0") break;

                bool allowMixed = (key == "2");

                var engine = new InferenceEngine(rules, allowMixed);
                
                // Начальный расклад
                FactPresets.CompletelyDefault(engine);
                engine.PrintInitialFacts();
                
                // Запуск
                engine.RunForwardChaining();
                engine.PrintFinalAdvice();

                Console.WriteLine("\nНажмите Enter, чтобы вернуться в меню...");
                Console.ReadLine();
            }
        }
    }
}
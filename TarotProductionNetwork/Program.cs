using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TarotExpertSystem
{
    // ==========================================
    // 1. ЛОГИЧЕСКОЕ ЯДРО (Expert System Core)
    // ==========================================

    public class Fact
    {
        public string Value { get; set; }
        public string TimeTag { get; set; }
        public override string ToString() => $"{Value} [{TimeTag}]";
        public override bool Equals(object obj) => obj is Fact f && Value == f.Value && TimeTag == f.TimeTag;
        public override int GetHashCode() => (Value + TimeTag).GetHashCode();
    }

    public class Rule
    {
        public string Id { get; set; }
        public List<string> Conditions { get; set; } = new List<string>();
        public string Conclusion { get; set; }
        public string Explanation { get; set; }
    }

    public class InferenceEngine
    {
        private List<Rule> _knowledgeBase;
        private HashSet<Fact> _facts;
        private bool _allowMixedStrategy;
        
        // События для отправки логов в интерфейс
        public Action<string, Color> OnLog; 
        public Action<string> OnAdviceFound;

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

        public void RunForwardChaining()
        {
            OnLog?.Invoke($"--- ЗАПУСК (Режим: {(_allowMixedStrategy ? "Свободный" : "Строгий")}) ---", Color.Black);
            bool newFactAdded = true;
            int step = 1;

            while (newFactAdded)
            {
                newFactAdded = false;
                foreach (var rule in _knowledgeBase)
                {
                    var matchingFacts = FindMatchingFactsForConditions(rule.Conditions);

                    foreach (var combination in matchingFacts)
                    {
                        string newTimeTag = ResolveTimeTag(combination, rule);
                        if (newTimeTag == null) continue;

                        Fact newFact = new Fact { Value = rule.Conclusion, TimeTag = newTimeTag };

                        if (!_facts.Contains(newFact))
                        {
                            // Формируем красивую строку использованных фактов
                            var used = string.Join(" + ", combination.Select(f => 
                                f.TimeTag == "RuleRequirement" ? $"Context:{f.Value}" : f.Value));

                            OnLog?.Invoke($"Шаг {step++}: [{rule.Id}]", Color.DarkBlue);
                            OnLog?.Invoke($"   Основа: {used}", Color.Black);
                            OnLog?.Invoke($"   Вывод:  {newFact.Value} [{newFact.TimeTag}]", Color.DarkGreen);
                            // OnLog?.Invoke($"   Суть:   {rule.Explanation}", Color.Gray);
                            OnLog?.Invoke("", Color.Black); // Пустая строка

                            _facts.Add(newFact);
                            newFactAdded = true;
                        }
                    }
                }
            }
            
            FindAndSendAdvice();
        }

        private void FindAndSendAdvice()
        {
            bool found = false;
            foreach (var f in _facts)
            {
                // Эвристика: советы - длинные строки
                if (f.Value.Length > 35) 
                {
                    OnAdviceFound?.Invoke(f.Value);
                    found = true;
                }
            }
            if (!found) OnAdviceFound?.Invoke("Совет сформировать не удалось. Попробуйте другой режим или добавьте карт.");
        }

        private string ResolveTimeTag(List<Fact> ingredients, Rule rule)
        {
            var requiredTime = rule.Conditions.FirstOrDefault(c => IsTimeKeyword(c));
            if (requiredTime != null)
            {
                var mainFact = ingredients.FirstOrDefault(f => !IsTimeKeyword(f.Value));
                if (mainFact == null) return null;

                bool exact = mainFact.TimeTag == requiredTime;
                bool mixed = _allowMixedStrategy && mainFact.TimeTag == "Mixed";

                return (exact || mixed) ? requiredTime : null;
            }

            var tags = ingredients.Select(f => f.TimeTag).Distinct().ToList();
            if (tags.Count == 1) return tags[0];
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
                var f1s = _facts.Where(f => f.Value == c1).ToList();
                var f2s = _facts.Where(f => f.Value == c2).ToList();

                if (!IsTimeKeyword(c2))
                {
                    foreach (var fa in f1s) foreach (var fb in f2s) results.Add(new List<Fact> { fa, fb });
                }
                else
                {
                    foreach (var fa in f1s) results.Add(new List<Fact> { fa, new Fact { Value = c2, TimeTag = "RuleRequirement" } });
                    if(IsTimeKeyword(c1) && !IsTimeKeyword(c2))
                    {
                        var realFacts = _facts.Where(f => f.Value == c2).ToList();
                        foreach (var rf in realFacts) results.Add(new List<Fact> { new Fact { Value = c1, TimeTag = "RuleRequirement" }, rf });
                    }
                }
            }
            return results;
        }
    }

    public static class KnowledgeBaseLoader
    {
        public static List<Rule> LoadFromFile(string filePath)
        {
            var rules = new List<Rule>();
            if (!File.Exists(filePath)) return rules;
            foreach (var line in File.ReadAllLines(filePath, Encoding.UTF8))
            {
                var parts = line.Split(';').Select(p => p.Trim()).ToArray();
                if (parts.Length == 4) rules.Add(new Rule { Id = parts[0], Conditions = { parts[1] }, Conclusion = parts[2], Explanation = parts[3] });
                else if (parts.Length == 5) rules.Add(new Rule { Id = parts[0], Conditions = { parts[1], parts[2] }, Conclusion = parts[3], Explanation = parts[4] });
            }
            return rules;
        }
    }

    // ==========================================
    // 2. ГРАФИЧЕСКИЙ ИНТЕРФЕЙС (WinForms)
    // ==========================================

    public class MainForm : Form
    {
        // Данные
        private List<Rule> rules;
        private List<string> allCardNames;
        private Dictionary<string, List<string>> selectedCards = new Dictionary<string, List<string>>();

        // Компоненты UI
        private RichTextBox logBox;
        private TextBox resultBox;
        private FlowLayoutPanel pnlPast, pnlPresent, pnlFuture;
        private RadioButton rbStrict, rbLoose;
        
        public MainForm()
        {
            // 1. Настройка формы
            this.Text = "Продукционная модель: Таро Эксперт";
            this.Size = new Size(1200, 800);
            this.Font = new Font("Segoe UI", 10);
            
            // 2. Загрузка данных
            LoadData();

            // 3. Создание UI
            InitializeCustomComponents();
        }

        private void LoadData()
        {
            string dbFile = "database.txt";
            if (!File.Exists(dbFile))
            {
                File.WriteAllText(dbFile, "// Создайте базу данных!");
                MessageBox.Show("Файл database.txt не найден! Создан пустой файл.");
            }
            rules = KnowledgeBaseLoader.LoadFromFile(dbFile);
            
            // Извлекаем имена карт из правил 1-го уровня (где 1 условие и ID начинается с "факт")
            allCardNames = rules
                .Where(r => r.Conditions.Count == 1 && r.Id.StartsWith("факт", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Conditions[0])
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            selectedCards["Прошлое"] = new List<string>();
            selectedCards["Настоящее"] = new List<string>();
            selectedCards["Будущее"] = new List<string>();
        }

        private void InitializeCustomComponents()
        {
            // --- ГЛАВНЫЙ СПЛИТ (Вертикальный) ---
            var mainSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 400 };
            
            // === ЛЕВАЯ ЧАСТЬ (Логи) ===
            var groupLog = new GroupBox { Text = "Ход вывода", Dock = DockStyle.Fill, Padding = new Padding(10) };
            logBox = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.None };
            groupLog.Controls.Add(logBox);
            mainSplit.Panel1.Controls.Add(groupLog);

            // === ПРАВАЯ ЧАСТЬ (Конфигурация + Результат) ===
            var rightPanel = new Panel { Dock = DockStyle.Fill };
            
            // Зона результата (Снизу)
            var groupResult = new GroupBox { Text = "Итоговое предсказание", Dock = DockStyle.Bottom, Height = 150 };
            resultBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
            groupResult.Controls.Add(resultBox);
            rightPanel.Controls.Add(groupResult);

            // Зона управления (Сверху правой части)
            var panelControls = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(5) };
            rbStrict = new RadioButton { Text = "Строгий режим", Location = new Point(10, 15), AutoSize = true };
            rbLoose = new RadioButton { Text = "Свободный режим (Mixed)", Location = new Point(150, 15), AutoSize = true, Checked = true };
            var btnReset = new Button { Text = "Сброс", Location = new Point(350, 10), Size = new Size(100, 40), BackColor = Color.LightCoral };
            var btnRun = new Button { Text = "ПОЛУЧИТЬ ПРЕДСКАЗАНИЕ", Location = new Point(460, 10), Size = new Size(200, 40), BackColor = Color.LightGreen, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            
            btnReset.Click += (s, e) => ResetAll();
            btnRun.Click += (s, e) => RunInference();

            panelControls.Controls.AddRange(new Control[] { rbStrict, rbLoose, btnReset, btnRun });
            rightPanel.Controls.Add(panelControls);

            // Зона Карт (Центр правой части) - Делим на 3 колонки
            var cardsTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));

            pnlPast = CreateColumn("ПРОШЛОЕ");
            pnlPresent = CreateColumn("НАСТОЯЩЕЕ");
            pnlFuture = CreateColumn("БУДУЩЕЕ");

            cardsTable.Controls.Add(pnlPast, 0, 0);
            cardsTable.Controls.Add(pnlPresent, 1, 0);
            cardsTable.Controls.Add(pnlFuture, 2, 0);

            rightPanel.Controls.Add(cardsTable); // Добавляем таблицу ПОСЛЕ кнопок и ДО результата
            cardsTable.BringToFront(); // Чтобы она заняла оставшееся место
            panelControls.Dock = DockStyle.Top; // Re-dock to ensure order
            groupResult.Dock = DockStyle.Bottom;

            mainSplit.Panel2.Controls.Add(rightPanel);
            this.Controls.Add(mainSplit);

            // Инициализация пустых кнопок
            AddPlusButton(pnlPast, "Прошлое");
            AddPlusButton(pnlPresent, "Настоящее");
            AddPlusButton(pnlFuture, "Будущее");
        }

        private FlowLayoutPanel CreateColumn(string title)
        {
            var pnl = new FlowLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                FlowDirection = FlowDirection.TopDown, 
                WrapContents = false, 
                AutoScroll = true,
                BackColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle
            };
            var lbl = new Label { Text = title, AutoSize = false, Width = 200, Height = 30, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.DarkSlateGray };
            pnl.Controls.Add(lbl);
            return pnl;
        }

        // --- ЛОГИКА ДОБАВЛЕНИЯ КАРТ ---

        private void AddPlusButton(FlowLayoutPanel panel, string timeTag)
        {
            var btn = new Button 
            { 
                Text = "+", 
                Font = new Font("Consolas", 24), 
                Size = new Size(160, 100), 
                Cursor = Cursors.Hand 
            };
            
            btn.Click += (s, e) => 
            {
                // Показать меню выбора
                var contextMenu = new ContextMenuStrip();
                // Фильтруем карты, которые уже выбраны ГДЕ УГОДНО
                var allUsed = selectedCards.Values.SelectMany(x => x).ToHashSet();
                
                foreach (var card in allCardNames)
                {
                    if (!allUsed.Contains(card))
                    {
                        contextMenu.Items.Add(card, null, (sender, args) => 
                        {
                            // 1. Добавляем карту в логику
                            selectedCards[timeTag].Add(card);
                            
                            // 2. Заменяем кнопку на Виджет Карты
                            int index = panel.Controls.IndexOf(btn);
                            panel.Controls.Remove(btn);
                            
                            var cardWidget = CreateCardWidget(card);
                            panel.Controls.Add(cardWidget);
                            panel.Controls.SetChildIndex(cardWidget, index);

                            // 3. Добавляем новую кнопку "+" снизу
                            AddPlusButton(panel, timeTag);
                        });
                    }
                }
                if (contextMenu.Items.Count == 0) contextMenu.Items.Add("Все карты уже на столе!");
                contextMenu.Show(btn, new Point(0, btn.Height));
            };

            panel.Controls.Add(btn);
        }

        private Control CreateCardWidget(string cardName)
        {
            var panel = new Panel { Size = new Size(160, 140), Margin = new Padding(3, 3, 3, 10) };
            
            // ASCII Art Label
            var lblAscii = new Label 
            { 
                Text = GenerateAsciiArt(cardName), 
                Font = new Font("Courier New", 7, FontStyle.Regular), // Моноширинный шрифт!
                Dock = DockStyle.Fill, 
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };

            panel.Controls.Add(lblAscii);
            return panel;
        }

        // --- ГЕНЕРАТОР ASCII ---
        private string GenerateAsciiArt(string cardName)
        {
            // Пытаемся разбить название, чтобы оно влезло
            string shortName = cardName.Replace("Факт", "").Trim();
            string[] lines = SplitString(shortName, 10);
            
            string l1 = lines.Length > 0 ? lines[0] : "";
            string l2 = lines.Length > 1 ? lines[1] : "";
            string l3 = lines.Length > 2 ? lines[2] : "";

            // Римские цифры (простая эвристика по первым буквам или просто заглушка)
            string roman = shortName.Split(' ')[0]; 

            return 
$@" .----------.
 | {roman,-8} |
 |          |
 |{PadCenter(l1,10)}|
 |{PadCenter(l2,10)}|
 |{PadCenter(l3,10)}|
 |          |
 |  TAROT   |
 '----------'";
        }

        private string PadCenter(string s, int width)
        {
            if (s.Length >= width) return s.Substring(0, width);
            int left = (width - s.Length) / 2;
            return s.PadLeft(left + s.Length).PadRight(width);
        }

        private string[] SplitString(string str, int maxLen)
        {
            var list = new List<string>();
            var words = str.Split(' ');
            string line = "";
            foreach(var w in words)
            {
                if ((line + w).Length > maxLen) { list.Add(line.Trim()); line = ""; }
                line += w + " ";
            }
            if(line.Length > 0) list.Add(line.Trim());
            return list.ToArray();
        }

        // --- ЗАПУСК ВЫВОДА ---
        private void RunInference()
        {
            logBox.Clear();
            resultBox.Clear();
            
            // Проверка на пустоту
            if (!selectedCards.Values.Any(x => x.Count > 0))
            {
                MessageBox.Show("Выберите хотя бы одну карту!");
                return;
            }

            var engine = new InferenceEngine(rules, rbLoose.Checked);
            
            // Подписка на события логгера
            engine.OnLog = (msg, color) => 
            {
                logBox.SelectionStart = logBox.TextLength;
                logBox.SelectionLength = 0;
                logBox.SelectionColor = color;
                logBox.AppendText(msg + "\n");
                logBox.ScrollToCaret();
            };

            engine.OnAdviceFound = (advice) => 
            {
                resultBox.AppendText("★ " + advice + "\r\n\r\n");
            };

            // Загрузка фактов из UI в Engine
            foreach (var kvp in selectedCards)
            {
                string time = kvp.Key; // Прошлое, Настоящее...
                foreach (var card in kvp.Value)
                {
                    engine.AddInitialFact(card, time);
                }
            }

            // Поехали
            engine.RunForwardChaining();
        }

        private void ResetAll()
        {
            selectedCards["Прошлое"].Clear();
            selectedCards["Настоящее"].Clear();
            selectedCards["Будущее"].Clear();
            
            pnlPast.Controls.Clear(); pnlPast.Controls.Add(new Label { Text = "ПРОШЛОЕ", AutoSize=false, Width=200, Height=30, TextAlign=ContentAlignment.MiddleCenter, Font=new Font("Segoe UI", 12, FontStyle.Bold) });
            pnlPresent.Controls.Clear(); pnlPresent.Controls.Add(new Label { Text = "НАСТОЯЩЕЕ", AutoSize=false, Width=200, Height=30, TextAlign=ContentAlignment.MiddleCenter, Font=new Font("Segoe UI", 12, FontStyle.Bold) });
            pnlFuture.Controls.Clear(); pnlFuture.Controls.Add(new Label { Text = "БУДУЩЕЕ", AutoSize=false, Width=200, Height=30, TextAlign=ContentAlignment.MiddleCenter, Font=new Font("Segoe UI", 12, FontStyle.Bold) });

            AddPlusButton(pnlPast, "Прошлое");
            AddPlusButton(pnlPresent, "Настоящее");
            AddPlusButton(pnlFuture, "Будущее");

            logBox.Clear();
            resultBox.Clear();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
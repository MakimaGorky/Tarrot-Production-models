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
    // 1. МОДЕЛЬ ДАННЫХ
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

    // ==========================================
    // 2. ДВИЖОК ВЫВОДА
    // ==========================================
    public class InferenceEngine
    {
        private List<Rule> _knowledgeBase;
        private HashSet<Fact> _facts;
        private bool _allowMixedStrategy;
        
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
            OnLog?.Invoke($"--- ЗАПУСК ПРЯМОГО ВЫВОДА ---", Color.Black);
            OnLog?.Invoke($"Режим: {(_allowMixedStrategy ? "Свободный (Смешанный)" : "Строгий")}", Color.Gray);
            
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
                            var used = string.Join(" + ", combination.Select(f => 
                                f.TimeTag == "RuleRequirement" ? $"Context:{f.Value}" : f.Value));

                            OnLog?.Invoke($"Шаг {step++}: [{rule.Id}]", Color.DarkBlue);
                            OnLog?.Invoke($"   Основа: {used}", Color.Black);
                            OnLog?.Invoke($"   Вывод:  {newFact.Value} [{newFact.TimeTag}]", Color.DarkGreen);
                            OnLog?.Invoke("", Color.Black); 

                            _facts.Add(newFact);
                            newFactAdded = true;
                        }
                    }
                }
            }
            FindAndSendAdvice();
        }

        public void RunBackwardChaining(string targetGoal, List<string> knownCards)
        {
            OnLog?.Invoke($"--- ЗАПУСК ОБРАТНОГО ВЫВОДА ---", Color.Black);
            OnLog?.Invoke($"Цель: {targetGoal}", Color.Purple);
            OnLog?.Invoke("", Color.Black);

            Stack<string> goalStack = new Stack<string>();
            goalStack.Push(targetGoal);
            HashSet<string> provenFacts = new HashSet<string>(knownCards);
            HashSet<string> currentPath = new HashSet<string>();
            List<string> successLog = new List<string>();

            while (goalStack.Count > 0)
            {
                string currentGoal = goalStack.Peek();
                if (provenFacts.Contains(currentGoal))
                {
                    goalStack.Pop();
                    currentPath.Remove(currentGoal);
                    continue;
                }

                var rules = _knowledgeBase.Where(r => r.Conclusion == currentGoal).ToList();
                if (rules.Count == 0)
                {
                     OnLog?.Invoke($"! Тупик: '{currentGoal}' не выводится ни одним правилом.", Color.Red);
                    return; 
                }

                bool ruleApplied = false;
                foreach (var rule in rules)
                {
                    if (rule.Conditions.Any(c => currentPath.Contains(c))) continue;

                    var missingConditions = rule.Conditions
                        .Where(c => !provenFacts.Contains(c) && !IsTimeKeyword(c))
                        .ToList();

                    if (missingConditions.Count == 0)
                    {
                        provenFacts.Add(currentGoal);
                        successLog.Add($"[{rule.Id}] Доказано: {currentGoal} (Из: {string.Join(", ", rule.Conditions)})");
                        goalStack.Pop();
                        currentPath.Remove(currentGoal);
                        ruleApplied = true;
                        break;
                    }
                    else
                    {
                        foreach (var cond in missingConditions) { goalStack.Push(cond); currentPath.Add(cond); }
                        ruleApplied = true; 
                        break; 
                    }
                }

                if (!ruleApplied) { OnLog?.Invoke($"! Не удалось доказать: {currentGoal}", Color.Red); return; }
            }

            OnLog?.Invoke("=== ЦЕЛЬ ДОСТИГНУТА! ===", Color.DarkGreen);
            OnLog?.Invoke("Цепочка (только сработавшие правила):", Color.DarkGreen);
            foreach (var line in successLog) OnLog?.Invoke(" -> " + line, Color.Black);
            OnAdviceFound?.Invoke($"Цель '{targetGoal}' подтверждена!");
        }

        private void FindAndSendAdvice()
        {
            bool found = false;
            foreach (var f in _facts) if (f.Value.Length > 35) { OnAdviceFound?.Invoke(f.Value); found = true; }
            if (!found) OnAdviceFound?.Invoke("Совет не найден.");
        }

        private string ResolveTimeTag(List<Fact> ingredients, Rule rule)
        {
            var requiredTime = rule.Conditions.FirstOrDefault(c => IsTimeKeyword(c));
            if (requiredTime != null)
            {
                var mainFact = ingredients.FirstOrDefault(f => !IsTimeKeyword(f.Value));
                if (mainFact == null) return null;
                return (mainFact.TimeTag == requiredTime || (_allowMixedStrategy && mainFact.TimeTag == "Смешанное")) ? requiredTime : null;
            }
            var tags = ingredients.Select(f => f.TimeTag).Distinct().ToList();
            return tags.Count == 1 ? tags[0] : "Смешанное";
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
                    foreach (var fa in f1s) foreach (var fb in f2s) results.Add(new List<Fact> { fa, fb });
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
                if (parts.Length < 4) continue;
                if (parts.Length == 4) rules.Add(new Rule { Id = parts[0], Conditions = { parts[1] }, Conclusion = parts[2], Explanation = parts[3] });
                else if (parts.Length == 5) rules.Add(new Rule { Id = parts[0], Conditions = { parts[1], parts[2] }, Conclusion = parts[3], Explanation = parts[4] });
            }
            return rules;
        }
    }

    // ==========================================
    // 3. ГРАФИЧЕСКИЙ ИНТЕРФЕЙС (ИСПРАВЛЕННЫЙ)
    // ==========================================
    public class MainForm : Form
    {
        private List<Rule> rules;
        private List<string> allCardNames;
        private Dictionary<string, List<string>> selectedCards = new Dictionary<string, List<string>>();

        // Компоненты
        private RichTextBox logBox;
        private TextBox resultBox;
        private FlowLayoutPanel pnlPast, pnlPresent, pnlFuture;
        private RadioButton rbStrict, rbLoose;
        private TextBox txtGoal;
        private Button btnBackward;

        public MainForm()
        {
            this.Text = "Продукционная модель: Таро Эксперт";
            this.Size = new Size(1350, 850); // Чуть шире для удобства
            this.Font = new Font("Segoe UI", 10);
            this.StartPosition = FormStartPosition.CenterScreen;
            
            LoadData();
            InitializeCustomComponents();
        }

        private void LoadData()
        {
            string dbFile = "database.txt";
            if (!File.Exists(dbFile)) File.WriteAllText(dbFile, "// Empty");
            rules = KnowledgeBaseLoader.LoadFromFile(dbFile);
            
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
            // 1. Главный сплит (Лог слева / Рабочая область справа)
            var mainSplit = new SplitContainer 
            { 
                Dock = DockStyle.Fill, 
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1 // Фиксируем левую панель при ресайзе
            };
            
            // ЛЕВАЯ ЧАСТЬ (ЛОГ)
            var groupLog = new GroupBox { Text = "Лог вывода", Dock = DockStyle.Fill, Padding = new Padding(10) };
            logBox = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.None };
            groupLog.Controls.Add(logBox);
            mainSplit.Panel1.Controls.Add(groupLog);

            // ПРАВАЯ ЧАСТЬ (ВСЁ ОСТАЛЬНОЕ)
            var rightPanel = new Panel { Dock = DockStyle.Fill };
            
            // --- ПАНЕЛЬ УПРАВЛЕНИЯ (ВЕРХ) ---
            var controlPanel = new GroupBox { Text = "Управление", Dock = DockStyle.Top, Height = 110 };
            
            // Левая группа (Прямой вывод) - выравнивание по сетке
            int col1X = 20;  // Радио-кнопки
            int col2X = 20;  // Кнопки действий
            int row1Y = 25;  // Первая строка
            int row2Y = 60;  // Вторая строка

            // Радио кнопки
            rbStrict = new RadioButton { Text = "Строгий", Location = new Point(col1X, row1Y), AutoSize = true };
            rbLoose = new RadioButton { Text = "Свободный (Смешанный)", Location = new Point(col1X + 100, row1Y), AutoSize = true, Checked = true };
            
            // Кнопки Прямого вывода и Сброса
            var btnRun = new Button { Text = "ПРЯМОЙ ВЫВОД", Location = new Point(col2X, row2Y), Size = new Size(160, 35), BackColor = Color.LightGreen, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            var btnReset = new Button { Text = "Сброс карт", Location = new Point(col2X + 170, row2Y), Size = new Size(100, 35), BackColor = Color.LightCoral };

            // Правая группа (Обратный вывод) - сдвигаем правее, чтобы не слипалось
            int col3X = 350; // Начало зоны обратного вывода
            
            var lblGoal = new Label { Text = "Цель для обратного вывода (факты или совет):", Location = new Point(col3X, row1Y + 2), AutoSize = true };
            txtGoal = new TextBox { Location = new Point(col3X, row2Y + 2), Width = 380, Height = 30 }; // Делаем широким
            btnBackward = new Button { Text = "ПРОВЕРИТЬ ГИПОТЕЗУ", Location = new Point(col3X + 390, row2Y), Size = new Size(180, 35), BackColor = Color.LightSkyBlue };

            controlPanel.Controls.AddRange(new Control[] { rbStrict, rbLoose, btnRun, btnReset, lblGoal, txtGoal, btnBackward });
            rightPanel.Controls.Add(controlPanel);

            // --- ПАНЕЛЬ РЕЗУЛЬТАТА (НИЗ) ---
            var groupResult = new GroupBox { Text = "Итоговый совет / Результат", Dock = DockStyle.Bottom, Height = 100 };
            resultBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ScrollBars = ScrollBars.Vertical };
            groupResult.Controls.Add(resultBox);
            rightPanel.Controls.Add(groupResult);

            // --- КАРТЫ (ЦЕНТР) ---
            // Используем TableLayoutPanel для равномерного распределения
            var cardsTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.WhiteSmoke };
            cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));

            pnlPast = CreateColumn("ПРОШЛОЕ");
            pnlPresent = CreateColumn("НАСТОЯЩЕЕ");
            pnlFuture = CreateColumn("БУДУЩЕЕ");

            cardsTable.Controls.Add(pnlPast, 0, 0);
            cardsTable.Controls.Add(pnlPresent, 1, 0);
            cardsTable.Controls.Add(pnlFuture, 2, 0);
            
            // Важно: BringToFront, чтобы таблица заняла всё оставшееся место между верхом и низом
            rightPanel.Controls.Add(cardsTable);
            cardsTable.BringToFront();

            // Привязка событий
            btnRun.Click += (s, e) => RunForward();
            btnReset.Click += (s, e) => ResetAll();
            btnBackward.Click += (s, e) => RunBackward();

            mainSplit.Panel2.Controls.Add(rightPanel);
            this.Controls.Add(mainSplit);

            // 4. Инициализация кнопок добавления карт
            AddPlusButton(pnlPast, "Прошлое");
            AddPlusButton(pnlPresent, "Настоящее");
            AddPlusButton(pnlFuture, "Будущее");

            // 5. Установка разделителя на 1/3 (делаем это в конце инициализации)
            // Используем событие Load, чтобы размеры формы точно применились
            this.Load += (s, e) => { mainSplit.SplitterDistance = this.ClientSize.Width / 3; };
        }

        private void RunForward()
        {
            if (!CheckCards()) return;
            PrepareRun();
            var engine = new InferenceEngine(rules, rbLoose.Checked);
            AttachLoggers(engine);
            foreach (var kvp in selectedCards) foreach (var card in kvp.Value) engine.AddInitialFact(card, kvp.Key);
            engine.RunForwardChaining();
        }

        private void RunBackward()
        {
            if (!CheckCards()) return;
            string goal = txtGoal.Text.Trim();
            if (string.IsNullOrEmpty(goal) || goal.Length < 2) { MessageBox.Show("Введите цель!"); return; }
            PrepareRun();
            var engine = new InferenceEngine(rules, true);
            AttachLoggers(engine);
            var knownCards = selectedCards.Values.SelectMany(x => x).ToList();
            engine.RunBackwardChaining(goal, knownCards);
        }

        private void AttachLoggers(InferenceEngine engine)
        {
            engine.OnLog = (msg, color) => { logBox.SelectionStart = logBox.TextLength; logBox.SelectionLength = 0; logBox.SelectionColor = color; logBox.AppendText(msg + "\n"); logBox.ScrollToCaret(); };
            engine.OnAdviceFound = (adv) => resultBox.Text = adv;
        }

        private void PrepareRun() { logBox.Clear(); resultBox.Clear(); }
        private bool CheckCards() { if (!selectedCards.Values.Any(x => x.Count > 0)) { MessageBox.Show("Добавьте карты!"); return false; } return true; }

        private FlowLayoutPanel CreateColumn(string title)
        {
            var pnl = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.WhiteSmoke, BorderStyle = BorderStyle.FixedSingle };
            pnl.Controls.Add(new Label { Text = title, AutoSize = false, Width = 200, Height = 30, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.DarkSlateGray });
            return pnl;
        }

        private void AddPlusButton(FlowLayoutPanel panel, string timeTag)
        {
            var btn = new Button { Text = "+", Font = new Font("Consolas", 24), Size = new Size(160, 100), Cursor = Cursors.Hand };
            btn.Click += (s, e) => 
            {
                var contextMenu = new ContextMenuStrip();
                var allUsed = selectedCards.Values.SelectMany(x => x).ToHashSet();
                foreach (var card in allCardNames)
                {
                    if (!allUsed.Contains(card))
                    {
                        contextMenu.Items.Add(card, null, (sender, args) => 
                        {
                            selectedCards[timeTag].Add(card);
                            int index = panel.Controls.IndexOf(btn);
                            panel.Controls.Remove(btn);
                            panel.Controls.Add(CreateCardWidget(card));
                            AddPlusButton(panel, timeTag);
                        });
                    }
                }
                contextMenu.Show(btn, new Point(0, btn.Height));
            };
            panel.Controls.Add(btn);
        }

        private Control CreateCardWidget(string cardName)
        {
            var p = new Panel { Size = new Size(160, 140), Margin = new Padding(3, 3, 3, 10) };
            p.Controls.Add(new Label { Text = GenerateAsciiArt(cardName), Font = new Font("Courier New", 7), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White });
            return p;
        }

        private string GenerateAsciiArt(string c)
        {
            string s = c.Replace("Факт", "").Trim();
            string[] lines = s.Split(' ').Aggregate(new List<string>{""}, (l, w) => { if((l.Last()+w).Length>10) l.Add(""); l[l.Count-1]+=w+" "; return l; }).Select(x=>x.Trim()).ToArray();
            string r = s.Split(' ')[0];
            return $".----------.\n| {r,-8} |\n|          |\n|{PC(lines.ElementAtOrDefault(0),10)}|\n|{PC(lines.ElementAtOrDefault(1),10)}|\n|{PC(lines.ElementAtOrDefault(2),10)}|\n|          |\n|  TAROT   |\n'----------'";
        }
        private string PC(string s, int w) { s=s??""; if(s.Length>=w) return s.Substring(0,w); int l=(w-s.Length)/2; return s.PadLeft(l+s.Length).PadRight(w); }

        private void ResetAll()
        {
            selectedCards.Keys.ToList().ForEach(k => selectedCards[k].Clear());
            pnlPast.Controls.Clear(); pnlPresent.Controls.Clear(); pnlFuture.Controls.Clear();
            pnlPast.Controls.Add(new Label { Text = "ПРОШЛОЕ", AutoSize=false, Width=200, Height=30, TextAlign=ContentAlignment.MiddleCenter, Font=new Font("Segoe UI", 12, FontStyle.Bold) });
            pnlPresent.Controls.Add(new Label { Text = "НАСТОЯЩЕЕ", AutoSize=false, Width=200, Height=30, TextAlign=ContentAlignment.MiddleCenter, Font=new Font("Segoe UI", 12, FontStyle.Bold) });
            pnlFuture.Controls.Add(new Label { Text = "БУДУЩЕЕ", AutoSize=false, Width=200, Height=30, TextAlign=ContentAlignment.MiddleCenter, Font=new Font("Segoe UI", 12, FontStyle.Bold) });
            AddPlusButton(pnlPast, "Прошлое"); AddPlusButton(pnlPresent, "Настоящее"); AddPlusButton(pnlFuture, "Будущее");
            logBox.Clear(); resultBox.Clear();
        }
    }

    static class Program { [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); } }
}
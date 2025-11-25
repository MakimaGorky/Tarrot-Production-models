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

    public class GoalFrame
    {
        public string Goal;                 // Какую цель доказываем
        public List<Rule> ApplicableRules;  // Список всех правил для этой цели
        public int CurrentRuleIndex;        // Какое правило пробуем сейчас
        public string LastTriedSubgoal;

        public GoalFrame(string goal, List<Rule> rules)
        {
            Goal = goal;
            ApplicableRules = rules;
            CurrentRuleIndex = 0;
            LastTriedSubgoal = null;
        }
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
            OnLog?.Invoke($"Режим: {(_allowMixedStrategy ? "Свободный (Mixed)" : "Строгий")}", Color.Gray);
            
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

        // Не проверяет временные модификаторы карт.
        public void RunBackwardChaining(string targetGoal, List<string> knownCards)
        {
            OnLog?.Invoke($"--- ЗАПУСК ОБРАТНОГО ВЫВОДА (С возвратом) ---", Color.Black);
            OnLog?.Invoke($"Цель: {targetGoal}", Color.Purple);

            // Множество доказанных фактов
            HashSet<string> provenFacts = new HashSet<string>(knownCards);
            
            // Стек состояний
            Stack<GoalFrame> stack = new Stack<GoalFrame>();
            
            // Цели в стеке
            HashSet<string> currentPath = new HashSet<string>();

            // Лог успешных применений
            List<string> successLog = new List<string>();

            // 1. Кладем исходную цель
            var initialRules = _knowledgeBase.Where(r => r.Conclusion == targetGoal).ToList();
            stack.Push(new GoalFrame(targetGoal, initialRules));
            currentPath.Add(targetGoal);

            bool success = false;

            while (stack.Count > 0)
            {
                var frame = stack.Peek();

                // СЛУЧАЙ 1: Цель уже доказана (кем-то ранее или является фактом)
                if (provenFacts.Contains(frame.Goal))
                {
                    currentPath.Remove(frame.Goal);
                    stack.Pop();
                    continue;
                }

                // СЛУЧАЙ 2: Кончились правила для этой цели - откат назад
                if (frame.CurrentRuleIndex >= frame.ApplicableRules.Count)
                {
                    currentPath.Remove(frame.Goal);
                    stack.Pop();
                    
                    // Если стек пуст после этого, значит мы провалили доказательство
                    if (stack.Count == 0) success = false;
                    
                    continue;
                }

                // СЛУЧАЙ 3: Пробуем текущее правило
                var rule = frame.ApplicableRules[frame.CurrentRuleIndex];

                // Проверка на цикл
                if (rule.Conditions.Any(c => currentPath.Contains(c)))
                {
                    frame.CurrentRuleIndex++;
                    continue;
                }

                // Ищем ПЕРВОЕ НЕДОКАЗАННОЕ условие
                // Игнорируем теги времени IsTimeKeyword потому что вот так
                string missingCondition = rule.Conditions
                    .FirstOrDefault(c => !provenFacts.Contains(c) && !IsTimeKeyword(c));

                if (missingCondition == null)
                {
                    // УРА! Все условия доказаны (или их нет)!
                    provenFacts.Add(frame.Goal);
                    successLog.Add($"[{rule.Id}] {frame.Goal} <= ({string.Join(", ", rule.Conditions)})");
                    
                    currentPath.Remove(frame.Goal);
                    stack.Pop();
                    
                    if (stack.Count == 0) success = true;
                }
                else
                {
                    // Если мы здесь, мы либо только зашли в правило, либо вернулись после неудачи.
                    
                    // Создаем фрейм для подцели
                    var subRules = _knowledgeBase.Where(r => r.Conclusion == missingCondition).ToList();
                    
                    // Если для подцели вообще нет правил и это не факт -> тупик
                    if (subRules.Count == 0 && !provenFacts.Contains(missingCondition))
                    {
                        frame.CurrentRuleIndex++;
                        continue;
                    }
                    
                    // Отличаем "уже неудачный смешарик" или "тут первый раз"
                    if (frame.LastTriedSubgoal == missingCondition) 
                    {
                        // Мы уже пробовали эту подцель для этого правила, и раз мы снова здесь, 
                        // а факт не в provenFacts - значит правило провалено.
                        frame.CurrentRuleIndex++;
                        frame.LastTriedSubgoal = null;
                    }
                    else
                    {
                        // Первый раз пробуем эту подцель
                        frame.LastTriedSubgoal = missingCondition;
                        currentPath.Add(missingCondition);
                        stack.Push(new GoalFrame(missingCondition, subRules));
                    }
                }
            }

            if (success)
            {
                OnLog?.Invoke("=== ГИПОТЕЗА ПОДТВЕРЖДЕНА! ===", Color.DarkGreen);
                foreach (var line in successLog) OnLog?.Invoke(line, Color.Black);
                OnAdviceFound?.Invoke("Утверждение верно.");
            }
            else
            {
                OnLog?.Invoke("! Гипотеза не подтвердилась (нет путей вывода).", Color.Red);
            }
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
                return (mainFact.TimeTag == requiredTime || (_allowMixedStrategy && mainFact.TimeTag == "Mixed")) ? requiredTime : null;
            }
            var tags = ingredients.Select(f => f.TimeTag).Distinct().ToList();
            return tags.Count == 1 ? tags[0] : "Mixed";
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
    // 3. ГРАФИЧЕСКИЙ ИНТЕРФЕЙС
    // ==========================================
    public class MainForm : Form
    {
        private List<Rule> rules;
        private List<string> allCardNames;
        private Dictionary<string, List<string>> selectedCards = new Dictionary<string, List<string>>();

        private RichTextBox logBox;
        private TextBox resultBox;
        private FlowLayoutPanel pnlPast, pnlPresent, pnlFuture;
        private RadioButton rbStrict, rbLoose;
        private TextBox txtGoal;
        private Button btnBackward;

        public MainForm()
        {
            this.Text = "Продукционная модель: Таро Эксперт";
            this.Size = new Size(1350, 850);
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
            var mainSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, FixedPanel = FixedPanel.Panel1 };
            
            // ЛОГ (Слева)
            var groupLog = new GroupBox { Text = "Лог вывода", Dock = DockStyle.Fill, Padding = new Padding(10) };
            logBox = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.None };
            groupLog.Controls.Add(logBox);
            mainSplit.Panel1.Controls.Add(groupLog);

            // ПРАВАЯ ЧАСТЬ
            var rightPanel = new Panel { Dock = DockStyle.Fill };
            
            // ПАНЕЛЬ УПРАВЛЕНИЯ
            var controlPanel = new GroupBox { Text = "Управление", Dock = DockStyle.Top, Height = 110 };
            
            int col1X = 20; int col2X = 20; int row1Y = 25; int row2Y = 60;
            rbStrict = new RadioButton { Text = "Строгий", Location = new Point(col1X, row1Y), AutoSize = true };
            rbLoose = new RadioButton { Text = "Свободный (Mixed)", Location = new Point(col1X + 100, row1Y), AutoSize = true, Checked = true };
            var btnRun = new Button { Text = "ПРЯМОЙ ВЫВОД", Location = new Point(col2X, row2Y), Size = new Size(160, 35), BackColor = Color.LightGreen, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            var btnReset = new Button { Text = "Сброс карт", Location = new Point(col2X + 170, row2Y), Size = new Size(100, 35), BackColor = Color.LightCoral };

            int col3X = 350; 
            var lblGoal = new Label { Text = "Цель для обратного вывода:", Location = new Point(col3X, row1Y + 2), AutoSize = true };
            txtGoal = new TextBox { Location = new Point(col3X, row2Y + 2), Width = 380, Height = 30 };
            btnBackward = new Button { Text = "ПРОВЕРИТЬ ГИПОТЕЗУ", Location = new Point(col3X + 390, row2Y), Size = new Size(180, 35), BackColor = Color.LightSkyBlue };

            controlPanel.Controls.AddRange(new Control[] { rbStrict, rbLoose, btnRun, btnReset, lblGoal, txtGoal, btnBackward });
            rightPanel.Controls.Add(controlPanel);

            // РЕЗУЛЬТАТ
            var groupResult = new GroupBox { Text = "Итоговый совет / Результат", Dock = DockStyle.Bottom, Height = 100 };
            resultBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ScrollBars = ScrollBars.Vertical };
            groupResult.Controls.Add(resultBox);
            rightPanel.Controls.Add(groupResult);

            // КАРТЫ
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
            rightPanel.Controls.Add(cardsTable);
            cardsTable.BringToFront();

            btnRun.Click += (s, e) => RunForward();
            btnReset.Click += (s, e) => ResetAll();
            btnBackward.Click += (s, e) => RunBackward();

            mainSplit.Panel2.Controls.Add(rightPanel);
            this.Controls.Add(mainSplit);

            AddPlusButton(pnlPast, "Прошлое");
            AddPlusButton(pnlPresent, "Настоящее");
            AddPlusButton(pnlFuture, "Будущее");

            this.Load += (s, e) => { mainSplit.SplitterDistance = this.ClientSize.Width / 3; };
        }

        // --- ЛОГИКА UI ---

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
                // Показываем меню добавления новой карты
                ShowCardSelectionMenu(btn, timeTag, null);
            };
            panel.Controls.Add(btn);
        }

        // ФИЧА: Замена карты!!!
        private Control CreateCardWidget(string cardName, string timeTag)
        {
            var p = new Panel { Size = new Size(160, 240), Margin = new Padding(3, 3, 3, 10) };
            
            // Label хранит ASCII арт, а в Tag мы кладем настоящее имя карты
            var lbl = new Label 
            { 
                Text = GenerateAsciiArt(cardName), 
                Font = new Font("Courier New", 7), 
                Dock = DockStyle.Fill, 
                TextAlign = ContentAlignment.MiddleCenter, 
                BorderStyle = BorderStyle.FixedSingle, 
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Tag = cardName
            };

            // Обработчик клика для замены
            lbl.Click += (s, e) => 
            {
                ShowCardSelectionMenu(lbl, timeTag, (string)lbl.Tag);
            };

            p.Controls.Add(lbl);
            return p;
        }

        // Универсальное меню выбора (и для добавления, и для замены)
        private void ShowCardSelectionMenu(Control anchorControl, string timeTag, string currentCardToReplace)
        {
            var contextMenu = new ContextMenuStrip();
            var allUsed = selectedCards.Values.SelectMany(x => x).ToHashSet();

            foreach (var card in allCardNames)
            {
                // Можно выбрать карту, если она нигде не используется, 
                // ЛИБО если это та же самая карта, на которую мы кликнули (чтобы можно было "отменить", выбрав её же)
                if (!allUsed.Contains(card) || card == currentCardToReplace)
                {
                    contextMenu.Items.Add(card, null, (sender, args) => 
                    {
                        // Если кликнули на ту же самую, ничего не делаем
                        if (card == currentCardToReplace) return;

                        if (currentCardToReplace == null)
                        {
                            // === ЛОГИКА ДОБАВЛЕНИЯ НОВОЙ ===
                            selectedCards[timeTag].Add(card);
                            
                            // Заменяем кнопку "+" на карту
                            var panel = (FlowLayoutPanel)anchorControl.Parent;
                            int index = panel.Controls.IndexOf(anchorControl);
                            panel.Controls.Remove(anchorControl);
                            
                            var newWidget = CreateCardWidget(card, timeTag);
                            panel.Controls.Add(newWidget);
                            panel.Controls.SetChildIndex(newWidget, index);

                            // Добавляем новый "+" вниз
                            AddPlusButton(panel, timeTag);
                        }
                        else
                        {
                            // === ЛОГИКА ЗАМЕНЫ СУЩЕСТВУЮЩЕЙ ===
                            // 1. Обновляем данные
                            int listIndex = selectedCards[timeTag].IndexOf(currentCardToReplace);
                            if (listIndex != -1) 
                            {
                                selectedCards[timeTag][listIndex] = card;
                            }

                            // 2. Обновляем UI (Label)
                            var lbl = (Label)anchorControl;
                            lbl.Text = GenerateAsciiArt(card);
                            lbl.Tag = card;
                        }
                    });
                }
            }
            
            if (contextMenu.Items.Count == 0) contextMenu.Items.Add("Нет доступных карт");
            contextMenu.Show(anchorControl, new Point(0, anchorControl.Height));
        }

        private string GenerateSimpleAsciiArt(string c)
        {
            string s = c.Replace("Факт", "").Trim();
            string[] lines = s.Split(' ').Aggregate(new List<string>{""}, (l, w) => { if((l.Last()+w).Length>10) l.Add(""); l[l.Count-1]+=w+" "; return l; }).Select(x=>x.Trim()).ToArray();
            string r = s.Split(' ')[0];
            return $".----------.\n| {r,-8} |\n|          |\n|{PC(lines.ElementAtOrDefault(0),10)}|\n|{PC(lines.ElementAtOrDefault(1),10)}|\n|{PC(lines.ElementAtOrDefault(2),10)}|\n|          |\n|  TAROT   |\n'----------'";
        }
        private string GenerateAsciiArt(string c)
        {
            string s = c.Replace("Факт", "").Trim();
            Console.WriteLine(s);
            if (ASCII_Tarot.Cards.ContainsKey(s))
            {
                return ASCII_Tarot.Cards[s];
            }
            
            return GenerateSimpleAsciiArt(s);
        }
        private string PC(string s, int w) { s=s??""; if(s.Length>=w) return s.Substring(0,w); int l=(w-s.Length)/2; return s.PadLeft(l+s.Length).PadRight(w); }

        // --- RUNNERS ---

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
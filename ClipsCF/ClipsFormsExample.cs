using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CLIPSNET;

// Подключаем стандартные библиотеки Windows вместо Microsoft.Speech
using System.Speech.Synthesis;
using System.Speech.Recognition;

namespace ClipsFormsExample
{
    public partial class ClipsFormsExample : Form
    {
        private CLIPSNET.Environment clips = new CLIPSNET.Environment();
        
        // Используем классы из System.Speech, которые у меня не установлены на компьютере
        private SpeechSynthesizer synth;
        private SpeechRecognitionEngine recogn;

        public ClipsFormsExample()
        {
            InitializeComponent();
            InitializeSpeech();
        }

        private void InitializeSpeech()
        {
            synth = new SpeechSynthesizer();
            try
            {
                synth.SetOutputToDefaultAudioDevice();
                
                // Ищем русские голоса
                var voices = synth.GetInstalledVoices(System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
                foreach (var v in voices)
                    voicesBox.Items.Add(v.VoiceInfo.Name);
                
                if (voicesBox.Items.Count > 0)
                {
                    voicesBox.SelectedIndex = 0;
                    synth.SelectVoice(voices[0].VoiceInfo.Name);
                }
            }
            catch (Exception ex) 
            { 
                outputBox.Text += "Ошибка синтеза: " + ex.Message + System.Environment.NewLine; 
            }

            try
            {
                // Пытаемся найти русский распознаватель в системе
                var RecognizerInfo = SpeechRecognitionEngine.InstalledRecognizers()
                    .FirstOrDefault(ri => ri.Culture.Name == "ru-RU");
                
                if (RecognizerInfo != null)
                {
                    recogn = new SpeechRecognitionEngine(RecognizerInfo);
                    recogn.SpeechRecognized += Recogn_SpeechRecognized;
                    recogn.SetInputToDefaultAudioDevice();
                }
                else
                {
                    outputBox.Text += "Внимание: Русский язык для распознавания речи не установлен в Windows!" + System.Environment.NewLine;
                    outputBox.Text += "Вы можете вводить ответы текстом через поле кода (командой assert)." + System.Environment.NewLine;
                }
            }
            catch (Exception ex) 
            { 
                outputBox.Text += "Ошибка инициализации распознавания: " + ex.Message + System.Environment.NewLine; 
            }
        }

        private void NewRecognPhrases(List<string> phrases)
        {
            if (recogn == null || phrases.Count == 0) return;

            try
            {
                outputBox.Text += "Слушаю варианты: " + string.Join(", ", phrases) + System.Environment.NewLine;
                var choices = new Choices();
                choices.Add(phrases.ToArray());

                var gb = new GrammarBuilder();
                gb.Culture = recogn.RecognizerInfo.Culture;
                gb.Append(choices);

                var gr = new Grammar(gb);
                
                // В System.Speech лучше использовать LoadGrammarAsync или просто LoadGrammar
                recogn.UnloadAllGrammars();
                recogn.LoadGrammar(gr);
                
                // Запускаем асинхронное распознавание (многократное)
                try {
                    recogn.RecognizeAsync(RecognizeMode.Multiple);
                } catch { 
                    // Иногда вылетает, если уже запущено - игнорируем
                }
            }
            catch (Exception ex)
            {
                outputBox.Text += "Ошибка обновления грамматики: " + ex.Message + System.Environment.NewLine;
            }
        }

        private void Recogn_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence < 0.5) return; // Фильтр шума

            // Останавливаем, чтобы не слышать самого себя, если нужно, 
            // но для диалога лучше оставить или перезапускать.
            // System.Speech работает стабильнее в фоне.
            
            outputBox.Text += "Вы сказали: " + e.Result.Text + System.Environment.NewLine;
            
            // Передаем ответ в CLIPS
            string clipCmd = $"(assert (answer (text \"{e.Result.Text}\")))";
            // В будущем ждёт I-mac
            clips.Eval(clipCmd);
            
            clips.Run();
            HandleResponse();
        }

        private void LoadTarotSystem()
        {
            clips.Clear();
            
            string[] files = { "00_templates-cf.clp", "01_facts-cf.clp", "02_rules-cf.clp" };
            
            foreach (var file in files)
            {
                if (System.IO.File.Exists(file))
                {
                    string content = System.IO.File.ReadAllText(file, Encoding.UTF8);
                    clips.LoadFromString(content);
                    outputBox.Text += $"Загружен {file}" + System.Environment.NewLine;
                }
                else
                {
                    outputBox.Text += $"ОШИБКА: Файл {file} не найден!" + System.Environment.NewLine;
                }
            }

            clips.Reset();
            clips.Run();
            HandleResponse();
        }

        // Метод для починки кодировки
        private string CorrectString(string brokenString)
        {
            // Получаем "сырые" байты, как их видит система
            byte[] bytes = Encoding.Default.GetBytes(brokenString);
            // Принудительно читаем их как UTF-8
            return Encoding.UTF8.GetString(bytes);
        }
        
        private void HandleResponse()
        {
            try
            {
                // Получаем факт-прокси
                var result = clips.Eval("(find-fact ((?f ioproxy)) TRUE)");
                if (!(result is MultifieldValue mv) || mv.Count == 0) return;

                FactAddressValue fv = (FactAddressValue)mv[0];

                // Читаем сообщения
                MultifieldValue messages = (MultifieldValue)fv["messages"];
                for (int i = 0; i < messages.Count; i++)
                {
                    LexemeValue msgVal = (LexemeValue)messages[i];
                    string message = CorrectString(msgVal.Value);

                    outputBox.Text += "Оракул: " + message + System.Environment.NewLine;
                    
                    // Проговариваем
                    if (synth != null && synth.State == SynthesizerState.Ready) 
                        synth.SpeakAsync(message);
                }

                // Читаем варианты ответов
                MultifieldValue answers = (MultifieldValue)fv["answers"];
                if (answers.Count > 0)
                {
                    var phrases = new List<string>();
                    for (int i = 0; i < answers.Count; i++)
                    {
                        LexemeValue ansVal = (LexemeValue)answers[i];
                        phrases.Add(CorrectString(ansVal.Value));
                    }
                    // Обновляем распознавание речи
                    NewRecognPhrases(phrases);
                }

                // Очищаем буфер сообщений в CLIPS
                if (messages.Count > 0)
                {
                    clips.Eval("(assert (clearmessage))");
                    clips.Run();
                }
            }
            catch (Exception ex)
            {
                outputBox.Text += "Ошибка обработки ответа: " + ex.Message + System.Environment.NewLine;
            }
        }

        private void nextBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(codeBox.Text))
            {
                clips.Run();
                HandleResponse();
                return;
            }

            string[] lines = codeBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string cmd = line.Trim();
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                try
                {
                    // 1. Системные команды CLIPS (скобки)
                    if (cmd.StartsWith("(") || cmd.StartsWith(";"))
                    {
                        clips.Eval(cmd);
                        outputBox.Text += "SYS: " + cmd + System.Environment.NewLine;
                    }
                    // 2. Попытка распознать ввод карты (Smart Input)
                    else if (TryHandleCardInput(cmd, out string clipsAssert))
                    {
                        clips.Eval(clipsAssert);
                        outputBox.Text += $"Вы (карта): {cmd}" + System.Environment.NewLine;
                    }
                    // 3. Просто ответ текстом
                    else
                    {
                        // Экранируем кавычки на всякий случай
                        string safeText = cmd.Replace("\"", "\\\""); 
                        string wrapper = $"(assert (answer (text \"{safeText}\")))";
                        clips.Eval(wrapper);
                        outputBox.Text += $"Вы: {cmd}" + System.Environment.NewLine;
                    }
                }
                catch (Exception ex)
                {
                    outputBox.Text += $"Ошибка команды '{cmd}': {ex.Message}" + System.Environment.NewLine;
                }
            }

            codeBox.Clear();
            clips.Run();
            HandleResponse();
        }

        // Умный парсер ввода
        private bool TryHandleCardInput(string input, out string clipsCommand)
        {
            clipsCommand = "";
            string lower = input.ToLower().Trim();
            
            // 1. Пытаемся найти коэффициент уверенности в конце строки
            float confidence = 1.0f;
            string[] parts = lower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool hasCf = false;

            if (parts.Length > 2)
            {
                string lastPart = parts[parts.Length - 1];
                
                // Пробуем распарсить как число с точкой (международный формат)
                if (float.TryParse(lastPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float valDot))
                {
                    if (valDot >= 0 && valDot <= 1.0) { confidence = valDot; hasCf = true; }
                }
                // Если не вышло, пробуем с запятой (локальный формат)
                else if (float.TryParse(lastPart.Replace('.', ','), out float valComma))
                {
                     if (valComma >= 0 && valComma <= 1.0) { confidence = valComma; hasCf = true; }
                }
            }

            // Если нашли коэффициент, удаляем его из строки для дальнейшего поиска имени
            if (hasCf)
            {
                // Собираем строку обратно без последнего элемента (числа)
                lower = string.Join(" ", parts.Take(parts.Length - 1));
            }

            // 2. Стандартный поиск времени
            var timeMap = new Dictionary<string, string> {
                { "прошлое", "Прошлое" },
                { "настоящее", "Настоящее" },
                { "будущее", "Будущее" },
                { "past", "Прошлое" },
                { "present", "Настоящее" },
                { "future", "Будущее" }
            };

            string foundTime = "";
            string cleanName = lower;

            foreach (var kvp in timeMap)
            {
                if (lower.EndsWith(kvp.Key))
                {
                    foundTime = kvp.Value;
                    // Отрезаем время от названия карты
                    cleanName = lower.Substring(0, lower.LastIndexOf(kvp.Key)).Trim();
                    break;
                }
            }

            // 3. Формируем команду, если нашли время и имя не пустое
            if (!string.IsNullOrEmpty(foundTime) && !string.IsNullOrEmpty(cleanName))
            {
                // ВАЖНО: Преобразуем число в строку с ТОЧКОЙ для CLIPS (Invariant)
                float cfString = confidence;//.ToString(System.Globalization.CultureInfo.InvariantCulture);

                // Используем raw-input-card, CLIPS сам разберется с ID карты через aliases
                clipsCommand = $"(assert (raw-input-card (user-text \"{cleanName}\") (time-modifier \"{foundTime}\") (cf {cfString:0.0})))";
                
                // Для отладки выведем в окно, что мы отправляем (раскомментируйте при необходимости)
                // outputBox.Text += "DEBUG SEND: " + clipsCommand + System.Environment.NewLine;
                
                return true;
            }

            return false;
        }
        
        

        private void resetBtn_Click(object sender, EventArgs e)
        {
            LoadTarotSystem();
        }
        
        private void openFile_Click(object sender, EventArgs e)
        {
            if (clipsOpenFileDialog.ShowDialog() == DialogResult.OK)
            {
                 codeBox.Text = System.IO.File.ReadAllText(clipsOpenFileDialog.FileName);
                 clips.LoadFromString(codeBox.Text);
                 clips.Reset();
                 Text = "Экспертная система – " + clipsOpenFileDialog.FileName;
            }
        }
        
        private void fontSelect_Click(object sender, EventArgs e)
        {
            if (fontDialog1.ShowDialog() == DialogResult.OK)
            {
                codeBox.Font = fontDialog1.Font;
                outputBox.Font = fontDialog1.Font;
            }
        }

        private void saveAsButton_Click(object sender, EventArgs e)
        {
            clipsSaveFileDialog.FileName = clipsOpenFileDialog.FileName;
            if (clipsSaveFileDialog.ShowDialog() == DialogResult.OK)
            {
                System.IO.File.WriteAllText(clipsSaveFileDialog.FileName, codeBox.Text);
            }
        }

        private void clearBtn_Click(object sender, EventArgs e)
        {
            codeBox.Clear();
            codeBox.Focus();
        }
    }
}
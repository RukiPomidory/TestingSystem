using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using Microsoft.Win32;
using System.Windows.Markup;

namespace TestingSystem
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        List<string> Tests { get; set; }
        List<string> Answers { get; set; }
        Programmer SelectedProgrammer { get; set; }
        Dictionary<string, Func<Programmer, Process>> CompilersDatabase { get; set; }
        Dictionary<string, CodeLanguage> Languages { get; set; }
        List<Comment> Comments { get; set; }
        ButtonControl FilesWindowButtons { get; set; }
        ButtonControl CodeViewButtons { get; set; }
        ButtonControl TestFilesButtons { get; set; }
        List<Programmer> Programmers { get; set; }
        CancellationTokenSource cancellation;
        int timeLimit = 1000;
        int TimeLimit
        {
            get
            {
                return timeLimit;
            }
            set
            {
                timeLimit = value > 600000 || value <= 0  ? -1 : value ;
            }
        }
        string SelectedLanguage
        {
            get
            {
                return ((ComboBoxItem)LanguageSelect.SelectedItem ??
                   throw new InvalidOperationException(
                       message: "Не выбран язык программирования")).Name;
            }
        }
        string SelectedCompilerPath
        {
            get
            {
                try
                {
                    return File.ReadAllText(SelectedLanguage);
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
            }
        }
        string SelectedTest
        {
            get
            {
                int index = TestList.SelectedIndex;
                if (index < 0) return null;
                return File.ReadAllText(Tests[index]);
            }
        }
        string SelectedAnswer
        {
            get
            {
                int index = AnswerList.SelectedIndex;
                if (index < 0) return null;
                return File.ReadAllText(Answers[index]);
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            Tests = new List<string>();
            Answers = new List<string>();
            CompilersDatabase = new Dictionary<string, Func<Programmer, Process>>();
            
            Languages = new Dictionary<string, CodeLanguage>
            {
                { ".cs", new CodeLanguage("CS", "C#", CreateCompilledProgram)},
                { ".java", new CodeLanguage("JA", "Java", NotSupported)},
                { ".cpp", new CodeLanguage("CP", "C++", NotSupported) },
                { ".py", new CodeLanguage("PY", "Python", CreateInterpretedProgram,
                (process, coder) => {
                    var input = process.StandardInput;
                    var output = process.StandardOutput;
                    input.WriteLine($"{File.ReadAllText(coder.language.ID)} {coder.programPath}");
                    output.ReadLine();
                    output.ReadLine();
                    output.ReadLine();
                    output.ReadLine();
                    })}
            };
            LanguageSelect.Items.Add(new ComboBoxItem
            {
                Name = "AUTO",
                Tag = "AUTO",
                Content = "Auto",
                Width = 128,
                Height = 20,
                Padding = new Thickness(4, 1, 4, 1)
            });
            LanguageSelect.SelectedIndex = 0;
            foreach (var id in Languages)
            {
                var lang = id.Value;
                lang.extension = id.Key;
                ComboBoxItem item = new ComboBoxItem
                {
                    Name = lang.ID,
                    Tag = id.Key,
                    Content = lang.Name,
                    Width = 128,
                    Height = 20,
                    Padding = new Thickness(4, 1, 4, 1)
                };
                LanguageSelect.Items.Add(item);
                CompilersDatabase.Add(lang.ID, lang.CreatingMethod);
            }
            LanguageSelect.SelectionChanged += LanguageSelect_SelectionChanged;
            FilesWindowButtons = new ButtonControl(
                new List<Func<bool>>
                {
                    () => {
                        if (!ProgramFilePath.IsEnabled) return false;
                        return ProgramFilePath.Text != SelectedProgrammer.programPath;
                    },
                    () => {
                        if (!CompilerFilePath.IsEnabled) return false;
                        return CompilerFilePath.Text != SelectedCompilerPath;
                    }
                }, FilesWindowSave, FilesWindowCancel);

            CodeViewButtons = new ButtonControl(
                new List<Func<bool>>
                {
                    () => {
                        if (SelectedProgrammer.IsExe)
                            return false;
                        return CodeTextView.Text != File
                            .ReadAllText(SelectedProgrammer.programPath);
                    }
                }, CodeViewSave, CodeViewCancel);

            TestFilesButtons = new ButtonControl(
                new List<Func<bool>>
                {
                    () => (SelectedAnswer ?? "")!= AnswersDemo.Text,
                    () => (SelectedTest ?? "")!= TestsDemo.Text
                }, TestsWindowCancel, TestsWindowSave);
            Comments = new List<Comment>
            {
                new Comment("программист", "программиста", "программистов"),
                new Comment("успех", "успеха", "успехов"),
                new Comment("выпитая чашка кофе", "выпитые чашки кофе", "выпитых чашек кофе"),
                new Comment("потраченный нерв", "потраченных нерва", "потраченных нервов"),
                new Comment("нажатая клавиша", "нажатые клавиши", "нажатых клавиш"),
                new Comment("довольный заказчик", "довольных заказчика", "довольных заказчиков")
            };
            Programmers = new List<Programmer>();
            TestingProgress.ValueChanged += TestingProgress_ValueChanged;
            TimeLimitSlider.ValueChanged += TimeLimitSlider_ValueChanged;
            TimeLimitDisplay.TextChanged += TimeLimitDisplay_TextChanged;
        }

        private void TestingProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Launch.IsEnabled = TestingProgress.Value == TestingProgress.Maximum;
            if (Launch.IsEnabled)
            {
                TestingProgress.Value = 0;
                Cancel.IsEnabled = false;
                Launch.IsEnabled = true;
                cancellation.Dispose();
            }
        }

        /// <summary>
        /// Проверяет совпадение вывода программы с правильным ответом
        /// </summary>
        /// <param name="stream">Поток, используемый для чтения данных программы</param>
        /// <param name="answerPath">Путь к файлу с правильным ответом</param>
        /// <param name="index">Текущий индекс</param>
        /// <param name="coder">Текущий программист</param>
        /// <returns>true - если ответ верный, иначе - false</returns>
        bool IsPassed(StreamReader stream, string answerPath, int index, Programmer coder)
        {
            bool result = true;
            FileData answerFileData = new FileData(answerPath);
            coder.UserAnswers.Add("");
            coder.CorrectAnswers.Add("");
            foreach (var answer in answerFileData)
            {
                string read = stream.ReadLine();
                coder.CorrectAnswers[index] += answer + '\n';
                coder.UserAnswers[index] += read + '\n';
                if (result) result = answer == read;
            }
            return result;
        }

        /// <summary>
        /// Асинхронно проверяет совпадение вывода программы с правильным ответом
        /// </summary>
        /// <param name="stream">Поток, используемый для чтения данных программы</param>
        /// <param name="answerPath">Путь к файлу с правильным ответом</param>
        /// <param name="index">Текущий индекс</param>
        /// <param name="coder">Текущий программист</param>
        /// <param name="cancel">Токен отмены</param>
        /// <returns>Асинхронная операция, возвращающая результат проверки</returns>
        Task<bool> IsPassedAsync(StreamReader stream, string answerPath, int index, Programmer coder,
            CancellationToken cancel)
        {
            Action check = cancellation.Token.ThrowIfCancellationRequested;
            check();
            var task = new Task<bool>(() => 
            {
                bool result = true;
                FileData answerFileData = new FileData(answerPath);
                coder.UserAnswers.Add("");
                coder.CorrectAnswers.Add("");
                foreach (var answer in answerFileData)
                {
                    check();
                    string read = stream.ReadLine();
                    coder.CorrectAnswers[index] += answer + '\n';
                    coder.UserAnswers[index] += read + '\n';
                    if (result) result = answer == read;
                }
                return result;
            }, cancel);
            task.Start();
            return task;
        }

        /// <summary>
        /// Добавляет элементы в лог 
        /// </summary>
        /// <param name="index">Текущий индекс</param>
        /// <param name="content">Контент элемента</param>
        void AddLogItem(int index, string content)
        {
            var item = new ListBoxItem
            {
                Name = $"testResult_{index}",
                Content = content,
                Height = 20,
                Margin = new Thickness(0)
            };
            Log.Items.Add(item);
        }

        /// <summary>
        /// Запуск программы вхолостую. Рекомендуется выполнить перед тестированием
        /// </summary>
        /// <param name="programmer">Текущий программист</param>
        /// <param name="program">Готовый к запуску процесс программы</param>
        void Idle(Programmer programmer, Process program)
        {
            program.Start();
            if (!programmer.IsExe) programmer.language.Preparation(program, programmer);
            var input = program.StandardInput;
            FileData test = new FileData(Tests[0]);
            foreach (var line in test)
                input.WriteLine(line);
            test.Close();
            Task<bool> isPassed = IsPassedAsync(program.StandardOutput, Answers[0], 0, programmer,
                new CancellationToken());
            isPassed.Wait();
            program.Close();
        }

        /// <summary>
        /// Проводит тестирование выбранного программиста на имеющихся в системе тестах
        /// </summary>
        /// <param name="programmer">Текущий программист</param>
        /// <param name="token">Токен отмены операции</param>
        async private void Run(Programmer programmer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                Process program;
                if (programmer.IsExe)
                    program = CreateProcess(programmer.programPath);
                else
                    program = await CreateProgramAsync(programmer);
                token.ThrowIfCancellationRequested();
                if (program == null)
                    throw new Exception(message: "Программа не компилируется");
                if (Tests.Count != Answers.Count)
                    throw new Exception(message: "Несовпадение количества тестов и ответов");
                int rightAnswerCount = 0;
                programmer.firstFail = -1;
                programmer.Log.Clear();
                programmer.time = 0;

                TestingProgress.Value += 3;
                Idle(programmer, program);
                TestingProgress.Value += 2;
                for (int i = 0; i < Tests.Count; i++)
                {
                    string testResult;
                    try
                    {
                        var sWatch = new Stopwatch();
                        program.Start();
                        if (!programmer.IsExe) programmer.language.Preparation(program, programmer);
                        var output = program.StandardOutput;
                        var input = program.StandardInput;
                        FileData test = new FileData(Tests[i]);
                        foreach (var line in test)
                            input.WriteLine(line);
                        test.Close();
                        var cancel = new CancellationTokenSource();
                        sWatch.Start();
                        var isPassedTask = IsPassedAsync(output, Answers[i], i, programmer, cancel.Token);
                        int completedId = TimeLimit > 0 ?
                            Task.WaitAny(Task.Delay(TimeLimit, new CancellationToken()), 
                            isPassedTask) : 1;
                        if (completedId == 1)
                        {
                            bool isPassed = await isPassedTask;
                            sWatch.Stop();
                            long time = sWatch.ElapsedMilliseconds;
                            if (isPassed)
                            {
                                testResult = $"PASSED \t{time}ms";
                                rightAnswerCount++;
                            }
                            else
                            {
                                if (programmer.firstFail == -1)
                                    programmer.firstFail = i;
                                testResult = $"FAILED \t{time}ms";
                            }
                            programmer.time += time;
                        }
                        else
                        {
                            cancel.Cancel();
                            isPassedTask.Dispose();
                            if (programmer.firstFail == -1)
                                programmer.firstFail = i;
                            testResult = $"FAILED \ttime limit exceeded";
                            programmer.time += TimeLimit;
                        }
                    }
                    catch (Exception exc)
                    {
                        testResult = "Ошибка: " + exc.Message;
                    }
                    finally
                    {
                        program.Close();
                    }
                    programmer.Log.Add($"Test {i + 1}: {testResult}");
                    
                    TestingProgress.Value++;
                }
                var random = new Random();
                int result = (int)Math.Round(100.0 * rightAnswerCount / Tests.Count);
                int simpleResult = result / 10;
                var comment = Comments[random.Next(Comments.Count)].GetAComment(simpleResult);
                programmer.ResultText = $"Пройдено {result}% тестов\n" 
                    + $"Диагноз: {simpleResult} {comment} из 10\n" +
                    $"Время: {programmer.time}ms";
                programmer.Result = result;
            }
            catch (FileNotFoundException)
            {
                programmer.ResultText = "Компилятор не найден";
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception exc)
            {
                programmer.ResultText = exc.Message;
            }
            ProgrammerSelect_SelectionChanged(null, null);
        }

        /// <summary>
        /// Запускает тестирование для каждого занесенного в систему программиста
        /// </summary>
        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            if (Programmers.Count == 0)
            {
                MessageBox.Show("Нет подопытных для экспериментов. Выберите программы",
                "Что тестировать?",
                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            if (Tests.Count > Answers.Count)
            {
                MessageBox.Show("Не хватает ответов",
                "Что-то не так с тестами",
                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            else if (Tests.Count != Answers.Count)
            {
                MessageBox.Show("Не хватает тестов",
                "Что-то не так с тестами",
                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            if (Tests.Count == 0)
            {
                MessageBox.Show("Добавьте тесты",
                "Что-то не так с тестами",
                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            Launch.IsEnabled = false;
            Cancel.IsEnabled = true;
            cancellation = new CancellationTokenSource();
            
            TestingProgress.Value = 0;
            int part = (Tests.Count + 5);
            TestingProgress.Maximum = Programmers.Count * part;
            if (Log.Items.Count != 0)
                Log.Items.Clear();

            foreach (var coder in Programmers)
            {
                coder.Result = 0;
                if (!coder.IsExe && (coder.language == null || coder.language.CreatingMethod == NotSupported))
                {
                    TestingProgress.Maximum -= part;
                    coder.ResultText = "Язык программирования остутствует или не поддерживается";
                    continue;
                }
                coder.UserAnswers.Clear();
                coder.CorrectAnswers.Clear(); 
                Run(coder, cancellation.Token);
            }
        }

        /// <summary>
        /// Вызывает остановку тестирования
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            cancellation.Cancel();
            TestingProgress_ValueChanged(null, null);
        }

        /// <summary>
        /// Создает программу на основе данных, полученных из экземпляра Programmer
        /// </summary>
        /// <param name="programmer">Программист, чью программу необходимо получить</param>
        /// <returns>Возвращает готовый к запуску процесс программы</returns>
        Process CreateProgram(Programmer programmer)
        {
            if (programmer.programPath == null)
                throw new ArgumentNullException();
            return programmer.language.CreatingMethod(programmer);
        }

        /// <summary>
        /// Асинхронно создает программу на основе данных, полученных из экземпляра Programmer
        /// </summary>
        /// <param name="programmer">Программист, чью программу необходимо получить</param>
        /// <returns>Асинхронная операция, возвращающая готовый к запуску процесс программы по окончании компиляции</returns>
        Task<Process> CreateProgramAsync(Programmer programmer)
        {
            var task = new Task<Process>(() => CreateProgram(programmer));
            task.Start();
            return task;
        }

        /// <summary>
        /// Метод компиляции программы для языков программирования, поддержка которых еще не реализована
        /// </summary>
        /// <param name="programmer">Текущий программист</param>
        /// <returns>Готовый к запуску процесс программы</returns>
        Process NotSupported(Programmer programmer)
        {
            throw new InvalidOperationException(message: "Выбранный язык не поддерживается");
        }
        
        /// <summary>
        /// Исполоьзуется для создания процессов программ, написанных на компилируемых языках программирования
        /// </summary>
        /// <param name="programmer">Программист, чья программа будет скомпилирована</param>
        /// <returns>Готовый к запуску процесс скомпилированной программы</returns>
        Process CreateCompilledProgram(Programmer programmer)
        {
            string compiler = File.ReadAllText(programmer.language.ID);
            string program = programmer.programPath;
            string direct = System.IO.Path.GetDirectoryName(program);

            Process cmd = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "C:\\Windows\\System32\\cmd.exe",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            cmd.Start();
            string file = System.IO.Path.GetFileNameWithoutExtension(program);
            var stream = cmd.StandardInput;
            stream.WriteLine($"cd /D {direct}");
            stream.WriteLine($"\"{compiler}\" {program}");
            stream.Close();
            cmd.WaitForExit();
            cmd.Close();
            
            return CreateProcess($"{direct}\\{file}.exe");
        }
        
        /// <summary>
        /// Используется для создания процессов программ, написанных на интерпретируемых языках программирования
        /// </summary>
        /// <param name="programmer">Программист, чья программа будет скомпилирована</param>
        /// <returns>Готовый к запуску процесс программы</returns>
        Process CreateInterpretedProgram(Programmer programmer)
        {
            return CreateProcess("C:\\Windows\\System32\\cmd.exe");
        }

        /// <summary>
        /// Используется для создания процессов программ, написанных на C++
        /// </summary>
        /// <param name="programmer">Программист, чья программа будет скомпилирована</param>
        /// <returns>Готовый к запуску процесс программы</returns>
        Process CreateCppProgram(Programmer programmer)
        {
            throw new NotImplementedException();
        } 
        
        /// <summary>
        /// Метод создания процесса программы
        /// </summary>
        /// <param name="path">Путь к файлу с текстом программы</param>
        /// <returns>Готовый к запуску процесс программы, если компиляция провалилась - null</returns>
        Process CreateProcess(string path)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = path ?? throw new Exception(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                Process process = new Process { StartInfo = info };
                process.Start();
                process.Kill();
                return process;
            }
            catch(InvalidOperationException)
            {
                return null;
            }
        }
        
        /// <summary>
        /// Открывает диалог выбора файлов и запускает для каждого метод обработки
        /// </summary>
        /// <param name="filter">Фильтры расширений файлов</param>
        /// <param name="act">Метод, обрабатывающий каждый из выбранных файлов. В качестве параметра принимает имя файла</param>
        void OpenFile(string filter, Action<string> act)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = filter,
                Multiselect = true
            };
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                foreach(var name in dialog.FileNames)
                    act(name);
            }
        }

        /// <summary>
        /// Создает файл с именем выбранного языка программирования, содержащий путь к компилятору
        /// </summary>
        /// <param name="path">Путь к компилятору выбранного языка программирования</param>
        void CreateCompilerFile(string path)
        {
            string lang = SelectedLanguage;
            File.WriteAllText(lang, path);
        }
        
        /// <summary>
        /// Открывает диалог выбора файлов программ
        /// </summary>
        private void ProgramSelect_Click(object sender, RoutedEventArgs e)
        {
            OpenFile("All files (*.*)|*.*|Text documents (.txt)|*.txt", (name) =>
            {
                var id = ((ComboBoxItem)LanguageSelect.SelectedItem).Tag.ToString();
                if (id == "AUTO")
                    id = System.IO.Path.GetExtension(name);
                Programmer coder;
                if (id != ".exe")
                {
                    try
                    {
                        coder = new Programmer(
                            Languages[id],
                            name,
                            System.IO.Path.GetFileName(name)
                            );
                    }
                    catch (KeyNotFoundException)
                    {
                        MessageBox.Show($"{System.IO.Path.GetFileName(name)}: " +
                            "не удалось автоматически определить язык программирования",
                            "Ошибка определения языка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        coder = new Programmer(name, System.IO.Path.GetFileName(name));
                    }
                }
                else
                    coder = new Programmer(name, System.IO.Path.GetFileName(name));

                Programmers.Add(coder);
                ProgrammerSelectAdd(coder);
                ProgrammerSelect.SelectedIndex = Programmers.Count - 1;
            });
        }

        /// <summary>
        /// Открывает диалог выбора файлов с тестами
        /// </summary>
        private void AddTest_Click(object sender, RoutedEventArgs e)
        {
            OpenFile("Text documents (.txt)|*.txt", (name) =>
            {
                Tests.Add(name);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(name);
                TestList.Items.Add(new ListBoxItem
                {
                    Height = 20,
                    Content = $"{Tests.Count}: {fileName}"
                });
                testCount.Text = $"{Tests.Count} : {Answers.Count}";
            });
        }

        /// <summary>
        /// Открывает диалог выбора файлов с ответами к тестам
        /// </summary>
        private void AddAnswer_Click(object sender, RoutedEventArgs e)
        {
            OpenFile("Text documents (.txt)|*.txt", (name) =>
            {
                Answers.Add(name);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(name);
                AnswerList.Items.Add(new ListBoxItem
                {
                    Height = 20,
                    Content = $"{Answers.Count}: {fileName}"
                });
                testCount.Text = $"{Tests.Count} : {Answers.Count}";
            });
        }

        /// <summary>
        /// Открывает диалог выбора файла компилятора
        /// </summary>
        private void CompilerSelect_Click(object sender, RoutedEventArgs e)
        {
            OpenFile("All files (*.*)|*.*", (name) =>
            {
                try
                {
                    CreateCompilerFile(name);
                    CompilerFilePath.Text = name;
                }
                catch (Exception exc)
                {
                    Result.Text = exc.Message;
                }
            });
        }
        
        private void TestList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = TestList.SelectedIndex;
            if (index < 0)
            {
                TestsDemo.Clear();
                TestsDemo.IsEnabled = false;
                return;
            }
            AnswerList.SelectedIndex = index >= Answers.Count ? -1 : index;
            TestsDemo.Text = File.ReadAllText(Tests[index]);
            TestsWindowDelete.IsEnabled = true;
            TestsDemo.IsEnabled = true;
        }

        private void AnswerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = AnswerList.SelectedIndex;
            if (index < 0)
            {
                AnswersDemo.Clear();
                AnswersDemo.IsEnabled = false;
                return;
            }
            TestList.SelectedIndex = index >= Tests.Count ? -1 : index;
            AnswersDemo.Text = File.ReadAllText(Answers[index]);
            TestsWindowDelete.IsEnabled = true;
            AnswersDemo.IsEnabled = true;
        }

        private void LanguageSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (SelectedLanguage == "AUTO" || 
                    CompilersDatabase[SelectedLanguage] == NotSupported)
                {
                    CompilerFilePath.Clear();
                    CompilerFilePath.IsEnabled = false;
                    CompilerSelect.IsEnabled = false;
                    FilesWindowButtons.Check();
                    return;
                }
                CompilerSelect.IsEnabled = true;
                CompilerFilePath.IsEnabled = true;
                CompilerFilePath.Text = File.ReadAllText(SelectedLanguage);
            }
            catch (FileNotFoundException)
            {
                CompilerFilePath.Text = "Компилятор отсутствует";
            }
        }
        
        private void FilesWindowSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ProgramFilePath.IsEnabled)
                    SelectedProgrammer.programPath = ProgramFilePath.Text;
                if (CompilerFilePath.IsEnabled)
                    File.WriteAllText(SelectedLanguage, CompilerFilePath.Text);
            }
            catch (InvalidOperationException exc)
            {
                Result.Text = exc.Message;
            }
            FilesWindowButtons.Check();
        }

        private void Log_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Log.SelectedIndex == -1)
            {
                userAnswerLog.Text = null;
                correctAnswerLog.Text = null;
                return;
            }
            var coder = SelectedProgrammer;
            userAnswerLog.Text = coder.UserAnswers[Log.SelectedIndex];
            correctAnswerLog.Text = coder.CorrectAnswers[Log.SelectedIndex];
        }

        private void FilesWindowCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProgramFilePath.Text = SelectedProgrammer.programPath;
                CompilerFilePath.Text = File.ReadAllText(SelectedLanguage);
            }
            catch (FileNotFoundException) { }
            catch (InvalidOperationException exc)
            {
                Result.Text = exc.Message;
            }
            FilesWindowButtons.Check();
        }

        private void TestsWindowSave_Click(object sender, RoutedEventArgs e)
        {
            if (TestList.SelectedIndex != -1)
            {
                var test = Tests[TestList.SelectedIndex];
                if (test != null)
                    File.WriteAllText(test, TestsDemo.Text);
            }
            if (AnswerList.SelectedIndex != -1)
            {
                var answer = Answers[AnswerList.SelectedIndex];
                if (answer != null)
                    File.WriteAllText(answer, AnswersDemo.Text);
            }
            TestFilesButtons.Check();
        }

        private void TestsWindowCancel_Click(object sender, RoutedEventArgs e)
        {
            TestsDemo.Text = SelectedTest;
            AnswersDemo.Text = SelectedAnswer;
            TestFilesButtons.Check();
        }

        private void TestsWindowDelete_Click(object sender, RoutedEventArgs e)
        {
            TestsWindowDelete.IsEnabled = false;
            TestListRemove(TestList.SelectedIndex);
            AnswerListRemove(AnswerList.SelectedIndex);
            testCount.Text = $"{Tests.Count} : {Answers.Count}";
        }

        /// <summary>
        /// Удаляет элемент списка тестов на указанном индексе
        /// </summary>
        /// <param name="index">Индекс элемента, который должен быть удален</param>
        void TestListRemove(int index)
        {
            if (index >= Tests.Count || index < 0) return;
            Tests.RemoveAt(index);
            TestList.Items.Clear();
            for (int i = 0; i < Tests.Count; i++)
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(Tests[i]);
                TestList.Items.Add(new ListBoxItem
                {
                    Height = 20,
                    Content = $"{i + 1}: {fileName}"
                });
            }
            TestList.SelectedIndex = index;
        }

        /// <summary>
        /// Удаляет элемент списка ответов на указанном индексе
        /// </summary>
        /// <param name="index">Индекс элемента, который должен быть удален</param>
        void AnswerListRemove(int index)
        {
            if (index >= Answers.Count || index < 0) return;
            Answers.RemoveAt(index);
            AnswerList.Items.Clear();
            for (int i = 0; i < Answers.Count; i++)
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(Answers[i]);
                AnswerList.Items.Add(new ListBoxItem
                {
                    Height = 20,
                    Content = $"{i + 1}: {fileName}"
                });
            }
            AnswerList.SelectedIndex = index;
        }

        private void AnswersDemo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (AnswerList.SelectedIndex < 0)
            {
                TestsDemo.Clear();
                AnswersDemo.IsEnabled = false;
                return;
            }
            TestFilesButtons.Check();
        }

        private void TestsDemo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TestList.SelectedIndex < 0)
            {
                TestsDemo.Clear();
                TestsDemo.IsEnabled = false;
                return;
            }
            TestFilesButtons.Check();
        }

        private void CodeViewCancel_Click(object sender, RoutedEventArgs e)
        {
            CodeTextView.Text = File.ReadAllText(SelectedProgrammer.programPath);
            CodeViewButtons.Check();
        }

        private void CodeViewSave_Click(object sender, RoutedEventArgs e)
        {
            File.WriteAllText(SelectedProgrammer.programPath, CodeTextView.Text);
            CodeViewButtons.Check();
        }

        private void CodeTextView_TextChanged(object sender, TextChangedEventArgs e)
        {
            CodeViewButtons.Check();
        }

        private void CompilerFilePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilesWindowButtons.Check();
        }

        private void ProgramFilePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilesWindowButtons.Check();
        }

        private void ProgrammerSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var id = ProgrammerSelect.SelectedIndex;
            if (id < 0 || id >= Programmers.Count)
                return;
            var coder = Programmers[id];
            SelectedProgrammer = coder;
            
            Result.Text = coder.ResultText;
            Log.Items.Clear();
            for (int i = 0; i < coder.Log.Count; i++)
            {
                AddLogItem(i + 1, coder.Log[i]);
            }
            ProgramFilePath.Text = coder.programPath;
            ProgramFilePath.IsEnabled = true;
            if (!coder.IsExe)
            {
                CodeTextView.Text = File.ReadAllText(coder.programPath);
                CodeTextView.IsEnabled = true;
            }
            else
            {
                CodeTextView.Clear();
                CodeTextView.IsEnabled = false;
            }
        }

        /// <summary>
        /// Создает элемент списка программистов
        /// </summary>
        /// <param name="programmer">Программист, которому будет принадлежать созданный элемент</param>
        void ProgrammerSelectAdd(Programmer programmer)
        {
            var grid = new Grid() as IAddChild;

            Button closeButton;
            grid.AddChild(closeButton = new Button
            {
                Name = "CloseButton",
                Content = "X",
                Width = 18,
                Padding = new Thickness(0, -1, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0x16, 0x0A)),
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0)
            });
            grid.AddChild(new TextBlock
            {
                Name = $"coder{programmer.ID}",
                Text = $"{programmer.ID}: {programmer.Name}",
                Margin = new Thickness(0, 0, 124, 0)
            });
            var languages = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 70,
                Margin = new Thickness(0, 0, 18, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0)
            };
            foreach (var id in Languages)
            {
                var lang = id.Value;
                ComboBoxItem item = new ComboBoxItem
                {
                    Name = lang.ID,
                    Tag = id.Key,
                    Content = lang.Name,
                    Width = 70,
                    Height = 18,
                    Padding = new Thickness(4, -4, 4, 0)
                };
                languages.Items.Add(item);
            }
            if (programmer.IsExe)
            {
                languages.SelectedIndex = -1;
                languages.IsEnabled = false;
            }
            else if (programmer.language == null)
                languages.SelectedIndex = -1;
            else
            {
                languages.SelectedItem = languages.Items
                    .Cast<ComboBoxItem>()
                    .Where((item) => item.Name == programmer.language.ID)
                    .First();
            }
            languages.SelectionChanged += (s, e) => programmer.language = Languages
                .ElementAt(languages.SelectedIndex).Value;
            grid.AddChild(languages);
            TextBox resultText;
            grid.AddChild(resultText = new TextBox
            {
                Text = "0%",
                Width = 34,
                Margin = new Thickness(0, 0, 90, 0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(-2, 0, 0, 0),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x72, 0x72, 0x72)),
                Foreground = new SolidColorBrush(Colors.Black),
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                IsUndoEnabled = false,
                IsReadOnly = true,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xDA, 0xD3, 0xD3)),
                MaxLines = 1,
                BorderThickness = new Thickness(1),
                IsEnabled = false
            });
            programmer.ResultValueChanged += () => resultText.Text = $"{programmer.Result}%";
            ListBoxItem listBoxItem;
            ProgrammerSelect.Items.Add(listBoxItem = new ListBoxItem
            {
                Height = 20,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Width = ProgrammerSelect.Width - 22,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Content = grid
            });
            closeButton.Click += (s, args) =>
            {
                Programmers.Remove(programmer);
                ProgrammerSelect.Items.Remove(listBoxItem);
            };
        }

        private void TimeLimitDisplay_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (TimeLimitDisplay.Text == "" || TimeLimitDisplay.Text == "-" ||
                    Int32.Parse(TimeLimitDisplay.Text) <= 0)
                {
                    TimeLimit = -1;
                    return;
                }
                TimeLimit = Int32.Parse(TimeLimitDisplay.Text);
            }
            catch (Exception)
            {
                if (TimeLimitDisplay.Text == "∞")
                {
                    TimeLimit = -1;
                }
                else
                    TimeLimitDisplay.Text = TimeLimit.ToString();
            }

            if (TimeLimit <= 0)
            {
                TimeLimitDisplay.Text = "∞";
                TimeLimitSlider.Value = TimeLimitSlider.Maximum;
            }
            else
                TimeLimitSlider.Value = TimeLimit / 20;
        }

        private void TimeLimitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TimeLimitSlider.IsMouseCaptureWithin)
                TimeLimitDisplay.Text = ((int)TimeLimitSlider.Value * 20).ToString();
        }
    }
}
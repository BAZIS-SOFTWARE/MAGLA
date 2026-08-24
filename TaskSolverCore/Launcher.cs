using ClientLogic;
using System.Net;
using System.Reflection;
using Project.Interfaces.Tasks;
using Project.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Project.TaskParameters;
using Project.IO;

namespace TaskSolverCore
{
    public static class Launcher
    {
        private static IProjectData project;
        private static Task serverConnectionTask;
        public static event Action<string> GeneralMessageEvent;

        private static CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
        public static ClientController ServerConnection { get; private set; }

        static JsonSerializerSettings settingsSerializer = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Newtonsoft.Json.Formatting.Indented
        };

        public static void StartSolver(string[] fileTCF)
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine($"BazisSoftware Ltd.");
            var verStr = $"v{ver.Major}.{ver.Minor}.{ver.Build}";
            Console.WriteLine($"BazisCAE Solver, {verStr}");
            Console.WriteLine();
            Console.WriteLine(@"|-|-------\--|");
            Console.WriteLine(@"|-| |----\ \-|");
            Console.WriteLine(@"|-| |    / /-|");
            Console.WriteLine(@"|-| |---/ /--|");
            Console.WriteLine(@"|-| |---\ \--|");
            Console.WriteLine(@"|-| |    \ \-|");
            Console.WriteLine(@"|-| |----/ /-|");
            Console.WriteLine(@"|-|-------/--|");
            Console.WriteLine();

            GetServerConnection();


            //MaterialDBData matDataSet = null;
            //FunctionDBData funDataSet = null;

            var lines = File.ReadAllLines(fileTCF[0]);
            foreach (var line in lines)
            {

                if (line.StartsWith("\\"))
                    continue;

                if (line.StartsWith("загрузить"))
                {
                    LoadData(line);
                    continue;
                }

                if (line.StartsWith("расчет"))
                {
                    ComputeTask(line);
                }
            }
        }

        private static void ComputeTask(string line)
        {
            var subStrs = line.Split();

            if (subStrs[3] == "пропустить")
                return;

            var activeModule = ConvertTaskKindToRequest(subStrs[1]);
            try
            {
                if (project.TaskData is null)
                    throw new Exception("начало расчета не может быть раньше загрузки файлов проекта");

                ServerConnection.RequestServer(activeModule + " Взять");
                if (CheckServerAnswer(ServerConnection.Answer))
                    StartLicensing(activeModule);     
                else throw new LicException($@"Ошибка лицензирования модуля {activeModule}!");

                var startTime = DateTime.Now;

                var index = int.Parse(Path.GetFileNameWithoutExtension(subStrs[2]).Split('_')[1]);
         
                var folder = string.Join("\\",subStrs[2].Split("\\").SkipLast(2));

                var compLine = $"Расчет : {subStrs[1]} задача {index} ";
                GeneralMessageEvent?.Invoke(compLine);
                Console.WriteLine(compLine);
                GeneralTask taskCalc;

                if (activeModule == "ThermalSolver")
                {
                    var thermalJson = File.ReadAllText(subStrs[2]);
                    var thermalParameters = JsonConvert.DeserializeObject<TermalParameters>(thermalJson, settingsSerializer) ?? throw new JsonSerializationException("Не удалось прочитать параметры тепловой задачи.");
                    var thermalData = JObject.Parse(thermalJson);
                    var transportOptions = thermalData["HeatTransport"]?.ToObject<HeatTransportOptions>(JsonSerializer.Create(settingsSerializer)) ?? new HeatTransportOptions();
                    var convection = thermalData.Value<bool>("Convection");
                    var convectionLoad = thermalData["ConvectionlLoad"]?.ToObject<double[]>();
                    if (convectionLoad is { Length: > 0 })
                        transportOptions.Convection.Velocity = convectionLoad;
                    if (project.TaskData.TaskType == TaskType.Volume |
                        project.TaskData.TaskType == TaskType.Volume_mixed)
                        taskCalc = new HeatTask3D(index, folder, project.TaskData, thermalParameters, transportOptions, convection);
                    else if (project.TaskData.TaskType == TaskType.Plain)
                        taskCalc = new HeatTask2DPlane(index, folder, project.TaskData, thermalParameters, transportOptions, convection);
                    else
                        taskCalc = new HeatTask2DAxi(index, folder, project.TaskData, thermalParameters, transportOptions, convection);
                }
                else
                {
                    if (project.TaskData.TaskType == TaskType.Volume | 
                        project.TaskData.TaskType == TaskType.Volume_mixed)
                        taskCalc = new MechTask3D(index, folder, project.TaskData,
                            JsonConvert.DeserializeObject<MechanicalParameters>
                    (File.ReadAllText(subStrs[2]), settingsSerializer)){};
                    else
                        taskCalc = new MechTask2DAxi(index, folder, project.TaskData,
                            JsonConvert.DeserializeObject<MechanicalParameters>
                    (File.ReadAllText(subStrs[2]), settingsSerializer));

                }
                taskCalc.Folder = folder;
                taskCalc.TaskInfoEvent += (ar) => { Console.WriteLine(ar); };
                taskCalc.Calc();


                StopLicensing();
                DisconnectWithServer(activeModule); //отсоединяемся от сервера лицензий                   

                var finishLine = $"Расчет завершен в {DateTime.Now}. Затраченное время {DateTime.Now - startTime}.";
                GeneralMessageEvent?.Invoke(finishLine);
                Console.WriteLine(finishLine);
                Thread.Sleep(5000); //ждем пока сервер обновит состояние лицензии
            }
            catch (Exception ex)
            {
                GeneralMessageEvent?.Invoke($"Ошибка : {ex.Message}, метод : {ex.TargetSite} , стек : {ex.StackTrace}");
                Console.WriteLine(ex.Message);

                var licException = ex as LicException;
                
                if(licException == null)
                {
                    StopLicensing();
                    DisconnectWithServer(activeModule); //отсоединяемся от сервера лицензий  
                }

                Console.ReadLine();
            }
            
        }

        private static void LoadData(string line)
        {
            try
            {
                var subStrs = line.Split();
                var fileType = subStrs[1];
                var filePath = subStrs[2];

                switch (fileType)
                {
                    //case "материалы":
                    //    matDataSet = JsonConvert.DeserializeObject<MaterialDBData>
                    //(File.ReadAllText(filePath), settingsSerializer);
                    //    if (matDataSet is null)
                    //        throw new Exception("Переданный файл материалов не существует или ничего не содержит");
                    //    break;

                    //case "функции":
                    //    funDataSet = JsonConvert.DeserializeObject<FunctionDBData>
                    //(File.ReadAllText(filePath), settingsSerializer);
                    //    if (funDataSet is null)
                    //        throw new Exception("Переданный файл функций не существует или ничего не содержит");
                    //    break;

                    case "проект":
                        var ext = Path.GetExtension(filePath);
                        var loader = new LoadProjectFromTextFormat();
                        loader.LoadEvent += (ar1, ar2) => { Console.WriteLine(ar2.Message); };

                        Console.WriteLine($"Загрузка проекта {filePath}");

                        project = loader.Load(filePath);

                        break;
                    default:
                        throw new Exception($"тип данных {fileType} не ожидаем в работе программы");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Source} {ex.Message}" );
                Console.ReadLine();
            }
        }

        private static bool CheckServerAnswer(string answer)
        {
            var answArr = answer.Split(' ');
            if (answArr[0] == "можно" && CheckNumberOfElements(int.Parse(answArr[1])))
                return true;
            else return false;
        }

        private static void GetServerConnection()
        {
            var net = Environment.GetEnvironmentVariable("BazisServerPath", EnvironmentVariableTarget.Machine);

            if (net != null)
            {
                var iPAddress = IPAddress.Parse(net.Split(':')[0]);
                var port = int.Parse(net.Split(':')[1]);
                Console.WriteLine($"Адресс подключения: {iPAddress}, порт: {port}");
                ServerConnection = new ClientController(iPAddress, port);
            }
            else
            {
                Console.WriteLine(@"Внимание! Не найдена переменная среды ""BazisServerPath = ip:port""");
                Console.WriteLine($"Адресс подключения: {IPAddress.Loopback}, порт: {8001}");
                ServerConnection = new ClientController(IPAddress.Loopback, 8001);
            }
        }

        private static void StartLicensing(string activeModule)
        {
            //var accidentExitDelegate = new Action(AccidentExit);
            cancelTokenSource = new CancellationTokenSource();

            serverConnectionTask = new Task(() =>
            {
                try
                {
                    while (true)
                    {
                        cancelTokenSource.Token.ThrowIfCancellationRequested();
                        lock (ServerConnection)
                        {
                            ServerConnection.RequestServer(activeModule + " Работа");
                            if (ServerConnection.Answer != "Работай")
                            {
                                throw new AccidentServerDisconnectionException();
                            }
                        }
                        Thread.Sleep(3000);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is AccidentServerDisconnectionException)
                    {
                        Console.WriteLine($@"Внимание! Лицензирование прервано со стороны сервера. Проверьте сервер лицензий. n\ 
Источник : {ex.Source}");
                    }
                }
            }, cancelTokenSource.Token);
            serverConnectionTask.Start();
        }

        public static void StopLicensing()
        {
            // отменяем выполнение задачи
            if(!cancelTokenSource.IsCancellationRequested)
            {
                cancelTokenSource.Cancel();
                Thread.Sleep(1000);
                while (true)
                {
                    if (serverConnectionTask.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                    {
                        cancelTokenSource.Dispose();
                        return;
                    }

                }
            }
        }

        public static void DisconnectWithServer(string activeModule)
        {
            ServerConnection.RequestServer(activeModule + " Отдать");
        }

        private static bool CheckNumberOfElements(int v)
        {
            if (v > project.ModelData.ObjectData.GetAllElements().Count())
            {
                return true;
            }
            else
            {
                Console.WriteLine("Превышено колличество доступных элементов");
                return false;
            }
        }
        
        private static string ConvertTaskKindToRequest(string taskKind)
        {
            switch (taskKind)
            {
                case "термическая":
                    return "ThermalSolver";
                case "механическая":
                    return "MechanicalSolver";
                case "химическая":
                    return "ChemicalSolver";
                default:
                    return "HardnessSolver";
            }
        }
    }
}

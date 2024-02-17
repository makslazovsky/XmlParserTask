using Newtonsoft.Json;
using RabbitMQ.Client;
using SharedLibrary.Models;
using SharedLibrary.Utilities;
using System.Text;
using System.Xml;

namespace FileParserService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConnection _rabbitMQConnection;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;

            // Инициализация подключения к RabbitMQ
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var factory = RabbitMQConnectionFactory.CreateConnectionFactory(configuration);
            _rabbitMQConnection = factory.CreateConnection();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Чтение и обработка XML файлов
                    ProcessXmlFiles();

                    // Ожидание 1 секунды перед следующей итерацией
                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"An error occurred: {ex.Message}");
                }
            }
        }

        private void ProcessXmlFiles()
        {
            string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "XmlFiles");

            // Создайте папку, если её нет
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            foreach (var filePath in Directory.GetFiles(directoryPath, "*.xml"))
            {
                try
                {
                    // Чтение XML файла
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(filePath);

                    // Парсинг XML данных и изменение ModuleState
                    // (здесь предполагается, что структура XML известна)
                    var modules = ParseXml(xmlDoc);

                    // Формирование JSON
                    var jsonResult = JsonConvert.SerializeObject(modules);

                    // Отправка JSON в DataProcessor Service через RabbitMQ
                    SendMessageToDataProcessor(jsonResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"An error occurred while processing {filePath}: {ex.Message}");
                }
            }
        }

        private List<Module> ParseXml(XmlDocument xmlDoc)
        {
            // Логика парсинга XML и создания объектов Module
            var modules = new List<Module>();
            foreach (XmlNode node in xmlDoc.SelectNodes("//Module"))
            {
                var module = new Module
                {
                    ModuleId = int.Parse(node.SelectSingleNode("ModuleId").InnerText),
                    ModuleState = GetRandomModuleState()
                };
                modules.Add(module);
            }

            return modules;
        }

        private string GetRandomModuleState()
        {
            // Метод для получения случайного значения ModuleState
            var states = new[] { "Online", "Run", "NotReady", "Offline" };
            var random = new Random();
            return states[random.Next(states.Length)];
        }

        private void SendMessageToDataProcessor(string jsonResult)
        {
            // Отправка сообщения в DataProcessor через RabbitMQ
            using (var channel = _rabbitMQConnection.CreateModel())
            {
                channel.QueueDeclare(queue: "DataProcessorQueue",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                var body = Encoding.UTF8.GetBytes(jsonResult);

                channel.BasicPublish(exchange: "",
                                     routingKey: "DataProcessorQueue",
                                     basicProperties: null,
                                     body: body);

                _logger.LogInformation($"Sent message to DataProcessor: {jsonResult}");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _rabbitMQConnection.Close();
            await base.StopAsync(cancellationToken);
        }
    }
}
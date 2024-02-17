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

            // ������������� ����������� � RabbitMQ
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
                    // ������ � ��������� XML ������
                    ProcessXmlFiles();

                    // �������� 1 ������� ����� ��������� ���������
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

            // �������� �����, ���� � ���
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            foreach (var filePath in Directory.GetFiles(directoryPath, "*.xml"))
            {
                try
                {
                    // ������ XML �����
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(filePath);

                    // ������� XML ������ � ��������� ModuleState
                    // (����� ��������������, ��� ��������� XML ��������)
                    var modules = ParseXml(xmlDoc);

                    // ������������ JSON
                    var jsonResult = JsonConvert.SerializeObject(modules);

                    // �������� JSON � DataProcessor Service ����� RabbitMQ
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
            // ������ �������� XML � �������� �������� Module
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
            // ����� ��� ��������� ���������� �������� ModuleState
            var states = new[] { "Online", "Run", "NotReady", "Offline" };
            var random = new Random();
            return states[random.Next(states.Length)];
        }

        private void SendMessageToDataProcessor(string jsonResult)
        {
            // �������� ��������� � DataProcessor ����� RabbitMQ
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
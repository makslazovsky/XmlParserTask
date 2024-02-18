using Newtonsoft.Json;
using RabbitMQ.Client;
using Serilog;
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

            // Configuring connection to RabbitMQ
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var factory = RabbitMQConnectionFactory.CreateConnectionFactory(configuration);
            _rabbitMQConnection = factory.CreateConnection();

            Log.Logger = new LoggerConfiguration()
           .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
           .CreateLogger();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Processing XML files
                    ProcessXmlFiles();

                    // Waiting for 1 second before checking for new files
                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred");
                    _logger.LogError(ex, "An error occurred");
                }

            }
        }

        private void ProcessXmlFiles()
        {
            string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "XmlFiles");

            // Creating directory if it doesn't exist
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            foreach (var filePath in Directory.GetFiles(directoryPath, "*.xml"))
            {
                try
                {
                    // Parsing XML file
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(filePath);

                    // Extracting XML data and generating ModuleState
                    var modules = ParseXml(xmlDoc);

                    // Converting to JSON
                    var jsonResult = JsonConvert.SerializeObject(modules);

                    // Sending JSON to DataProcessor Service via RabbitMQ
                    SendMessageToDataProcessor(jsonResult);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred while processing {FilePath}", filePath);
                    _logger.LogError(ex, "An error occurred while processing {FilePath}", filePath);
                }
            }
        }

        private List<Module> ParseXml(XmlDocument xmlDoc)
        {
            // Parsing XML and creating Module objects
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
            // Generating random ModuleState
            var states = new[] { "Online", "Run", "NotReady", "Offline" };
            var random = new Random();
            return states[random.Next(states.Length)];
        }

        private void SendMessageToDataProcessor(string jsonResult)
        {
            // Sending message to DataProcessor via RabbitMQ
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
                Log.Information($"Sent message to DataProcessor: {jsonResult}");
                _logger.LogInformation($"Sent message to DataProcessor: {jsonResult}");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _rabbitMQConnection.Close();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while closing connection");
            }
            await base.StopAsync(cancellationToken);
        }
    }
}
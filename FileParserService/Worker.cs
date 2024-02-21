using Newtonsoft.Json;
using RabbitMQ.Client;
using Serilog;
using SharedLibrary.Models;
using SharedLibrary.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
                    // Get the list of XML files
                    string[] xmlFiles = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "XmlFiles"), "*.xml");

                    // Create and start tasks for processing each file
                    var tasks = new List<Task>();
                    foreach (var filePath in xmlFiles)
                    {
                        tasks.Add(Task.Run(() => ProcessXmlFile(filePath, stoppingToken)));
                    }

                    // Wait for all tasks to complete
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred");
                    _logger.LogError(ex, "An error occurred");
                }

                // Pause before checking for new files again
                await Task.Delay(1000, stoppingToken);
            }
        }

        private void ProcessXmlFile(string filePath, CancellationToken stoppingToken)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning($"File {filePath} not found.");
                return;
            }
            try
            {
                // Load the XML file
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filePath);

                // Extract data from XML and generate ModuleState
                var modules = ParseXml(xmlDoc);

                // Convert to JSON
                var jsonResult = JsonConvert.SerializeObject(modules);

                // Send JSON to DataProcessor Service via RabbitMQ
                SendMessageToDataProcessor(jsonResult);
            }
            catch (OperationCanceledException)
            {
                // The operation was cancelled, ignore
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while processing {FilePath}", filePath);
                _logger.LogError(ex, "An error occurred while processing {FilePath}", filePath);
            }
        }

        private List<Module> ParseXml(XmlDocument xmlDoc)
        {
            // Parse XML and create Module objects
            var modules = new List<Module>();
            foreach (XmlNode node in xmlDoc.SelectNodes("//DeviceStatus"))
            {
                XmlNode moduleCategoryIDNode = node.SelectSingleNode("ModuleCategoryID");

                    var module = new Module
                    {
                        ModuleCategoryID = moduleCategoryIDNode.InnerText,
                        ModuleState = GetRandomModuleState()
                    };
                    modules.Add(module);
            }

            return modules;
        }

        private string GetRandomModuleState()
        {
            // Generate random ModuleState
            var states = new[] { "Online", "Run", "NotReady", "Offline" };
            var random = new Random();
            return states[random.Next(states.Length)];
        }

        private void SendMessageToDataProcessor(string jsonResult)
        {
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
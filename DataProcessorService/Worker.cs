using System.Text;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using SharedLibrary.Models;
using SharedLibrary.Utilities;

namespace DataProcessorService
{
    public class Worker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConnection _rabbitMQConnection;

        public Worker(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;

            // Инициализация подключения к RabbitMQ
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
            using (var channel = _rabbitMQConnection.CreateModel())
            {
                channel.QueueDeclare(queue: "DataProcessorQueue",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var jsonMessage = Encoding.UTF8.GetString(body);
                        var module = JsonConvert.DeserializeObject<List<Module>>(jsonMessage);

                        // Обработка сообщения
                        await ProcessMessage(module);

                        // Подтверждение обработки сообщения
                        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Ошибка при обработке сообщения");
                    }
                };

                channel.BasicConsume(queue: "DataProcessorQueue",
                                     autoAck: false,
                                     consumer: consumer);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private async Task ProcessMessage(List<Module> modules)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                try
                {
                    // Проверка существования ModuleCategoryID в базе данных
                    foreach (var module in modules)
                    {
                        var existingModule = await dbContext.Modules.FirstOrDefaultAsync(m => m.ModuleId == module.ModuleId);

                        if (existingModule != null)
                        {
                            existingModule.ModuleState = module.ModuleState;
                        }
                        else
                        {
                            dbContext.Modules.Add(module);
                        }
                    }

                    await dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка при обработке сообщения");
                }
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
                Log.Error(ex, "Ошибка при остановке сервиса");
            }
            await base.StopAsync(cancellationToken);
        }
    }
}
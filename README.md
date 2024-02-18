Требования
Для развертывания системы на хосте и проведения проверки требуется выполнить следующие шаги:

Установите RabbitMQ, Erlang, SQLite и .NET SDK.
Запустите и убедитесь в работоспособности RabbitMq.
Скачайте решение по ссылке: https://github.com/makslazovsky/XmlParserTask.
Отредактируйте файлы appsettings.json в проектах DataProcessorService и FileParserService, чтобы указать настройки для подключения к RabbitMQ и путь к базе данных SQLite.
Скомпилируйте проекты DataProcessorService и FileParserService.
Перенесите файлы XML из проекта FileParserService в папку XmlFiles в скомпилированный проект FileParserService.
Скопируйте файл базы данных DataProcessorService.db из проекта DataProcessorService в скомпилированный проект DataProcessorService.
Запустите FileParserService.exe, чтобы начать отправку и парсинг XML-файлов через RabbitMQ.
После этого запустите DataProcessorService.exe, чтобы принимать сообщения через RabbitMQ и сохранять их в базе данных SQLite.

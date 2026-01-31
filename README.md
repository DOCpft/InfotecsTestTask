# InfotecsTestTask

Веб-приложение для обработки CSV файлов с метриками, построенное на ASP.NET Core с использованием TimescaleDB для хранения временных рядов данных.

## Содержание
- [Функциональность](#функциональность)
- [Технологический стек](#технологический-стек)
- [Архитектура проекта](#архитектура-проекта)
- [Структура проекта](#структура-проекта)
- [Использование API](#использование-api)

## Функциональность

### Основные возможности:
    - Загрузка и обработка CSV файлов формата: `Date;ExecutionTime;Value`
    - Валидация данных и обработка ошибок
    - Хранение временных рядов в TimescaleDB (гипертаблицы)
    - Агрегация данных и вычисление статистики
    - Гибкая фильтрация результатов через API
    - Полная документация API через Swagger

### Поддерживаемый формат данных:
  csv
Date;ExecutionTime;Value
2024-01-15T10:30:00;1500;123.45
2024-01-15T11:00:00;1200;678.90

## Технологический стек
    - ASP.NET Core 9.0 - веб-фреймворк

    - Entity Framework Core 9.0 - ORM с поддержкой TimescaleDB

    - TimescaleDB (PostgreSQL 15+) - база данных для временных рядов

    - Npgsql - драйвер PostgreSQL для .NET

    - Swashbuckle.AspNetCore - генерация OpenAPI документации

## Архитектура проекта
    Presentation Layer (Controllers)
            ↓
     Business Logic Layer (Services)
            ↓
      Data Access Layer (DataAccess)
            ↓
         Database (TimescaleDB)

## Структура проекта
    InfotecsTestTask/
    │
    ├── Abstract/                          # Интерфейсы
    │   ├── IFileProcessingConfiguration.cs
    │   ├── IFileProcessingService.cs
    │   └── IFileProcessorFactory.cs
    │
    ├── Controllers/                       # API контроллеры
    │   └── FilesController.cs            # Основной контроллер для работы с файлами
    │
    ├── DataAccess/                        # Доступ к данным
    │   └── AppDbContext.cs               # Контекст Entity Framework
    │
    ├── DTOs/                             # Data Transfer Objects
    │   ├── FileProcessingResult.cs       # Результат обработки файла
    │   ├── ProcessedData.cs              # Обработанные данные
    │   └── ResultFilterDto.cs            # DTO для фильтрации результатов
    │
    ├── Entities/                          # Сущности базы данных
    │   ├── Result.cs                     # Агрегированные результаты
    │   └── Values.cs                     # Исходные значения (гипертаблица)
    │
    ├── Exceptions/                        # Кастомные исключения
    │   └── CsvValidationException.cs     # Исключение валидации CSV
    │
    ├── Migrations/                        # Миграции Entity Framework
    │
    ├── Services/                          # Бизнес-логика
    │   ├── ConcreteServices/
    │   │   └── CsvProcessingService.cs   # Реализация обработки CSV
    │   │
    │   ├── Configuration/
    │   │   └── FileProcessingConfigurationService.cs # Сервис конфигурации
    │   │
    │   ├── Factories/
    │   │   └── FileProcessorFactory.cs   # Фабрика для обработчиков файлов
    │   │
    │   └── UniversalFileProcessingService.cs # Основной сервис обработки
    │
    ├── appsettings.json                   # Основная конфигурация
    ├── appsettings.Development.json      # Конфигурация для разработки
    ├── docker-compose.yml                # Конфигурация Docker Compose
    ├── InfotecsTestTask.http             # Тестовые HTTP запросы
    └── Program.cs                        # Точка входа приложения
### Описание ключевых компонентов
    1. FilesController - обрабатывает все HTTP запросы:

        - UploadFile() - загрузка и обработка CSV файлов

        - GetResults() - получение агрегированных данных с фильтрацией

        - GetLatestValues() - получение последних значений по файлу

    2. UniversalFileProcessingService - основной сервис обработки:

        - Координирует весь процесс обработки файла

        - Управляет удалением старых данных и сохранением новых

        - Вызывает вычисление статистики

    AppDbContext - контекст базы данных:

        - Настроен для работы с TimescaleDB

        - Values настроена как гипертаблица для эффективного хранения временных рядов

        - Results содержит агрегированные данные

## Использование API        
Доступ к документации API:
Swagger UI: https://localhost:7141/swagger

Основные endpoint'ы:
    1. Загрузка CSV файла
        http
        POST /api/Files/upload
        Content-Type: multipart/form-data

        Параметры:
        - file: CSV файл в формате Date;ExecutionTime;Value          
2. Получение агрегированных результатов
        http
        GET /api/Files/results
        Параметры:
        - fileName: фильтр по имени файла (частичное совпадение)
        - minDateFrom, minDateTo: диапазон дат начала измерений
        - averageValueFrom, averageValueTo: диапазон средних значений
        - averageExecutionTimeFrom, averageExecutionTimeTo: диапазон среднего времени выполнения                  
3. Получение последних значений
        http
        GET /api/Files/{fileName}/values/latest/{n?}
        Параметры:
         - fileName: имя файла
         - n: количество записей (необязательный, по умолчанию 10, максимум 100)
Примеры использования:
        Загрузка файла через curl:
         bash
         curl -X POST "https://localhost:7141/api/Files/upload" \
         -H "Content-Type: multipart/form-data" \
         -F "file=@test.csv"
        Получение результатов с фильтрацией:
         bash
         curl "https://localhost:7141/api/Files/results?fileName=test.csv&averageValueFrom=100&averageValueTo=500"
         Получение последних 10 значений:
         bash
         curl "https://localhost:7141/api/Files/test.csv/values/latest/10"
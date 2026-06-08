using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Pozdravlyator
{
    // Модель данных: запись о дне рождения (упрощённая версия)
    public class BirthdayRecord
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }

        // Свойство для удобного отображения "день-месяц"
        public string DayMonth => BirthDate.ToString("dd.MM");
        
        // Возраст на текущую дату
        public int GetAge(DateTime currentDate)
        {
            int age = currentDate.Year - BirthDate.Year;
            if (currentDate.Month < BirthDate.Month || 
                (currentDate.Month == BirthDate.Month && currentDate.Day < BirthDate.Day))
                age--;
            return age;
        }

        // Следующий день рождения (ближайший в будущем или сегодня)
        public DateTime NextBirthday(DateTime currentDate)
        {
            var next = new DateTime(currentDate.Year, BirthDate.Month, BirthDate.Day);
            if (next < currentDate.Date)
                next = next.AddYears(1);
            return next;
        }

        // Дней до следующего ДР
        public int DaysUntilNext(DateTime currentDate)
        {
            return (NextBirthday(currentDate) - currentDate.Date).Days;
        }

        public override string ToString()
        {
            return $"{Id,3} | {FullName,-20} | {BirthDate:dd.MM.yyyy}";
        }
    }

    // Сервис для работы с коллекцией
    public class BirthdayService
    {
        private List<BirthdayRecord> _records = new();
        private int _nextId = 1;
        private readonly string _dataFile = "birthdays.json";

        public BirthdayService()
        {
            LoadFromFile();
        }

        // Загрузка из файла
        public void LoadFromFile()
        {
            if (File.Exists(_dataFile))
            {
                try
                {
                    string json = File.ReadAllText(_dataFile);
                    var loaded = JsonSerializer.Deserialize<List<BirthdayRecord>>(json);
                    if (loaded != null && loaded.Any())
                    {
                        _records = loaded;
                        _nextId = _records.Max(r => r.Id) + 1;
                        Console.WriteLine("Данные загружены из файла.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка загрузки: {ex.Message}");
                }
            }
        }

        // Сохранение в файл
        public void SaveToFile()
        {
            try
            {
                string json = JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_dataFile, json);
                Console.WriteLine("Данные сохранены в файл.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения: {ex.Message}");
            }
        }

        // Добавление
        public void Add(BirthdayRecord record)
        {
            record.Id = _nextId++;
            _records.Add(record);
            SaveToFile();
        }

        // Удаление
        public bool Delete(int id)
        {
            var record = _records.FirstOrDefault(r => r.Id == id);
            if (record != null)
            {
                _records.Remove(record);
                SaveToFile();
                return true;
            }
            return false;
        }

        // Редактирование
        public bool Update(int id, BirthdayRecord updated)
        {
            var existing = _records.FirstOrDefault(r => r.Id == id);
            if (existing != null)
            {
                existing.FullName = updated.FullName;
                existing.BirthDate = updated.BirthDate;
                SaveToFile();
                return true;
            }
            return false;
        }

        // Получить запись по Id
        public BirthdayRecord? GetById(int id) => _records.FirstOrDefault(r => r.Id == id);

        // Все записи
        public List<BirthdayRecord> GetAll() => _records.ToList();

        // Сегодняшние (день и месяц совпадают)
        public List<BirthdayRecord> GetToday(DateTime currentDate)
        {
            return _records.Where(r => r.BirthDate.Month == currentDate.Month && r.BirthDate.Day == currentDate.Day)
                           .OrderBy(r => r.FullName)
                           .ToList();
        }

        // Ближайшие (включая сегодня) на N дней вперед (по умолчанию 7)
        public List<BirthdayRecord> GetUpcoming(DateTime currentDate, int daysAhead = 7)
        {
            return _records.Where(r => 
                {
                    var next = r.NextBirthday(currentDate);
                    var daysLeft = (next - currentDate.Date).Days;
                    return daysLeft >= 0 && daysLeft <= daysAhead;
                })
                .OrderBy(r => r.DaysUntilNext(currentDate))
                .ThenBy(r => r.FullName)
                .ToList();
        }

        // Методы сортировки для всего списка
        public enum SortType
        {
            ByDateCalendar,  // по календарю (месяц/день)
            ByName,          // по имени
            ByAge,           // по возрасту
            ByDaysUntil      // по дням до ДР
        }

        public List<BirthdayRecord> GetSortedList(SortType sort, DateTime currentDate)
        {
            return sort switch
            {
                SortType.ByName => _records.OrderBy(r => r.FullName).ToList(),
                SortType.ByAge => _records.OrderByDescending(r => r.GetAge(currentDate)).ToList(),
                SortType.ByDaysUntil => _records.OrderBy(r => r.DaysUntilNext(currentDate)).ToList(),
                SortType.ByDateCalendar => _records.OrderBy(r => r.BirthDate.Month)
                                                   .ThenBy(r => r.BirthDate.Day)
                                                   .ThenBy(r => r.FullName).ToList(),
                _ => _records.ToList()
            };
        }
    }

    // UI - консольное меню
    public class ConsoleUI
    {
        private readonly BirthdayService _service;
        private readonly DateTime _currentDate;

        public ConsoleUI()
        {
            _service = new BirthdayService();
            _currentDate = DateTime.Now;
        }

        public void Run()
        {
            // При запуске сразу показываем сегодняшние и ближайшие ДР
            ShowTodayAndUpcoming();
            
            while (true)
            {
                ShowMainMenu();
                var choice = Console.ReadLine()?.Trim();
                
                switch (choice)
                {
                    case "1": ShowTodayAndUpcoming(); break;
                    case "2": ShowAllBirthdaysWithSorting(); break;
                    case "3": AddBirthday(); break;
                    case "4": DeleteBirthday(); break;
                    case "5": EditBirthday(); break;
                    case "0": 
                        Console.WriteLine("До свидания!");
                        return;
                    default:
                        Console.WriteLine("Неверный выбор. Нажмите Enter...");
                        Console.ReadLine();
                        break;
                }
            }
        }

        private void ShowMainMenu()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==========================================");
            Console.WriteLine("           ПОЗДРАВЛЯТОР                  ");
            Console.WriteLine("==========================================");
            Console.ResetColor();
            Console.WriteLine($"Сегодня: {_currentDate:dd.MM.yyyy} ({_currentDate:dddd})\n");
            Console.WriteLine("1. Показать сегодняшние и ближайшие ДР (7 дней)");
            Console.WriteLine("2. Показать весь список ДР");
            Console.WriteLine("3. Добавить запись");
            Console.WriteLine("4. Удалить запись");
            Console.WriteLine("5. Редактировать запись");
            Console.WriteLine("0. Выход");
            Console.Write("\nВыберите действие: ");
        }

        private void ShowTodayAndUpcoming()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("=== СЕГОДНЯШНИЕ И БЛИЖАЙШИЕ ДНИ РОЖДЕНИЯ ===\n");
            Console.ResetColor();

            var today = _service.GetToday(_currentDate);
            var upcoming = _service.GetUpcoming(_currentDate, 7);
            
            // Убираем из upcoming те, что уже в today, чтобы не дублировать
            var upcomingFiltered = upcoming.Where(u => !today.Any(t => t.Id == u.Id)).ToList();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"СЕГОДНЯ ({_currentDate:dd.MM}):");
            Console.ResetColor();
            if (today.Any())
            {
                foreach (var b in today)
                {
                    Console.WriteLine($"   - {b.FullName,-20} | Возраст: {b.GetAge(_currentDate)} лет");
                }
            }
            else
            {
                Console.WriteLine("   Нет дней рождения сегодня");
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"БЛИЖАЙШИЕ 7 ДНЕЙ:");
            Console.ResetColor();
            
            if (upcomingFiltered.Any())
            {
                foreach (var b in upcomingFiltered)
                {
                    int days = b.DaysUntilNext(_currentDate);
                    string daysText = days == 0 ? "СЕГОДНЯ" : days == 1 ? "завтра" : $"через {days} дней";
                    Console.WriteLine($"   - {b.FullName,-20} | {b.DayMonth} | ({daysText})");
                }
            }
            else
            {
                Console.WriteLine("   Нет ближайших дней рождения");
            }

            Console.WriteLine("\nНажмите Enter для продолжения...");
            Console.ReadLine();
        }

        private void ShowAllBirthdaysWithSorting()
        {
            while (true)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("=== ВЕСЬ СПИСОК ДНЕЙ РОЖДЕНИЯ ===\n");
                Console.ResetColor();
                
                // Меню выбора сортировки
                Console.WriteLine("Выберите тип сортировки:");
                Console.WriteLine("1. По календарю (месяц/день)");
                Console.WriteLine("2. По имени (А-Я)");
                Console.WriteLine("3. По возрасту (от старших к младшим)");
                Console.WriteLine("4. По дням до дня рождения");
                Console.WriteLine("0. Назад в главное меню");
                Console.Write("\nВаш выбор: ");
                
                string? choice = Console.ReadLine();
                
                if (choice == "0")
                    return;
                
                BirthdayService.SortType sortType = choice switch
                {
                    "1" => BirthdayService.SortType.ByDateCalendar,
                    "2" => BirthdayService.SortType.ByName,
                    "3" => BirthdayService.SortType.ByAge,
                    "4" => BirthdayService.SortType.ByDaysUntil,
                    _ => BirthdayService.SortType.ByDateCalendar
                };
                
                var all = _service.GetSortedList(sortType, _currentDate);
                
                if (!all.Any())
                {
                    Console.WriteLine("\nСписок пуст. Добавьте записи.");
                }
                else
                {
                    // Заголовок в зависимости от типа сортировки
                    Console.WriteLine($"\nСортировка: {GetSortTypeName(sortType)}");
                    Console.WriteLine(new string('=', 60));
                    Console.WriteLine($"{"ID",3} | {"ФИО",-25} | {"Дата рождения",-12} | {"Дней до ДР",-10}");
                    Console.WriteLine(new string('-', 60));
                    
                    foreach (var b in all)
                    {
                        int daysUntil = b.DaysUntilNext(_currentDate);
                        
                        // Подсветка сегодняшних
                        if (b.BirthDate.Month == _currentDate.Month && b.BirthDate.Day == _currentDate.Day)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write($"{b.Id,3} | {b.FullName,-25} | {b.BirthDate:dd.MM.yyyy}");
                            Console.ResetColor();
                            Console.WriteLine($" | {daysUntil,10} (СЕГОДНЯ)");
                        }
                        // Просроченные в этом году (уже были, но следующий в следующем году)
                        else if (b.NextBirthday(_currentDate).Year > _currentDate.Year)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.WriteLine($"{b.Id,3} | {b.FullName,-25} | {b.BirthDate:dd.MM.yyyy} | {daysUntil,10} (был в этом году)");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.WriteLine($"{b.Id,3} | {b.FullName,-25} | {b.BirthDate:dd.MM.yyyy} | {daysUntil,10}");
                        }
                    }
                }

                Console.WriteLine($"\nВсего записей: {all.Count}");
                Console.WriteLine("\nНажмите Enter для продолжения...");
                Console.ReadLine();
            }
        }

        private string GetSortTypeName(BirthdayService.SortType sortType)
        {
            return sortType switch
            {
                BirthdayService.SortType.ByDateCalendar => "По календарю (месяц/день)",
                BirthdayService.SortType.ByName => "По имени (А-Я)",
                BirthdayService.SortType.ByAge => "По возрасту (от старших)",
                BirthdayService.SortType.ByDaysUntil => "По дням до дня рождения",
                _ => "Стандартная"
            };
        }

        private void AddBirthday()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("=== ДОБАВЛЕНИЕ НОВОЙ ЗАПИСИ ===\n");
            Console.ResetColor();

            var newRecord = new BirthdayRecord();

            Console.Write("ФИО: ");
            newRecord.FullName = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(newRecord.FullName))
            {
                Console.WriteLine("ФИО не может быть пустым!");
                Console.ReadLine();
                return;
            }

            Console.Write("Дата рождения (дд.мм.гггг): ");
            if (!DateTime.TryParse(Console.ReadLine(), out DateTime birthDate))
            {
                Console.WriteLine("Неверный формат даты!");
                Console.ReadLine();
                return;
            }
            newRecord.BirthDate = birthDate;

            _service.Add(newRecord);
            Console.WriteLine("\nЗапись успешно добавлена!");
            Console.ReadLine();
        }

        private void DeleteBirthday()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("=== УДАЛЕНИЕ ЗАПИСИ ===\n");
            Console.ResetColor();

            ShowBriefList();
            
            Console.Write("\nВведите ID записи для удаления: ");
            if (int.TryParse(Console.ReadLine(), out int id))
            {
                var record = _service.GetById(id);
                if (record != null)
                {
                    Console.WriteLine($"\nВы уверены, что хотите удалить: {record.FullName} ({record.BirthDate:dd.MM.yyyy})? (y/n)");
                    if (Console.ReadLine()?.ToLower() == "y")
                    {
                        if (_service.Delete(id))
                            Console.WriteLine("Запись удалена!");
                        else
                            Console.WriteLine("Ошибка удаления.");
                    }
                    else
                    {
                        Console.WriteLine("Удаление отменено.");
                    }
                }
                else
                {
                    Console.WriteLine("Запись не найдена.");
                }
            }
            else
            {
                Console.WriteLine("Неверный ID.");
            }
            Console.ReadLine();
        }

        private void EditBirthday()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== РЕДАКТИРОВАНИЕ ЗАПИСИ ===\n");
            Console.ResetColor();

            ShowBriefList();
            
            Console.Write("\nВведите ID записи для редактирования: ");
            if (int.TryParse(Console.ReadLine(), out int id))
            {
                var existing = _service.GetById(id);
                if (existing != null)
                {
                    Console.WriteLine($"\nРедактируем: {existing.FullName}\n");
                    
                    var updated = new BirthdayRecord();
                    updated.Id = id; // ID остаётся прежним

                    Console.Write($"Новое ФИО (было: {existing.FullName}): ");
                    string input = Console.ReadLine()?.Trim();
                    updated.FullName = string.IsNullOrWhiteSpace(input) ? existing.FullName : input;

                    Console.Write($"Новая дата рождения (было: {existing.BirthDate:dd.MM.yyyy}): ");
                    input = Console.ReadLine()?.Trim();
                    if (DateTime.TryParse(input, out DateTime newDate))
                        updated.BirthDate = newDate;
                    else
                        updated.BirthDate = existing.BirthDate;

                    if (_service.Update(id, updated))
                        Console.WriteLine("Запись обновлена!");
                    else
                        Console.WriteLine("Ошибка обновления.");
                }
                else
                {
                    Console.WriteLine("Запись не найдена.");
                }
            }
            else
            {
                Console.WriteLine("Неверный ID.");
            }
            Console.ReadLine();
        }

        private void ShowBriefList()
        {
            var all = _service.GetAll();
            if (!all.Any())
            {
                Console.WriteLine("Список пуст.");
                return;
            }
            
            Console.WriteLine("Текущие записи:");
            Console.WriteLine($"{"ID",3} | {"ФИО",-25} | {"Дата",-10}");
            Console.WriteLine(new string('-', 45));
            foreach (var b in all.Take(10))
            {
                Console.WriteLine($"{b.Id,3} | {b.FullName,-25} | {b.BirthDate:dd.MM.yyyy}");
            }
            if (all.Count > 10)
                Console.WriteLine($"... и ещё {all.Count - 10} записей");
        }
    }

    // Точка входа
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            var app = new ConsoleUI();
            app.Run();
        }
    }
}